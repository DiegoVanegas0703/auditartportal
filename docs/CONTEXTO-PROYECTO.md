# Auditart — Contexto completo del proyecto (handoff)

Documento para **no perder contexto** al cambiar de máquina o de chat.  
Última actualización: **2026-10-19**.

Repo: https://github.com/DiegoVanegas0703/auditartportal.git  
Workspace típico: `C:\Projects\auditart`  
Demo GCP: VM `auditartdemo`, usuario SSH `dvelopmentcode`, carpeta `~/auditartportal`.

Tras deploy en la VM:

```bash
cd ~/auditartportal && git pull && docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

---

## 1. Qué es Auditart

Portal operativo de **auditorías médicas ART** (Argentina):

- Ingesta de correos (Gmail) → **Triage** → prestación en **Rojo**
- Tablero por colores (máquina de estados)
- Paciente **padre** + varias **prestaciones** hijas
- Cartilla de **doctores/prestadores** (con firma imagen)
- Catálogo de **precios conciliados** (ART / especialista)
- Facturación (verde → celeste pagado)
- Coordinación **tardía** (periódica)
- **Formularios PDF** PMI / PME / PMR (AcroForm, sin aplanar)
- **Informes Word** telemedicina / terreno / consultorio (en Amarillo)
- **WhatsApp** Meta Cloud API (aviso de turno con vista previa)
- **Gemini** híbrido: links a GEMs + chat API con asistentes configurables

Sitio público del cliente: [auditart.com.ar](https://www.auditart.com.ar/)

---

## 2. Stack técnico

| Capa | Tecnología |
|------|------------|
| Front | Vite + React + TypeScript + Tailwind (`apps/web`, puerto **5173**) |
| API | .NET 9 Clean Architecture (`apps/api`, puerto **5070**) |
| DB | PostgreSQL 16 via Docker Compose (host **5433** local; en prod compose = `db`) |
| Auth | Email/contraseña + JWT + refresh (Google OAuth de login **removido**; Client ID Google solo para Gmail) |
| Correo | Gmail API, 2 canales: **General** y **Crónicos** |
| Adjuntos | S3 o disco local (`Aws:S3:UseLocal=true` en demo) |
| PDF | iText7 AcroForm (`IAcroFormFiller`) — **no flatten** (sigue editable) |
| Word | DocumentFormat.OpenXml — plantillas con `{{TOKENS}}` |
| WhatsApp | Meta Cloud API (texto + templates) |
| IA | Gemini API + URLs de GEMs |

### Capas API

- `Auditart.Domain` — entidades / reglas
- `Auditart.Application` — casos de uso
- `Auditart.Infrastructure` — EF Core, Gmail, storage, Meta WhatsApp, PDF filler
- `Auditart.Api` — controllers JWT

Migraciones al arrancar: `DbSeeder.SeedAsync` → `MigrateAsync`.

Plantillas embebidas (CopyToOutput/Publish):

- `apps/api/templates/formularios/` — PMI/PME/PMR `.pdf` + `.fields.json`
- `apps/api/templates/informes/` — `auditoria-*.docx`

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
- **Email del paciente opcional**; **localidad y provincia obligatorias** (junto con domicilio).
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
| Amarillo | Turno coordinado (+ informe Word + auto-aviso WA opcional) |
| Azul | Consulta hecha; esperar autorización |
| Verde | Listo facturación (**exige autorización** al avanzar) |
| Celeste | Pagado / cerrada |

- Amarillo puede ir a Verde si ya hay auth; si no, a Azul.
- Se puede **devolver estado** (p. ej. Amarillo→Rojo, Verde→Azul, Celeste→Verde).
- Notas del caso editables en cualquier momento.
- Autorización (código y/o PDF) se puede cargar **en cualquier momento**.
- “Cuenta pago anticipado” fue reemplazado por **Celeste**.
- Plantillas Word de informe deben ir en ZIP con rutas `/` (no `\`); si falla “Documento Word inválido”, regenerar plantillas.

### Coordinación tardía

- Ya **no** hay menú “Nuevo crónico”.
- Al crear prestación: checkbox **Coordinación tardía** → pide **periodicidad** (semanal / mensual / cada X días).
- Va a cola **Crónicos**; al vencer vuelve a **Rojo**.

### Precios (Rojo)

- Operador elige **Médico auditor** o **Especialista**.
- Catálogo `/precios` (Admin / Jefatura / Facturación).
- Auditor → precios filtrados por ART del caso.
- Especialista → catálogo global; **Total a cobrar ART = valor especialista × 1,5 (+50%) o × 2 (+100%) + 3,5% de ese producto**.
  Ejemplo: 25.000 × 1,5 = 37.500 + 1.312,5 = **38.812,5**.
  Recargo único: `AuditService.RecargoArtEspecialista` (API) y `RECARGO_ART_ESPECIALISTA` (front).
- Alta manual de pacientes (Admin/Jefatura): botón **Nuevo paciente** en el tablero (`POST /api/pacientes`).
- “Presupuesto enviado a ART” se marca/desmarca con clic en el detalle.
- UI: modal “Negociar precios” + detalle en acordeones.

### Doctores / prestadores

- CRUD `/doctores`, import Excel cartilla.
- **No confundir** con el operador del triage: triage asigna un *usuario interno*; el médico es un *prestador* de la cartilla.
- Al pasar a Amarillo hay que **elegir un resultado de la cartilla** (no alcanza con escribir el nombre a mano).
- La búsqueda del turno usa la **misma cartilla** que Doctores (`/api/prestadores/active`, hasta 500 filas, mismos filtros; checkbox “Solo activos”).
- Búsqueda case-insensitive.
- **Firma del médico** (PNG/JPG) se sube en la ficha del prestador; se usa en informes Word.

### Colas

- Incluye **Telemedicina** (etiqueta sin nombre de persona; antes “Telemedicina (Aylen)”).

---

## 5. Formularios PDF (PMI / PME / PMR)

- Botón **Formularios** en detalle de prestación.
- Tipos: `pmi` (ingreso), `pme` (alta), `pmr` (reingreso).
- Prefill con datos paciente/prestación; UI editable; genera PDF AcroForm **sin flatten** (sigue editable en Acrobat).
- Queda como **adjunto** del caso.
- Servicio: `FormularioPdfService` + `IAcroFormFiller`.
- Endpoints: `GET/POST /api/services/{id}/formularios/{tipo}`.

Detalle UI: selects para contingencia, ART en encabezado PMI, etc.

---

## 6. Informes Word (auditoría)

Disponible solo en estado **Amarillo** (`Informe Word` en el detalle).

| Tipo de servicio | Plantilla sugerida |
|------------------|--------------------|
| Telemedicina | `auditoria-telemedicina.docx` |
| Terreno | `auditoria-terreno.docx` (+ internado SI/NO, institución, etc.) |
| Resto (consultorio, etc.) | `auditoria-consultorio.docx` |

Flujo:

1. Prefill (fecha, paciente, DNI, ART, STRO, diagnóstico, caso).
2. Operador/Jefe completa **resumen** (antecedentes / lo que dijo el médico) y conclusión.
3. Se inserta la **firma del prestador** al final (`{{FIRMA}}`).
4. Genera `.docx` adjunto **editable** en Word.

Servicio: `InformeWordService` + `WordDocxPlaceholderFiller`.  
Endpoints: `GET/POST /api/services/{id}/informes/{tipo}`.

Plantillas origen (usuario): `AUDITORIA DE TELEMEDICINA/TERRENO/CONSULTORIO - JULI.docx` → repo en `templates/informes/`.

---

## 7. WhatsApp (turno asignado)

- Feature flag front: `FEATURES.whatsapp` (`apps/web/src/config/features.ts`).
- Backend: `WhatsApp__Enabled` + credenciales Meta (`deploy/.env.prod.example`).
- **Auto-notify** al pasar a **Amarillo** (`WhatsApp__NotifyOnStatuses=Amarillo`).
- Botón **Avisar turno (WhatsApp)** abre modal con **vista previa** del mensaje.
- Texto incluye fecha, dirección, médico, aviso AUDITART vs ART, y aclara que **no se responde** a ese número.
- Cada usuario configura su celular en **Mi cuenta → WhatsApp de contacto** (`User.WhatsAppPhone`).
- El mensaje incluye link `https://wa.me/{e164}` al operador que notificó.
- Envío: `IWhatsAppSender.SendTextAsync` (Meta Graph API).
- Endpoints:  
  - `GET /api/whatsapp/services/{id}/turno-preview`  
  - `POST /api/whatsapp/services/{id}/notify-turno`  
  - `PUT /api/auth/whatsapp-phone`

Nota Meta: mensajes de texto libres pueden fallar fuera de la ventana de 24 h; en producción puede hacer falta plantilla aprobada.

---

## 8. Gemini / asistentes IA

- Config: `Gemini__Enabled`, `Gemini__ApiKey`, `Gemini__Model` (usar `gemini-2.5-flash`; `gemini-2.0-flash` da 404).
- Asistentes en DB (`AiAssistant`): nombre, prompt, URL externa de GEM, flag chat.
- UI: links a GEMs + chat embebido cuando `supportsChat`.
- Servicio: `AiAssistantService`.

---

## 9. Módulos UI y permisos

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
| `/mi-cuenta` | Firma correo + WhatsApp + contraseña | autenticado |
| `/alertas`, `/sla`, `/auditorias` | SLA / reportes | board / reports |

---

## 10. Correos operativos

| Canal | Mailbox | Config keys |
|-------|---------|-------------|
| General | **`info@auditart.com.ar`** | `Gmail:*` / `Gmail:General:*` |
| Crónicos | Confirmar con cliente (típicamente `cronicos@auditart.com.ar`) | `Gmail:Cronicos:*` |

Guía tokens: [`docs/GMAIL-REFRESH-TOKEN.md`](GMAIL-REFRESH-TOKEN.md).  
Despliegue GCP demo: [`docs/DEPLOY-GCP-DEMO.md`](DEPLOY-GCP-DEMO.md).

Secretos **nunca** en Git: User Secrets local o `.env` en el server (`chmod 600`).

---

## 11. Decisiones de producto recientes

1. Paciente padre + N prestaciones; email opcional; localidad/provincia obligatorias.
2. Celeste = cerrado/pagado.
3. Auth no obligatoria Rojo→Amarillo; sí para Verde.
4. Precios duales + catálogo + +50%/+100% especialista + 3,5%.
5. Crónicos = opción tardía en nueva prestación.
6. Formularios PDF PMI/PME/PMR sin aplanar.
7. Informes Word por tipo de auditoría en Amarillo + firma prestador.
8. WhatsApp real Meta: preview de turno + teléfono por usuario + wa.me.
9. Gemini híbrido (GEMs URL + chat API).
10. En HTTP (IP demo sin HTTPS) no usar `crypto.randomUUID` → helper `newClientId()`.

### Pendiente / mejoras futuras

- Plantilla WhatsApp aprobada en Meta (si el texto libre falla fuera de 24 h).
- Import masivo hojas Excel “VALOR PRESTADORES” / por ART (hoy alta manual en `/precios`).
- Storage nativo GCS (demo usa disco local en la VM).
- HTTPS + dominio en demo (hoy suele ser IP HTTP).

---

## 12. Archivos clave

```
apps/web/src/pages/AuditDetailPage.tsx
apps/web/src/pages/MiCuentaPage.tsx
apps/web/src/components/formularios/FormularioPdfModal.tsx
apps/web/src/components/informes/InformeWordModal.tsx
apps/web/src/components/whatsapp/WhatsAppTurnoModal.tsx
apps/web/src/config/features.ts
apps/api/templates/formularios/
apps/api/templates/informes/
apps/api/.../Formularios/FormularioPdfService.cs
apps/api/.../Informes/InformeWordService.cs
apps/api/.../WhatsApp/WhatsAppNotifyService.cs
apps/api/.../WhatsApp/WhatsAppTurnoMessage.cs
apps/api/.../Entities/User.cs                 # WhatsAppPhone
apps/api/.../Entities/Prestador.cs            # Firma*
apps/api/.../Entities/Paciente.cs             # Localidad/Provincia
apps/api/.../Controllers/ServicesController.cs
apps/api/.../Controllers/WhatsAppController.cs
deploy/.env.prod.example
docker-compose.prod.yml
```

Excel operativo de referencia (local del usuario):  
`c:\Users\User\Downloads\AUDITORIAS PARA HACER Y HECHAS.xlsx`

---

## 13. Checklist al cambiar de máquina

1. Clonar repo + `git pull` de `master`.
2. Instalar: Node 22+, .NET 9 SDK, Docker Desktop, Git.
3. `docker compose up -d`
4. Restaurar User Secrets Gmail / WhatsApp / Gemini (o `.env`) — **no están en Git**.
5. `dotnet run` API + `npm run dev` web.
6. Login admin, sync Gmail, smoke: triage → Rojo precios → Amarillo (informe Word + WA preview).
7. Leer [`DEPLOY-GCP-DEMO.md`](DEPLOY-GCP-DEMO.md) antes de la demo con cliente.

### Secretos a respaldar aparte

- `Gmail:ClientId` / `ClientSecret` / `RefreshToken` (General y Crónicos)
- `Jwt:Key`
- `WhatsApp__*` (PhoneNumberId, AccessToken, WebhookVerifyToken)
- `Gemini__ApiKey`
- Contraseñas DB demo / `.env` de producción

---

## 14. Conversación Cursor

Historial largo: transcript  
`agent-transcripts/ca0454b1-ad15-4a4f-aa8a-4df04d91207b`  
(fases Auditart + PDF + WhatsApp + informes Word).

Si abrís un chat nuevo: **adjuntá este archivo** y pedí continuar desde el estado actual (no desde el backlog de sep-2026).
