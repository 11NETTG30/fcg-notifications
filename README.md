# FCG Notifications API

Microsserviço responsável por notificar o usuário por e-mail (simulado via log).

## Eventos consumidos
- `UserCreatedEvent` → e-mail de boas-vindas
- `PaymentProcessedEvent` (Approved) → e-mail de confirmação de compra

## Variáveis de ambiente
| Variável | Descrição |
|---|---|
| `RabbitMQ__Host` | Host do RabbitMQ |
| `RabbitMQ__User` | Usuário do RabbitMQ |
| `RabbitMQ__Password` | Senha do RabbitMQ |

## Executar localmente
\`\`\`bash
docker-compose up
\`\`\`

## Deploy no Kubernetes
\`\`\`bash
kubectl apply -f k8s/
\`\`\`