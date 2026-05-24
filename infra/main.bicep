// =========================================================
// FCG Notifications - Container App com KEDA RabbitMQ scaler.
// Substitui o container 24/7 por execucao serverless escalando 0..N.
// =========================================================

@description('Localizacao dos recursos (ex.: brazilsouth, eastus)')
param location string = resourceGroup().location

@description('Nome base dos recursos')
param namePrefix string = 'fcg-notifications'

@description('Imagem do container (ex.: myacr.azurecr.io/fcg-notifications-fn:tag)')
param containerImage string

@description('Login server do ACR (ex.: myacr.azurecr.io)')
param acrLoginServer string

@description('Resource ID da identidade gerenciada com AcrPull no registry')
param userAssignedIdentityId string

@description('Usuario do RabbitMQ')
param rabbitMqUser string = 'fcg'

@description('Senha do RabbitMQ')
@secure()
param rabbitMqPassword string

@description('Limite de mensagens por replica que dispara scale-up no KEDA')
param kedaQueueLength string = '5'

@description('Maximo de replicas concorrentes')
param maxReplicas int = 5

@description('Host SMTP')
param smtpHost string
@description('Porta SMTP')
param smtpPorta string = '587'
@description('Remetente padrao')
param smtpRemetente string = 'noreply@fcg.com'
@description('Usuario SMTP (opcional)')
param smtpUsuario string = ''
@description('Senha SMTP (opcional)')
@secure()
param smtpSenha string = ''

// ----------- Log Analytics (necessario para Container Apps Environment) ------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ----------- Application Insights (telemetria das functions) -----------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ----------- Container Apps Environment --------------------------------------------
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ----------- Storage Account para persistencia do RabbitMQ -------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: replace('${namePrefix}stor', '-', '')
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: 'rabbitmq-data'
  parent: fileService
  properties: {
    shareQuota: 1
  }
}

// ----------- Volume Azure Files no Container Apps Environment ----------------------
resource environmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: 'rabbitmq-storage'
  parent: environment
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// ----------- RabbitMQ Container App ------------------------------------------------
var rabbitMqAppName = '${namePrefix}-rabbitmq'
var encodedRabbitMqPassword = replace(replace(replace(rabbitMqPassword, '@', '%40'), '/', '%2F'), '#', '%23')
var rabbitMqInternalFqdn = '${rabbitMqAppName}.internal.${environment.properties.defaultDomain}'
var rabbitMqConnectionString = 'amqp://${rabbitMqUser}:${encodedRabbitMqPassword}@${rabbitMqInternalFqdn}:5672/'

resource rabbitMqApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: rabbitMqAppName
  location: location
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        transport: 'tcp'
        exposedPort: 5672
        targetPort: 5672
      }
      secrets: [
        { name: 'rabbitmq-password', value: rabbitMqPassword }
      ]
    }
    template: {
      volumes: [
        {
          name: 'rabbitmq-data'
          storageType: 'AzureFile'
          storageName: environmentStorage.name
        }
      ]
      containers: [
        {
          name: 'rabbitmq'
          image: 'rabbitmq:4-management'
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            { name: 'RABBITMQ_DEFAULT_USER', value: rabbitMqUser }
            { name: 'RABBITMQ_DEFAULT_PASS', secretRef: 'rabbitmq-password' }
          ]
          volumeMounts: [
            {
              volumeName: 'rabbitmq-data'
              mountPath: '/var/lib/rabbitmq'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ----------- Container App (FCG Notifications Function) ----------------------------
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-fn'
  location: location
  dependsOn: [rabbitMqApp]
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: concat(
        [
          #disable-next-line use-secure-value-for-secure-inputs
          { name: 'rabbitmq-connection', value: rabbitMqConnectionString }
          #disable-next-line use-secure-value-for-secure-inputs
          { name: 'storage-connection', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net' }
        ],
        empty(smtpSenha) ? [] : [{ name: 'smtp-senha', value: smtpSenha }]
      )
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'fcg-notifications-fn'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            { name: 'RabbitMq', secretRef: 'rabbitmq-connection' }
            { name: 'AzureWebJobsStorage', secretRef: 'storage-connection' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
            { name: 'Smtp__Host', value: smtpHost }
            { name: 'Smtp__Porta', value: smtpPorta }
            { name: 'Smtp__Remetente', value: smtpRemetente }
            { name: 'Smtp__Usuario', value: smtpUsuario }
            ...(!empty(smtpSenha) ? [{ name: 'Smtp__Senha', secretRef: 'smtp-senha' }] : [])
            { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'rabbitmq-user-created'
            custom: {
              type: 'rabbitmq'
              metadata: {
                queueName: 'user-created-queue'
                mode: 'QueueLength'
                value: kedaQueueLength
              }
              auth: [
                { secretRef: 'rabbitmq-connection', triggerParameter: 'host' }
              ]
            }
          }
          {
            name: 'rabbitmq-order-placed'
            custom: {
              type: 'rabbitmq'
              metadata: {
                queueName: 'order-placed-queue'
                mode: 'QueueLength'
                value: kedaQueueLength
              }
              auth: [
                { secretRef: 'rabbitmq-connection', triggerParameter: 'host' }
              ]
            }
          }
          {
            name: 'rabbitmq-payment-processed'
            custom: {
              type: 'rabbitmq'
              metadata: {
                queueName: 'payment-processed-queue'
                mode: 'QueueLength'
                value: kedaQueueLength
              }
              auth: [
                { secretRef: 'rabbitmq-connection', triggerParameter: 'host' }
              ]
            }
          }
        ]
      }
    }
  }
}

output containerAppName string = containerApp.name
output rabbitMqAppName string = rabbitMqApp.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
