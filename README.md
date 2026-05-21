# Users API — Microsservico de Usuarios (FCG)

Microsservico de **Usuarios** da FIAP Cloud Games (Tech Challenge). Responsavel por registro, autenticacao e gerenciamento de usuarios.

## Finalidade

- **Registro de usuarios** — criar conta com email/senha (BCrypt hashing).
- **Autenticacao** — login com JWT emitido pelo Cognito.
- **Publica `UserCreatedEvent`** via SNS — consumido pelo servico de notificacoes.

## Tecnologias / Dependencias

| Recurso | Local (dev) | AWS (producao) |
|---------|------------|----------------|
| Runtime | .NET 10 / C# | .NET 10 / C# |
| Banco | PostgreSQL 16 | RDS PostgreSQL 16 |
| Auth | JWT local (dev) | Cognito User Pool |
| Mensageria (pub) | SNS (localstack opcional) | SNS |
| Logs | Console / arquivo | CloudWatch Logs |
| Metricas | `/metrics` (Prometheus) | `/metrics` (Prometheus) |
| API docs | Scalar | Scalar |

Pacotes NuGet principais: `AWSSDK.SimpleNotificationService`, `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `prometheus-net.AspNetCore`, `Serilog.AspNetCore`, `Scalar.AspNetCore`, `ErrorOr`.

## Como rodar localmente

```bash
# 1. Subir PostgreSQL
cd ../postech-orchestration/docker
docker compose up -d postgresql

# 2. Rodar a API
cd ../../postech-users-api/src/Postech.Users.Api
dotnet run
```

API disponivel em **http://localhost:5159**. Abra no navegador:

- `http://localhost:5159/health` — health check
- `http://localhost:5159/scalar/v1` — documentacao e testes
- `http://localhost:5159/metrics` — metricas Prometheus

## Variaveis de ambiente

| Variavel | Descricao | Default (local) |
|----------|-----------|-----------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL | `Host=localhost;Port=5432;Database=postech_users;Username=postgres;Password=postgres` |
| `JwtSettings__SecretKey` | Chave JWT (dev) | — |
| `JwtSettings__Issuer` | Emissor do token | `PostechAuthServer` |
| `JwtSettings__Audience` | Audiencia do token | `PostechUsersAPI` |
| `JwtSettings__ExpirationMinutes` | Expiracao (minutos) | `60` |
| `CognitoSettings__UserPoolId` | Cognito User Pool ID (prod) | — |
| `CognitoSettings__ClientId` | Cognito App Client ID (prod) | — |
| `CognitoSettings__Region` | Regiao Cognito | `us-east-1` |
| `AWS__Region` | Regiao AWS | `us-east-1` |
| `AWS__ServiceURL` | LocalStack (opcional) | — |
| `AWS__SnsTopicArn` | ARN do topico SNS para UserCreatedEvent | — |

## Endpoints

| Metodo | Rota | Autenticacao | Descricao |
|--------|------|-------------|-----------|
| `GET` | `/health` | — | Health check |
| `GET` | `/health/alive` | — | Liveness probe |
| `GET` | `/metrics` | — | Metricas Prometheus |
| `POST` | `/auth/register` | — | Registrar novo usuario |
| `POST` | `/auth/login` | — | Login (retorna JWT) |
| `GET` | `/users/me` | JWT | Dados do usuario autenticado |

## Eventos

- **Publica:** `UserCreatedEvent` (UserId, Email, Name) via SNS apos registro bem-sucedido.

## Estrutura do projeto

```
src/Postech.Users.Api/
  Application/            # DTOs, Services (UserService, CognitoAuthService)
  Domain/                 # Entities (User), Enums (UserRole), Errors
  Endpoints/              # Minimal API endpoints (Auth, Users, Health)
  Extensions/             # DI registration, auth pipeline
  Infrastructure/
    Data/                 # UsersDbContext (EF Core / Postgres)
    Messaging/            # SnsEventPublisher
    Repositories/         # IUserRepository, UserRepository
  Middleware/             # CorrelationIdMiddleware
  Migrations/             # EF Core migrations
```

## Como atualizar imagem no ECR

```bash
ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
ECR="${ACCOUNT}.dkr.ecr.us-east-1.amazonaws.com/tf-postech-postech-users-api"

aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin "${ACCOUNT}.dkr.ecr.us-east-1.amazonaws.com"

docker build -t "${ECR}:latest" -f Dockerfile .
docker push "${ECR}:latest"
```
