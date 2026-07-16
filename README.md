# Auditart 
 
Monorepo del portal Auditart: frontend (Vite + React + TypeScript) y API (.NET). 
 
## Arquitectura 
 
- **apps/web** - POC Vite + React + TypeScript 
- **apps/api** - API .NET 9 (Clean Architecture) 
- **docs/** - documentacion del proyecto 
 
### Capas de la API 
 
- **Auditart.Domain** - entidades y reglas de dominio 
- **Auditart.Application** - casos de uso, validacion (FluentValidation) 
- **Auditart.Infrastructure** - EF Core + PostgreSQL (Npgsql) 
- **Auditart.Api** - Web API, autenticacion JWT 
 
Referencias: Api -> Application, Infrastructure; Application -> Domain; Infrastructure -> Application, Domain. 
 
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
- Git 
 
## Desarrollo local 
 
### Frontend 
 
    cd apps\web 
    npm install 
    npm run dev 
 
### API 
 
    cd apps\api 
    dotnet run --project src\Auditart.Api 
 
## Repositorio 
 
Remote: https://github.com/DiegoVanegas0703/auditartportal.git 
 
