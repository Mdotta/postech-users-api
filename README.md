# Postech Users API

A .NET-based microservice for user management with authentication, authorization, and event-driven architecture.

## Technologies Used

- **.NET 10.0** - Latest .NET framework
- **C#** - Programming language
- **PostgreSQL** - Database (via Entity Framework Core)
- **Entity Framework Core 10.0** - ORM for database access
- **JWT (JSON Web Tokens)** - Authentication mechanism
- **BCrypt.Net** - Password hashing
- **RabbitMQ** - Message broker (via MassTransit)
- **Serilog** - Structured logging
- **ErrorOr** - Functional error handling
- **Scalar** - OpenAPI/Swagger documentation
- **xUnit** - Testing framework
- **FluentAssertions** - Test assertions library

## Features

- User registration and authentication
- JWT-based authorization
- Role-based access control (User/Administrator)
- Password hashing with BCrypt
- Event publishing to RabbitMQ
- RESTful API endpoints
- OpenAPI/Swagger documentation
- Correlation ID middleware for request tracking
- Structured logging with Serilog

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [RabbitMQ](https://www.rabbitmq.com/download.html) (optional, for event publishing)
- [Docker](https://www.docker.com/get-started) (optional, for containerization)

## Configuration

The application requires the following configuration in `appsettings.json` or environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=usersdb;Username=postgres;Password=yourpassword"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-min-32-chars",
    "Issuer": "Postech.API",
    "Audience": "Postech.Client",
    "ExpirationMinutes": 60
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  }
}
```

## How to Build

1. **Clone the repository**
   ```bash
   cd src/postech.Users.Api
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

   Or build in Release mode:
   ```bash
   dotnet build -c Release
   ```

## How to Run

1. **Update configuration**
   - Edit `postech.Users.Api/appsettings.json` with your database and RabbitMQ settings
   - Or use `appsettings.Development.json` for development settings

2. **Apply database migrations**
   ```bash
   cd postech.Users.Api
   dotnet ef database update
   ```

3. **Run the application**
   ```bash
   dotnet run --project postech.Users.Api
   ```

   The API will be available at:
   - HTTP: `http://localhost:5159`
   - HTTPS: `https://localhost:7246`
   - Scalar: `http://localhost:5159/scalar/v1` (or configured port)

## How to Test

1. **Run all tests**
   ```bash
   dotnet test
   ```

2. **Run tests with detailed output**
   ```bash
   dotnet test --verbosity normal
   ```

3. **Run tests with code coverage**
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

## How to Create Docker Image

1. **Build Docker image**
   ```bash
   docker build -t postech-users-api:latest -f Dockerfile .
   ```

2. **Run Docker container**
   ```bash
   docker run -d -p 8080:8080 -p 8081:8081 \
     -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=usersdb;Username=postgres;Password=yourpassword" \
     -e JwtSettings__SecretKey="your-secret-key-min-32-chars" \
     -e RabbitMQ__Host="host.docker.internal" \
     --name postech-users-api \
     postech-users-api:latest
   ```

3. **Verify container is running**
   ```bash
   docker ps
   ```

4. **View logs**
   ```bash
   docker logs postech-users-api
   ```

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login and get JWT token

### Users
- `GET /api/users/{id}` - Get user by ID (requires authentication)

## Project Structure

```
postech.Users.Api/
├── Application/
│   ├── Constants/
│   ├── DTOs/
│   ├── Events/
│   ├── Services/
│   └── Validations/
├── Domain/
│   ├── Authorization/
│   ├── Entities/
│   ├── Enums/
│   └── Errors/
├── Endpoints/
├── Extensions/
├── Infrastructure/
│   ├── Data/
│   ├── Messaging/
│   └── Repositories/
├── Middleware/
├── Migrations/
└── Program.cs
```

## Development

### Adding a new migration
```bash
cd postech.Users.Api
dotnet ef migrations add MigrationName
```

### Reverting a migration
```bash
dotnet ef migrations remove
```

### Running with watch mode (hot reload)
```bash
dotnet watch run --project postech.Users.Api
```