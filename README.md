# fcg-notifications

**Azure Function** de notificações do **FIAP Cloud Games (FCG)**, hospedada em **Azure Container Apps** com **KEDA** fazendo scale-to-zero baseado em profundidade das filas RabbitMQ.

Substitui o container 24/7 da NotificationsAPI por execução serverless: quando há mensagem na fila, KEDA acorda o container; quando a fila esvazia e fica ociosa, ele desliga. Custo pay-per-use.

---

## Tecnologias

| Categoria | Tecnologia |
|---|---|
| Runtime | .NET 10 (self-contained, isolated worker) |
| Framework | Azure Functions v4 (`Microsoft.Azure.Functions.Worker`) |
| Trigger | RabbitMQ (extension oficial — funciona com qualquer broker) |
| Hospedagem | Azure Container Apps (scale-to-zero via KEDA) |
| Registry | Azure Container Registry |
| Observabilidade | Application Insights + Log Analytics |
| IaC | Bicep (`infra/main.bicep`) |
| CI/CD | GitHub Actions com OIDC → Azure |
| E-mail | MailKit (SMTP — SendGrid / SES SMTP / Mailpit) |

---

## Arquitetura

```
Producers (UsersAPI, PaymentsAPI, CatalogAPI)
        │ publish (MassTransit / RabbitMQ)
        ▼
RabbitMQ (broker existente em fcg-infra/k8s, sem mudanças)
   ├── user-created-queue
   ├── order-placed-queue
   └── payment-processed-queue
        ▲                              ▲
        │ peek (queue length)          │ consume
        │                              │
   KEDA RabbitMQ Scaler ─── escala ──► Container App (0..N replicas)
                                              │
                                              ├── 3 Azure Functions (RabbitMQTrigger)
                                              ├── EventoHandler<T>
                                              └── ServicoEmail (SMTP)
```

**Por que Container Apps em vez de Functions Premium/B1?**
- Premium plan: ~US$ 140/mês (overkill)
- App Service Plan B1: ~US$ 13/mês mas sem scale-to-zero
- **Container Apps**: pay-per-use real, sem instância ociosa, com KEDA decidindo a escala

### Estrutura do código

```
src/
├── FCG.Notifications.Application/
│   ├── Interfaces/IServicoEmail.cs
│   ├── Templates/ICarregadorTemplate.cs
│   └── EventHandlers/  (IEventoHandler<T> + 3 handlers POCO)
├── FCG.Notifications.Infrastructure/
│   ├── Email/ServicoEmail.cs
│   ├── Templates/CarregadorTemplate.cs + *.html (EmbeddedResource)
│   └── Messaging/MassTransitEnvelope.cs   ← parser do envelope MassTransit
└── FCG.Notifications.Function/
    ├── Program.cs                ← host builder + DI
    ├── NotificationFunctions.cs  ← 3 funções (uma por queue) com [RabbitMQTrigger]
    ├── host.json
    ├── local.settings.json
    └── Dockerfile

infra/
├── main.bicep                    ← Container App + ACR ref + KEDA scalers + AppInsights
└── main.parameters.example.json

.github/workflows/
└── deploy-azure.yml              ← OIDC -> ACR push -> az deployment group
```

### Fluxo de uma mensagem
1. Producer publica `OrderPlacedEvent` no exchange `OrderPlacedEvent` (fanout, configurado pelo MassTransit).
2. Mensagem chega em `order-placed-queue` (bound ao exchange).
3. KEDA detecta `messageCount >= 1` e escala o Container App de 0 para 1.
4. Functions Host arranca, o trigger `[RabbitMQTrigger("order-placed-queue", ...)]` conecta e consome.
5. `MassTransitEnvelopeReader.Extrair<T>` desserializa o envelope, extraindo o payload.
6. `OrderPlacedHandler` carrega o template HTML e dispara o e-mail via SMTP.
7. Após ~5 min sem mensagens novas, KEDA escala de volta para 0.

---

## Build e teste local

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- RabbitMQ acessível (`docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management`)
- Mailpit (`docker run -p 1025:1025 -p 8025:8025 axllent/mailpit`) ou outro SMTP
- Token GitHub Packages (`NUGET_AUTH_TOKEN`) para pacotes `FCG.*`

### Rodar com Azure Functions Core Tools
```bash
export NUGET_AUTH_TOKEN=<seu_pat>
cd src/FCG.Notifications.Function
func start
```

Os valores em `local.settings.json` apontam para RabbitMQ e SMTP locais (porta 5672 e 1025 respectivamente).

### Testar via container localmente
```bash
docker build \
  --secret id=nuget_auth_token,env=NUGET_AUTH_TOKEN \
  -f src/FCG.Notifications.Function/Dockerfile \
  -t fcg-notifications-fn:dev .

docker run --rm \
  -e RabbitMq="amqp://guest:guest@host.docker.internal:5672/" \
  -e Smtp__Host=host.docker.internal -e Smtp__Porta=1025 \
  -e FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
  fcg-notifications-fn:dev
```

---

## Setup Azure — Passo a passo

> Tudo abaixo é executado **uma vez** para preparar a assinatura Azure antes do primeiro deploy.

### 1. Login Azure CLI
```bash
az login
az account set --subscription "<sua-subscription-id>"
```

### 2. Criar Resource Group
```bash
RG=fcg-notifications-rg
LOC=brazilsouth
az group create --name $RG --location $LOC
```

### 3. Criar Azure Container Registry
```bash
ACR_NAME=fcgnotifications   # precisa ser unico globalmente
az acr create --resource-group $RG --name $ACR_NAME --sku Basic --admin-enabled false
```

### 4. Criar User-Assigned Managed Identity (para o Container App puxar do ACR)
```bash
IDENTITY_NAME=fcg-aca-identity
az identity create --resource-group $RG --name $IDENTITY_NAME

IDENTITY_ID=$(az identity show -g $RG -n $IDENTITY_NAME --query id -o tsv)
IDENTITY_PRINCIPAL_ID=$(az identity show -g $RG -n $IDENTITY_NAME --query principalId -o tsv)
ACR_ID=$(az acr show -g $RG -n $ACR_NAME --query id -o tsv)

# Conceder AcrPull
az role assignment create \
  --assignee-object-id $IDENTITY_PRINCIPAL_ID \
  --assignee-principal-type ServicePrincipal \
  --role AcrPull \
  --scope $ACR_ID
```

Guarde o valor de `$IDENTITY_ID` — vai virar a secret `ACA_IDENTITY_ID`.

### 5. Expor o RabbitMQ atual (fcg-infra) para o Container App
O broker que vive no Kubernetes do `fcg-infra` precisa estar acessível pelo Container App. Opções:

**a) Mais simples (dev/projeto de pós):** expor o RabbitMQ via LoadBalancer público com TLS + autenticação. No `fcg-infra/k8s/rabbitmq/service.yaml`, mude o `type` para `LoadBalancer`. Pegue o IP público resultante e ajuste o `RABBITMQ_CONNECTION_STRING` com TLS (porta 5671) e usuário/senha fortes.

**b) Mais seguro:** Container Apps Environment com **VNet integration** apontando para a mesma VNet do AKS. Requer subnet dedicada `/23` e configuração mais elaborada — fora do escopo desta documentação.

A connection string completa deve ficar tipo:
```
amqps://fcguser:senhaforte@meu-broker-publico.com:5671/
```
ou para teste com broker sem TLS (não recomendado em rede pública):
```
amqp://guest:guest@meu-broker-publico.com:5672/
```

### 6. Configurar SMTP
Recomendado: **SendGrid** (free tier 100 e-mails/dia), Azure Communication Services Email, ou Mailgun.
- Host (SendGrid): `smtp.sendgrid.net`, porta `587`
- Usuário: `apikey`
- Senha: a API key

### 7. Criar App Registration para OIDC do GitHub Actions
```bash
APP_NAME=gh-fcg-notifications
az ad app create --display-name $APP_NAME
APP_ID=$(az ad app list --display-name $APP_NAME --query "[0].appId" -o tsv)
az ad sp create --id $APP_ID
SP_OBJECT_ID=$(az ad sp list --display-name $APP_NAME --query "[0].id" -o tsv)

# Federated credential — substitui senha por OIDC
az ad app federated-credential create --id $APP_ID --parameters "{
  \"name\":\"github-main\",
  \"issuer\":\"https://token.actions.githubusercontent.com\",
  \"subject\":\"repo:11NETTG30/fcg-notifications:ref:refs/heads/main\",
  \"audiences\":[\"api://AzureADTokenExchange\"]
}"

# Permissoes no resource group
SUB_ID=$(az account show --query id -o tsv)
az role assignment create --assignee $APP_ID \
  --role Contributor --scope /subscriptions/$SUB_ID/resourceGroups/$RG
az role assignment create --assignee $APP_ID \
  --role AcrPush --scope $ACR_ID

echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID=$SUB_ID"
```

### 8. Cadastrar secrets no GitHub
Settings → Secrets and variables → Actions:

| Secret | Valor |
|---|---|
| `AZURE_CLIENT_ID` | App ID do passo 7 |
| `AZURE_TENANT_ID` | Tenant ID do passo 7 |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID |
| `ACA_IDENTITY_ID` | `$IDENTITY_ID` do passo 4 |
| `RABBITMQ_CONNECTION_STRING` | `amqps://user:pass@host:5671/` do passo 5 |
| `SMTP_HOST` | `smtp.sendgrid.net` (ou seu provedor) |
| `SMTP_PORTA` | `587` |
| `SMTP_REMETENTE` | seu remetente |
| `SMTP_USUARIO` | `apikey` (SendGrid) ou usuário do provedor |
| `SMTP_SENHA` | API key ou senha |
| `NUGET_AUTH_TOKEN` | PAT com `read:packages` |

### 9. Primeiro deploy
**Opção A (recomendado):** Actions → **Deploy Container App (ACR + Bicep)** → Run workflow.

**Opção B (local):**
```bash
export NUGET_AUTH_TOKEN=<seu_pat>
az acr login --name $ACR_NAME

docker build \
  --secret id=nuget_auth_token,env=NUGET_AUTH_TOKEN \
  -f src/FCG.Notifications.Function/Dockerfile \
  --platform linux/amd64 \
  -t $ACR_NAME.azurecr.io/fcg-notifications-fn:v1 .
docker push $ACR_NAME.azurecr.io/fcg-notifications-fn:v1

az deployment group create \
  --resource-group $RG \
  --template-file infra/main.bicep \
  --parameters \
    containerImage=$ACR_NAME.azurecr.io/fcg-notifications-fn:v1 \
    acrLoginServer=$ACR_NAME.azurecr.io \
    userAssignedIdentityId=$IDENTITY_ID \
    rabbitMqConnectionString="amqps://user:pass@host:5671/" \
    smtpHost=smtp.sendgrid.net smtpPorta=587 \
    smtpRemetente=noreply@fcg.com \
    smtpUsuario=apikey smtpSenha="SG.xxx"
```

### 10. Verificar funcionamento
- Publique uma mensagem em qualquer fila e observe no portal Azure: Container App → **Revisions and replicas** — uma replica deve aparecer em ~30s, processar, e sumir após o `cooldown` (default 5 min).
- Logs: Container App → **Log stream** ou Application Insights → **Live Metrics**.

---

## Mudanças necessárias **fora deste repo** (não foram aplicadas)

### Em `fcg-infra`
- Remover `k8s/fcg-notifications/` (deployment obsoleto da NotificationsAPI).
- Em `k8s/rabbitmq/service.yaml`, mudar `type: ClusterIP` para `type: LoadBalancer` se for usar o caminho de exposição pública. Manter `secret.yaml` com usuário admin novo (não usar `guest/guest` na internet).

### Em `fcg-users`, `fcg-payments`, `fcg-catalog`
**Nenhuma mudança de código.** Os producers continuam publicando no mesmo broker via MassTransit. Só precisam:
- Apontar `RabbitMQ:Host`/`RabbitMQ:Username`/`RabbitMQ:Password` para o broker exposto (novas credenciais).
- Garantir conectividade rede.

> Esse é o grande ganho de continuar com RabbitMQ em vez de migrar pra SNS/SQS ou Service Bus: producers ficam intocados.

---

## Atendimento ao Tech Challenge — Fase 3

| Requisito do PDF | Como atendido |
|---|---|
| Migrar NotificationsAPI para Função Serverless | Azure Functions isolated worker em Container Apps com KEDA scale-to-zero |
| Função acionada diretamente por mensagens da fila | `[RabbitMQTrigger]` consome direto das 3 filas; KEDA escala 0→N por queue length |
| Código + IaC em repositório próprio | Este repo: código em `src/FCG.Notifications.Function/`, IaC Bicep em `infra/main.bicep` |
| Observabilidade | Application Insights ligado via `ConfigureFunctionsApplicationInsights()` + logs em Log Analytics |

---

## Custo estimado (dev/projeto)

- Container Apps: ~US$ 0 enquanto em 0 réplicas; ~US$ 0.000024/vCPU·s + US$ 0.000003/GB·s em execução
- Log Analytics: 5GB grátis/mês
- Application Insights: 5GB grátis/mês
- ACR Basic: ~US$ 5/mês
- **Total típico em demos/projeto: < US$ 5/mês**

## Limitações conhecidas

- **`Microsoft.Azure.Functions.Worker.Extensions.RabbitMQ`** ainda esteve em preview por bastante tempo; verifique no [release notes](https://github.com/Azure/azure-functions-rabbitmq-extension) se a versão usada (1.0.0) está estável no seu cenário. Se houver problema, considere descer para a versão `0.x` mais recente.
- A imagem base `mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0` traz .NET 8; nossa publish é self-contained .NET 10, o que funciona porque o Functions Host é desacoplado do runtime do worker (gRPC). Se sair imagem oficial `:4-dotnet-isolated10.0`, atualize a tag no `Dockerfile`.
- **Não consegui rodar `dotnet build` na sessão** pois `FCG.Shared.Contracts` exige autenticação no GitHub Packages. Rode `dotnet build` localmente com `NUGET_AUTH_TOKEN` exportado para validar antes do primeiro deploy.
