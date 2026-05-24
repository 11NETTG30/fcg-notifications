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