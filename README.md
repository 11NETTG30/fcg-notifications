# fcg-notifications

Microsserviço de notificações do **FIAP Cloud Games (FCG)**.

Responsável por enviar e-mails transacionais em resposta a eventos publicados pelos demais microsserviços via RabbitMQ. Os e-mails são disparados usando templates HTML e entregues via SMTP (Mailpit em desenvolvimento).

---

## 🛠️ Tecnologias Utilizadas

| Categoria | Tecnologia / Ferramenta |
|---|---|
| Plataforma | .NET 10 |
| Framework Web | ASP.NET Core 10 |
| Linguagem | C# 14 |
| Mensageria | RabbitMQ + MassTransit |
| E-mail | MailKit (SMTP) |
| SMTP dev | Mailpit |
| Documentação API | OpenAPI + Swagger + Scalar |

---

## 🏛️ Arquitetura

```
src/
├── FCG.Notifications.Application/    ← interfaces de serviço, contratos de eventos
├── FCG.Notifications.Infrastructure/ ← consumers MassTransit, serviço SMTP, templates HTML
└── FCG.Notifications.API/            ← Program.cs, configurações
```

> Consulte o repositório [fcg-shared](https://github.com/11NETTG30/fcg-shared) para mais detalhes sobre os pacotes compartilhados e instruções de configuração do NuGet (GitHub Packages).

---

## 🚀 Executar Localmente

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- RabbitMQ acessível (ex: via Docker Compose do [fcg-infra](https://github.com/11NETTG30/fcg-infra))
- Mailpit (ou outro servidor SMTP) para receber os e-mails
- Acesso ao GitHub Packages da organização [11NETTG30](https://github.com/11NETTG30) (veja [fcg-shared](https://github.com/11NETTG30/fcg-shared))

### Configurar appsettings

Ajuste as configurações em `appsettings.Development.json`:

```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "User": "guest",
    "Password": "guest"
  },
  "Smtp": {
    "Host": "localhost",
    "Porta": "1025",
    "Remetente": "noreply@fcg.com"
  }
}
```

### Executar a API

```bash
dotnet run --project src/FCG.Notifications.API
```

---

## ⚙️ CI — GitHub Actions

| Workflow | Gatilho | Descrição |
|---|---|---|
| `docker-publish.yml` | `workflow_dispatch` | Build da imagem Docker e push para o GitHub Container Registry (`ghcr.io/11nettg30/fcg-notifications-api:latest`) |

Para publicar, acesse **Actions → Publicar Imagem Docker → Run workflow**.
