# Auditart

Monorepo del portal Auditart: frontend (Vite + React + TypeScript) y API (.NET).

## Arquitectura

- **apps/web** - POC Vite + React + TypeScript
- **apps/api** - API .NET 9 (Clean Architecture)
- **docs/** - documentacion del proyecto
- **docs/ARQUITECTURA.md** - arquitectura, capas, endpoints y decisiones de alcance

### Capas de la API

- **Auditart.Domain** - entidades y reglas de dominio
- **Auditart.Application** - casos de uso, validacion (FluentValidation)
- **Auditart.Infrastructure** - EF Core + PostgreSQL (Npgsql)
- **Auditart.Api** - Web API, autenticacion JWT

Referencias: Api -> Application, Infrastructure; Application -> Domain; Infrastructure -> Application, Domain.

Mas detalle: [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md).

## Decisiones de alcance (fases 1-4)

1. **Fases 1-4 en alcance** - entrega incremental del portal y la API segun el plan de fases del producto.
2. **Correo de prueba Gmail** - cuenta de desarrollo: developmentcode@gmail.com.
3. **Login con Google OAuth** - autenticacion de usuarios via Google.
4. **PostgreSQL en RDS** - base de datos gestionada en AWS RDS.
5. **JWT + refresh** - tokens de acceso JWT con refresh tokens para sesiones.
6. **Monorepo** - apps/web (frontend) + apps/api (backend) en un solo repositorio.

## Requisitos

- Node.js 22+
- .NET SDK 9+
- Docker Desktop (PostgreSQL local via Compose)
- Git
- Herramienta global `dotnet-ef` (opcional): `dotnet tool install --global dotnet-ef`

## Desarrollo local

### Base de datos (Docker Compose)

Desde la raiz del repo:

    docker compose up -d

Esto levanta PostgreSQL 16 en el puerto **5433** (`auditart` / `auditart_dev`) para no chocar con un Postgres local en 5432. Variables de ejemplo en `.env.example`.

Para detener:

    docker compose down

### Frontend

    cd apps\web
    npm install
    npm run dev

### API

    cd apps\api
    dotnet run --project src\Auditart.Api

Al arrancar aplica migraciones y seed de usuarios mock.

- Health: `GET http://localhost:5070/health`
- Login Dev: `POST http://localhost:5070/api/auth/google` con `{ "idToken": "dev:developmentcode@gmail.com" }`

Migraciones EF Core:

    cd apps\api
    dotnet ef migrations add <Nombre> --project src\Auditart.Infrastructure --startup-project src\Auditart.Api --output-dir Persistence/Migrations

## Documentacion

- [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) - capas, endpoints REST, triage y stack

## Repositorio

Remote: https://github.com/DiegoVanegas0703/auditartportal.git
