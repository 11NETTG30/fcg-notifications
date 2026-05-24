# fcg-notifications

Função **AWS Lambda** de notificações do **FIAP Cloud Games (FCG)**.

Acionada diretamente por mensagens do **Amazon MQ for RabbitMQ** (event source mapping da Lambda), substituindo o container 24/7 que rodava continuamente. Envia e-mails transacionais via SMTP em resposta a eventos de domínio publicados pelos demais microsserviços.

---

## Tecnologias

| Categoria | Tecnologia |
|---|---|
| Runtime | .NET 10 (container image, base `public.ecr.aws/lambda/provided:al2023`) |
| Linguagem | C# 14 |
| Empacotamento | Container Image publicada no Amazon ECR |
| IaC | AWS SAM (`infra/template.yaml`) |
| Trigger | Amazon MQ for RabbitMQ (Event Source Mapping) |
| E-mail | MailKit (SMTP, compatível com Amazon SES / Mailpit) |
| CI/CD | GitHub Actions com OIDC -> AWS |

---

## Arquitetura

```
Producers (UsersAPI, PaymentsAPI, CatalogAPI)
        │ publish (MassTransit / RabbitMQ)
        ▼
Amazon MQ for RabbitMQ (broker gerenciado, VPC privada)
   ├── user-created-queue
   ├── order-placed-queue
   └── payment-processed-queue
        │ event source mapping
        ▼
AWS Lambda (fcg-notifications-fn)
        │
        ├── Dispatcher (roteia pelo nome da fila)
        ├── EventoHandler<T> (UserCreated / OrderPlaced / PaymentProcessed)
        └── ServicoEmail (SMTP -> SES / outro)
```

### Estrutura do código

```
src/
├── FCG.Notifications.Application/
│   ├── Interfaces/IServicoEmail.cs
│   ├── Templates/ICarregadorTemplate.cs
│   └── EventHandlers/
│       ├── IEventoHandler.cs
│       ├── UserCreatedHandler.cs
│       ├── OrderPlacedHandler.cs
│       └── PaymentProcessedHandler.cs
├── FCG.Notifications.Infrastructure/
│   ├── Email/ServicoEmail.cs
│   ├── Templates/CarregadorTemplate.cs + *.html (EmbeddedResource)
│   └── Messaging/MassTransitEnvelope.cs (parser do envelope)
└── FCG.Notifications.Function/
    ├── Function.cs       <- entrypoint Lambda (LambdaBootstrapBuilder)
    ├── Dispatcher.cs     <- roteia por nome de fila
    ├── CompositionRoot.cs<- DI (Microsoft.Extensions.DependencyInjection)
    └── Dockerfile        <- multi-stage com SDK 10 + provided:al2023

infra/
├── template.yaml         <- SAM (Lambda + EventSourceMapping + IAM)
└── samconfig.toml

.github/workflows/
└── deploy-lambda.yml     <- build da imagem, push ECR, sam deploy
```

### Como a Lambda processa uma mensagem

1. A Event Source Mapping da Lambda assina as filas configuradas no broker.
2. A AWS entrega um `RabbitMQEvent` contendo `RmqMessagesByQueue` com pares `{ "<fila>::<vhost>": [mensagens] }`.
3. `Function.HandlerAsync` itera o batch e delega ao `Dispatcher`.
4. O `Dispatcher` decodifica o `Data` (base64), identifica a fila e desserializa o envelope MassTransit em um DTO de `FCG.Shared.Contracts.Events`.
5. O `IEventoHandler<T>` correspondente carrega o template HTML e dispara o e-mail via SMTP.

Falhas são logadas mas não propagadas, preservando o ack do batch (mesmo comportamento do consumer original).

> Consulte [fcg-shared](https://github.com/11NETTG30/fcg-shared) para os contratos de evento (`UserCreatedEvent`, `OrderPlacedEvent`, `PaymentProcessedEvent`).

---

## Build e teste local

> Não há mais API HTTP — este projeto é exclusivamente uma função Lambda.

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/)
- [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)
- [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- Acesso ao GitHub Packages da organização [11NETTG30](https://github.com/11NETTG30) (token com escopo `read:packages` exportado como `NUGET_AUTH_TOKEN`)

### Compilar localmente

```bash
export NUGET_AUTH_TOKEN=<seu_token_pat>
dotnet build src/FCG.Notifications.Function/FCG.Notifications.Function.csproj
```

### Invocar localmente com SAM (opcional)

Crie um arquivo `events/rabbitmq.json` com um payload de exemplo:

```json
{
  "eventSource": "aws:rmq",
  "eventSourceArn": "arn:aws:mq:us-east-1:123456789012:broker:fcg-broker:b-xxxxxx",
  "rmqMessagesByQueue": {
    "user-created-queue::/": [
      {
        "basicProperties": { "contentType": "application/json" },
        "data": "<JSON_DO_ENVELOPE_MASSTRANSIT_EM_BASE64>",
        "redelivered": false
      }
    ]
  }
}
```

```bash
docker build \
  --secret id=nuget_auth_token,env=NUGET_AUTH_TOKEN \
  -f src/FCG.Notifications.Function/Dockerfile \
  -t fcg-notifications-fn:dev .

sam local invoke NotificationsFunction \
  --template infra/template.yaml \
  --event events/rabbitmq.json \
  --docker-network host
```

---

## Setup AWS — Passo a passo

> Tudo abaixo é executado **uma única vez** para preparar a conta AWS antes do primeiro deploy.

### 1. Configurar AWS CLI

```bash
aws configure
# Region: us-east-1 (ou a sua escolha — atualize template.yaml/workflow se mudar)
```

### 2. Provisionar VPC (se ainda não existir)

A Lambda precisa estar na mesma VPC do broker Amazon MQ. Reutilize uma VPC existente ou crie pelo console:
- 2 subnets privadas em AZs diferentes
- 1 NAT Gateway (necessário para a Lambda alcançar a Internet — SES, Secrets Manager, etc.)
- 1 Security Group para a Lambda (saída para 5671/TCP no broker e 443 para serviços AWS)
- 1 Security Group para o broker (entrada para 5671/TCP do SG da Lambda)

Anote os IDs: `subnet-aaa, subnet-bbb` e `sg-lambda, sg-broker`.

### 3. Criar o broker Amazon MQ for RabbitMQ

Console -> Amazon MQ -> Create broker -> RabbitMQ:
- Engine: RabbitMQ 3.13+
- Deployment: Single-instance (para dev) ou Cluster (produção)
- Storage: EBS
- VPC: a do passo 2; Subnet: privada
- Security group: `sg-broker`
- Usuário/senha do broker: defina (ex.: `fcg-admin / SenhaSegura123`)

Após criado, anote o **ARN do broker** (algo como `arn:aws:mq:us-east-1:123456789012:broker:fcg-broker:b-...`).

### 4. Guardar as credenciais do broker no Secrets Manager

```bash
aws secretsmanager create-secret \
  --name fcg/rabbitmq/credentials \
  --secret-string '{"username":"fcg-admin","password":"SenhaSegura123"}'
```

Anote o **ARN do Secret** retornado.

### 5. Criar repositório ECR para a imagem da função

```bash
aws ecr create-repository --repository-name fcg-notifications-fn --region us-east-1
```

### 6. Configurar saída de e-mail (recomendado: Amazon SES)

1. Console -> SES -> verifique o domínio do remetente (ex.: `fcg.com`) ou ao menos um e-mail individual.
2. Saia do sandbox do SES (se ainda estiver) — abra ticket de produção, ou continue no sandbox enviando apenas para e-mails verificados.
3. SES -> SMTP settings -> **Create SMTP credentials** -> guarde **SMTP username** e **SMTP password** (não confundir com chave AWS).
4. Host SMTP: `email-smtp.us-east-1.amazonaws.com`, porta `587`.

> Se preferir SMTP externo (Brevo, SendGrid, Mailgun, etc.), basta apontar `SmtpHost` / `SmtpUsuario` / `SmtpSenha` para o provedor.

### 7. Configurar OIDC para o GitHub Actions (deploy automatizado)

a) Crie o **Identity Provider OIDC** na AWS apontando para `https://token.actions.githubusercontent.com` (audience `sts.amazonaws.com`).
b) Crie uma **IAM Role** com trust policy:
```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": { "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com" },
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": { "token.actions.githubusercontent.com:aud": "sts.amazonaws.com" },
      "StringLike":   { "token.actions.githubusercontent.com:sub": "repo:11NETTG30/fcg-notifications:*" }
    }
  }]
}
```
c) Anexe a política gerenciada `AWSCloudFormationFullAccess` + uma política inline mínima:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    { "Effect": "Allow", "Action": ["ecr:*", "lambda:*", "iam:PassRole", "iam:CreateRole", "iam:AttachRolePolicy", "iam:PutRolePolicy", "iam:DeleteRole", "iam:DetachRolePolicy", "iam:DeleteRolePolicy", "iam:GetRole", "iam:TagRole", "logs:*", "ec2:DescribeSubnets", "ec2:DescribeSecurityGroups", "ec2:DescribeVpcs", "secretsmanager:GetSecretValue"], "Resource": "*" }
  ]
}
```
d) Anote o **ARN da Role** (`arn:aws:iam::<ACCOUNT_ID>:role/GhActionsDeployRole`).

### 8. Cadastrar os Secrets no GitHub

Repositório no GitHub -> Settings -> Secrets and variables -> Actions -> New repository secret:

| Secret | Valor |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | ARN da role do passo 7d |
| `MQ_BROKER_ARN` | ARN do broker do passo 3 |
| `MQ_BROKER_SECRET_ARN` | ARN do Secret do passo 4 |
| `VPC_SUBNET_IDS` | `subnet-aaa,subnet-bbb` |
| `VPC_SECURITY_GROUP_IDS` | `sg-lambda` |
| `SMTP_HOST` | `email-smtp.us-east-1.amazonaws.com` |
| `SMTP_PORTA` | `587` |
| `SMTP_REMETENTE` | `noreply@<seu-dominio>` |
| `SMTP_USUARIO` | SMTP username do passo 6 |
| `SMTP_SENHA` | SMTP password do passo 6 |
| `NUGET_AUTH_TOKEN` | PAT do GitHub com `read:packages` para baixar `FCG.*` |

### 9. Primeiro deploy

Opção A — via GitHub Actions (recomendado):
- Actions -> **Deploy Lambda (ECR + SAM)** -> **Run workflow** na branch `main`.

Opção B — via terminal local:
```bash
export NUGET_AUTH_TOKEN=<seu_token>

# build + push da imagem
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_URI=$ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/fcg-notifications-fn
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin $ECR_URI

docker build \
  --secret id=nuget_auth_token,env=NUGET_AUTH_TOKEN \
  -f src/FCG.Notifications.Function/Dockerfile \
  --platform linux/amd64 \
  -t $ECR_URI:latest .
docker push $ECR_URI:latest

# deploy SAM
cd infra
sam deploy --guided \
  --image-repositories NotificationsFunction=$ECR_URI \
  --parameter-overrides \
    ImageUri=$ECR_URI:latest \
    BrokerArn=arn:aws:mq:... \
    BrokerCredentialsSecretArn=arn:aws:secretsmanager:... \
    VpcSubnetIds=subnet-aaa,subnet-bbb \
    VpcSecurityGroupIds=sg-lambda \
    SmtpHost=email-smtp.us-east-1.amazonaws.com \
    SmtpPorta=587 \
    SmtpRemetente=noreply@fcg.com \
    SmtpUsuario=AKIA... \
    SmtpSenha=...
```

### 10. Apontar os producers para o Amazon MQ

Nos demais microsserviços (UsersAPI, PaymentsAPI, CatalogAPI), troque o host do RabbitMQ pela URL do broker Amazon MQ (`<broker-id>.mq.us-east-1.amazonaws.com:5671`, com TLS habilitado) e as credenciais para as do passo 3. Os nomes das filas/exchanges continuam os mesmos.

---

## Observabilidade

Logs da Lambda são entregues automaticamente ao **CloudWatch Logs** (`/aws/lambda/fcg-notifications-fn`).

Para integrar com Datadog/New Relic (opção B do Tech Challenge), basta adicionar a respectiva Lambda Extension ao Dockerfile (camada do agente) e definir as variáveis de ambiente correspondentes (`DD_API_KEY` etc.) no `template.yaml`.

---

## Atendimento ao Tech Challenge — Fase 3

| Requisito do PDF | Como atendido |
|---|---|
| Migrar NotificationsAPI para Serverless | Container API removido; agora é AWS Lambda (`fcg-notifications-fn`) com `PackageType: Image` |
| Função acionada diretamente por mensagens | Event source mapping `Type: MQ` no `template.yaml` apontando para o broker e suas filas |
| Código + IaC em repositório próprio | Este repositório é "o repositório da função": código em `src/FCG.Notifications.Function/`, IaC SAM em `infra/template.yaml` |
| Observabilidade | Logs CloudWatch automáticos; pronto para receber extensão APM (Datadog/New Relic) via Dockerfile |
