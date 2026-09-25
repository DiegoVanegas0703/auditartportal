# Auditart — Contexto completo del proyecto (handoff)

Documento para **no perder contexto** al cambiar de máquina o de chat.  
Última actualización: **2026-09-22**.

Repo: https://github.com/DiegoVanegas0703/auditartportal.git  
Workspace típico: `C:\Projects\auditart`

---

## 1. Qué es Auditart

Portal operativo de **auditorías médicas ART** (Argentina):

- Ingesta de correos (Gmail) → **Triage** → prestación en **Rojo**
- Tablero por colores (máquina de estados)
- Paciente **padre** + varias **prestaciones** hijas
- Cartilla de **doctores/prestadores**
- Catálogo de **precios conciliados** (ART / especialista)
- Facturación (verde → celeste pagado)
- Coordinación **tardía** (periódica; antes “Nuevo crónico” como menú aparte)

Sitio público del cliente: [auditart.com.ar](https://www.auditart.com.ar/)

---

## 2. Stack técnico

| Capa | Tecnología |
|------|------------|
| Front | Vite + React + TypeScript + Tailwind (`apps/web`, puerto **5173**) |
| API | .NET 9 Clean Architecture (`apps/api`, puerto **5070**) |
| DB | PostgreSQL 16 via Docker Compose (host **5433**) |
| Auth | Email/contraseña + JWT + refresh (Google OAuth de login **removido**; queda Client ID Google para Gmail) |
| Correo | Gmail API, 2 canales: **General** y **Crónicos** |
| Adjuntos | S3 o disco local (`Aws:S3:UseLocal=true` en demo) |

### Capas API

- `Auditart.Domain` — entidades / reglas
- `Auditart.Application` — casos de uso
- `Auditart.Infrastructure` — EF Core, Gmail, storage
- `Auditart.Api` — controllers JWT

Migraciones al arrancar: `DbSeeder.SeedAsync` → `MigrateAsync`.

---

## 3. Cómo levantar en local

```powershell
# 1) Postgres
cd C:\Projects\auditart
docker compose up -d

# 2) API
cd apps\api
dotnet run --project src\Auditart.Api --urls http://localhost:5070

# 3) Front
cd apps\web
npm install
npm run dev -- --host 127.0.0.1 --port 5173
```

- Health: `GET http://localhost:5070/health`
- Front: `http://127.0.0.1:5173`
- Login seed: `admin@auditart.local` / `Test` (pide cambio de contraseña la 1ª vez)

Otras cuentas seed (todas `Test`):  
`jefatura@`, `operador@`, `telemedicina@`, `cronicos@`, `facturacion@` + `@auditart.local`

---

## 4. Modelo de negocio implementado

### Paciente + prestaciones

- Identidad paciente: **nombre + DNI** (normalizados).
- Cada fila del tablero = **una prestación**.
- Tablero agrupado por paciente; filtro “Abiertas” oculta **Celeste**.
- Mail de triage crea 1ª prestación y liga/crea paciente.
- Ficha `/pacientes/:id`: editar datos del paciente + **Nueva prestación**.
- Edición de paciente se propaga a prestaciones **abiertas** (no Celeste).

### Estados (por prestación)

```
Rojo → Amarillo → Azul | Verde → Celeste
```

| Estado | Significado |
|--------|-------------|
| Rojo | Buscar profesional / negociar precios |
| Amarillo | Turno coordinado |
| Azul | Consulta hecha; esperar autorización |
| Verde | Listo facturación (**exige autorización**) |
| Celeste | Pagado / cerrada |

- Amarillo puede ir a Verde si ya hay auth; si no, a Azul.
- Autorización (código y/o PDF) se puede cargar **en cualquier momento**.
- “Cuenta pago anticipado” fue reemplazado por **Celeste**.

### Coordinación tardía

- Ya **no** hay menú “Nuevo crónico”.
- Al crear prestación: checkbox **Coordinación tardía** → pide **periodicidad** (semanal / mensual / cada X días).
- Va a cola **Crónicos**; al vencer vuelve a **Rojo**.

### Precios (Rojo)

- Operador elige **Médico auditor** o **Especialista**.
- Catálogo `/precios` (Admin / Jefatura / Facturación).
- Auditor → precios filtrados por ART del caso.
- Especialista → catálogo global; cotización ART = base **+50%** (×1,5) o **+100%** (×2).  
  Ejemplo: base 100 → 150 (+50%) o 200 (+100%).
- UI: modal “Negociar precios” + detalle en acordeones (menos scroll).

### Doctores

- CRUD `/doctores`, import Excel cartilla.
- Al pasar a Amarillo se puede elegir prestador (copia valor consulta + flag anticipado).

---

## 5. Módulos UI y permisos

| Ruta | Módulo | Permiso |
|------|--------|---------|
| `/` | Dashboard | cualquiera autenticado |
| `/triage` | Bandeja triage | triage (Admin/Jefatura) |
| `/tablero` | Tablero operativo | operationalBoard |
| `/servicio/:id` | Detalle prestación | board |
| `/pacientes/:id` | Ficha paciente | board |
| `/doctores` | Prestadores | reports |
| `/precios` | Catálogo precios | precios (Admin/Jefatura/Facturación) |
| `/facturacion` | Facturación | billing |
| `/usuarios` | Usuarios | manageUsers |
| `/alertas`, `/sla`, `/auditorias` | SLA / reportes | board / reports |

---

## 6. Correos operativos (a conectar para demo)

| Canal | Mailbox | Config keys |
|-------|---------|-------------|
| General | **`info@auditart.com.ar`** | `Gmail:*` / `Gmail:General:*` |
| Crónicos | Confirmar con cliente (típicamente `cronicos@auditart.com.ar`) | `Gmail:Cronicos:*` |

Guía tokens: [`docs/GMAIL-REFRESH-TOKEN.md`](GMAIL-REFRESH-TOKEN.md).  
Despliegue GCP demo: [`docs/DEPLOY-GCP-DEMO.md`](DEPLOY-GCP-DEMO.md).

Secretos **nunca** en Git: User Secrets local o Secret Manager / `.env` en el server.

---

## 7. Decisiones de producto recientes (sep 2026)

1. Paciente padre + N prestaciones.
2. Celeste = cerrado/pagado.
3. Auth no obligatoria Rojo→Amarillo; sí para Verde; botón siempre visible.
4. Precios duales + módulo catálogo + +50%/+100% especialista.
5. Crónicos = opción tardía en nueva prestación (sin menú aparte).
6. Datos paciente editables en ficha.
7. Detalle: sticky actions + modal precios + acordeones.

### Pendiente / no implementado

- PME / PMI / PMR (PDFs editables) — fase 5.4.
- Gemini / Gems — fase 5.5.
- Import masivo hojas Excel “VALOR PRESTADORES” / por ART (hoy alta manual en `/precios`).
- WhatsApp real (hoy simulado).
- Storage nativo GCS (demo puede usar disco local en la VM).
- Commit/push de todos los cambios locales recientes (verificar `git status` antes de migrar de PC).

---

## 8. Archivos clave

```
apps/web/src/pages/AuditDetailPage.tsx   # detalle + modal precios + acordeones
apps/web/src/pages/PacientePage.tsx      # editar paciente + nueva prestación / tardía
apps/web/src/pages/PreciosPage.tsx       # catálogo
apps/web/src/pages/OperationalBoardPage.tsx
apps/api/.../Entities/AuditService.cs
apps/api/.../Entities/Paciente.cs
apps/api/.../Entities/PrecioCatalogo.cs
apps/api/.../Precios/PrecioCatalogoService.cs
apps/api/.../Controllers/ServicesController.cs
apps/api/.../Controllers/PacientesController.cs
apps/api/.../Gmail/GmailChannelRegistry.cs
docker-compose.yml                       # Postgres :5433
docker-compose.prod.yml                  # api + web nginx
```

Excel operativo de referencia (local del usuario):  
`c:\Users\User\Downloads\AUDITORIAS PARA HACER Y HECHAS.xlsx`

---

## 9. Checklist al cambiar de máquina

1. Clonar repo + `git status` / pull de la rama de trabajo.
2. Instalar: Node 22+, .NET 9 SDK, Docker Desktop, Git.
3. `docker compose up -d`
4. Restaurar User Secrets Gmail (o `.env`) — **no están en Git**.
5. `dotnet run` API + `npm run dev` web.
6. Login admin, sync Gmail, smoke test triage → prestación → precios.
7. Leer [`DEPLOY-GCP-DEMO.md`](DEPLOY-GCP-DEMO.md) antes de la demo con cliente.

### Secretos a respaldar aparte (USB cifrado / 1Password / etc.)

- `Gmail:ClientId` / `ClientSecret` / `RefreshToken` (General y Crónicos)
- `Jwt:Key`
- Contraseñas DB demo
- Cualquier `.env` de producción

---

## 10. Conversación Cursor

Historial largo de este trabajo: transcript  
`agent-transcripts/ca0454b1-ad15-4a4f-aa8a-4df04d91207b`  
(título orientativo: fases Auditart + paciente/precios).

Si abrís un chat nuevo en otra PC: **adjuntá este archivo** y pedí continuar desde `DEPLOY-GCP-DEMO.md` + conexión de `info@auditart.com.ar`.
