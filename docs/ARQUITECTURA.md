# Arquitectura Auditart — Fases 1-4

## Monorepo

| Ruta | Contenido |
|------|-----------|
| `apps/web` | Portal React (POC → integración API) |
| `apps/api` | API .NET 9 Clean Architecture |
| `docker-compose.yml` | PostgreSQL local |

## Capas API

- **Domain**: entidades, máquina de estados Rojo→Amarillo→Azul→Verde
- **Application**: AuthService, contratos (Gmail, S3, JWT, Google)
- **Infrastructure**: EF Core + Npgsql, JWT, Google ID token, S3 (o disco local en Dev), stub Gmail
- **Api**: controllers REST

## Decisiones confirmadas

- Login: Google OAuth (en Dev: `dev:email@dominio.com`)
- JWT access + refresh rotativo
- Correo de prueba: `developmentcode@gmail.com` (inbox no leídos)
- Adjuntos → S3 (local fallback en Development)
- Triage: jefa elige operador y cola; servicio siempre nace en **Rojo**
- PostgreSQL (local Docker → RDS en AWS)
- Fases 1-4 en alcance del mes

## Endpoints actuales

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/health` | Healthcheck |
| POST | `/api/auth/google` | Login Google / dev token |
| POST | `/api/auth/refresh` | Renovar tokens |
| POST | `/api/auth/logout` | Revocar refresh |
| GET | `/api/auth/me` | Usuario actual |
| GET | `/api/triage/emails` | Emails pendientes |
| POST | `/api/triage/emails/{id}/assign` | Derivar (crea servicio Rojo) |
| GET | `/api/services` | Tablero operativo (filtrado por rol) |
| GET | `/api/services/{id}` | Detalle |
| POST | `/api/services/{id}/transition` | Transición de estado |
| PATCH | `/api/services/{id}/flags` | Flags comerciales / notas |

## Cómo correr local

```bash
# 1. Postgres
docker compose up -d

# 2. API (migra y seed automático al arrancar)
cd apps/api
dotnet run --project src/Auditart.Api
# → http://localhost:5070

# 3. Web
cd apps/web
npm run dev
# → http://localhost:5173
```

Login de desarrollo (sin Google Console aún):

```http
POST http://localhost:5070/api/auth/google
{ "idToken": "dev:developmentcode@gmail.com" }
```

> Postgres Docker usa el puerto host **5433** (evita conflicto con Postgres nativo en 5432).

## Próximos pasos técnicos

1. Credenciales Google OAuth (Client ID) para login real
2. OAuth Gmail / Pub-Sub sobre `developmentcode@gmail.com`
3. Conectar frontend a la API (reemplazar mocks)
4. Worker SLA 24/48hs + WhatsApp Cloud API
5. Módulo facturación / export
6. Deploy AWS (ECS/Fargate, RDS, S3, CloudFront)
