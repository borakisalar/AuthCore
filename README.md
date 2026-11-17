# AuthCore

An ASP.NET Core (.NET 8) Web API for authentication and user management. It provides JWT authentication with refresh tokens, role-based authorization, multi-factor authentication (TOTP), email verification, session management, and sample sales endpoints backed by AdventureWorks with Redis caching.

## Features

- **Authentication**: registration, login, JWT access tokens, refresh tokens
- **Account management**: email verification, change email, forgot/reset/change password
- **MFA**: TOTP setup (via email code), verify, disable, recovery code
- **Sessions**: list, revoke a single session, and log out
- **Role-based access**: `Admin`, `Manager` and other roles, with user–role assignment
- **User administration**: list, create, delete, activate/deactivate, and filter by role
- **Sales data**: AdventureWorks queries served from the database or a Redis cache
- **Infrastructure**: global exception handling middleware, request/response logging, error log table, Serilog (console + file), Swagger UI

## Tech stack

| Area | Technology |
|------|-----------|
| Framework | ASP.NET Core, .NET 8 |
| Database | SQL Server, EF Core 8 (migrations) |
| Cache | Redis |
| Auth | JWT Bearer, BCrypt, GoogleAuthenticator (TOTP) |
| Email | MailKit (SMTP) |
| Mapping | AutoMapper |
| Logging | Serilog |
| Docs | Swagger (Swashbuckle) |
| Tests | `AuthCore.Tests` |
| CI/CD | GitHub Actions, Docker |

## Project structure

```
AuthCore/            API project
  Controllers/       Auth, Users, Roles, Sales
  Implements/        Service implementations
  Interfaces/        Service contracts
  Middlewares/       Exception handling, request/response logging
  Migrations/        EF Core migrations
  Mappings/          AutoMapper profiles
  Helpers/           Data seeding
AuthCore.Tests/      Unit tests (controllers, services, middleware)
docker-compose.yml   SQL Server, Redis and API
```

## Getting started

### Prerequisites

- .NET 8 SDK
- SQL Server (the AdventureWorks2022 database is needed for the sales endpoints)
- Redis
- An SMTP account (Gmail by default) for verification and MFA emails

### Configuration

Default settings are in `AuthCore/appsettings.json`. Store secrets with user secrets rather than committing them:

```bash
cd AuthCore
dotnet user-secrets set "Jwt:Key" "<a-long-random-secret>"
dotnet user-secrets set "EmailSettings:SenderEmail" "<your-email>"
dotnet user-secrets set "EmailSettings:SenderPassword" "<app-password>"
```

Check these settings:

- `ConnectionStrings:UsersContext`: the users database
- `ConnectionStrings:AdventureWorksConnection`: the AdventureWorks database
- `Redis:Configuration`: the Redis endpoint, for example `localhost:6379`

### Run locally

```bash
dotnet ef database update --project AuthCore
dotnet run --project AuthCore
```

Swagger UI is available in development at `/swagger`.

### Run with Docker

Create a `.env` file next to `docker-compose.yml`:

```
DB_PASSWORD=<strong-sa-password>
EMAIL=<sender-email>
EMAIL_PASSWORD=<app-password>
```

Then run:

```bash
docker compose up -d
```

The API is exposed on port `80`, SQL Server on `1433` and Redis on `6379`.

### Run the tests

```bash
dotnet test
```

## API overview

### Auth (`/api/auth`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/register` | – | Register a new user |
| POST | `/login` | – | Log in and receive tokens |
| POST | `/refresh-token` | – | Refresh the access token |
| GET | `/verify-email` | – | Verify an email address |
| POST | `/forgot-password` | – | Request a password reset |
| POST | `/reset-password` | – | Reset the password |
| POST | `/change-password` | Yes | Change the password |
| POST | `/change-email` | Yes | Request an email change |
| GET | `/verify-email-change` | – | Confirm an email change |
| GET | `/session` | Yes | Current session |
| DELETE | `/session` | Yes | Log out |
| GET | `/sessions` | Yes | List active sessions |
| DELETE | `/sessions/{id}` | Yes | Revoke a session |
| POST | `/mfa/send-setup-code` | Yes | Send an MFA setup code |
| POST | `/mfa/setup` | Yes | Set up MFA |
| POST | `/mfa/verify` | Yes | Verify an MFA code |
| POST | `/mfa/disable` | Yes | Disable MFA |
| POST | `/mfa/recovery-code` | Yes | Use an MFA recovery code |

### Users (`/api/users`)

| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| GET | `/` | Admin, Manager | List users |
| GET | `/{id}` | – | Get a user |
| POST | `/` | Admin | Create a user |
| DELETE | `/{id}` | Admin | Delete a user |
| PUT | `/{id}/activate`, `/{id}/deactivate` | – | Activate or deactivate a user |
| GET | `/recent` | – | Recently added users |
| GET | `/my-info` | Authenticated | Current user's info |
| POST | `/{id}/roles` | Admin | Assign a role |
| DELETE | `/{id}/roles/{roleId}` | Admin | Remove a role |
| GET | `/by-role/{roleName}` | Admin, Manager | Users in a role |

### Roles (`/api/roles`)

| Method | Endpoint | Access | Description |
|--------|----------|--------|-------------|
| GET | `/` | Admin | List roles |

### Sales (`/api/sales`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/database` | Sales data read from the database |
| GET | `/cache` | Sales data served through Redis |

Authenticated endpoints expect an `Authorization: Bearer <token>` header. See `AuthCore/AuthCore.http` for request examples.

## Deployment

Pushing to `master` triggers the GitHub Actions workflow in `.github/workflows/deploy.yml`. It builds the Docker image and deploys it.
