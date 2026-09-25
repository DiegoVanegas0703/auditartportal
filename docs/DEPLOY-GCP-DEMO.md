# Demo Auditart en Google Cloud (créditos gratis)

Guía práctica para quien **no tiene experiencia en GCP**: desplegar portal + API + DB para una **demo con el cliente**, usando el **trial / créditos gratis** de Google Cloud, y conectar los correos reales.

Documento hermano: [`CONTEXTO-PROYECTO.md`](CONTEXTO-PROYECTO.md).

---

## 0. Qué vamos a armar (simple)

Para demo, **no** hace falta Kubernetes ni 10 servicios.

```text
Internet
   │
   ▼
VM en Compute Engine (1 máquina)
   ├── Docker: PostgreSQL
   ├── Docker: API .NET
   ├── Docker: Front (nginx)
   └── Disco local para adjuntos (Aws:S3:UseLocal=true)
```

| Pieza | Servicio GCP | ¿Por qué así? |
|-------|--------------|---------------|
| Compute | **Compute Engine** VM | Una sola caja Docker = menos complejidad |
| DNS/HTTPS | Dominio + Cloudflare Free o IP pública + Caddy | Barato / gratis |
| Correo | **Gmail API** (mismo proyecto GCP) | El cliente ya vive en Google |
| Secretos | Archivo `.env` en la VM (chmod 600) o Secret Manager | Demo: `.env` alcanza |
| Storage | Disco de la VM | Evita migrar a GCS ya |

**Créditos:** al crear una cuenta nueva de Google Cloud suele haber trial (~USD 300 / 90 días; confirmar en consola). La VM pequeña + poco tráfico cabe holgado en demo.

> Tip: activá **alertas de presupuesto** (Billing → Budgets) a USD 10–20 para no llevarte sorpresas.

---

## 1. Orden recomendado (no saltear)

1. Cuenta GCP + proyecto + billing con créditos.
2. Conectar Gmail **local** primero (`info@` + crónicos) y probar sync.
3. Subir código a Git (sin secretos).
4. Crear VM + Docker + compose prod.
5. Poner HTTPS + CORS + JWT.
6. Demo con el cliente.

---

## 2. Crear el proyecto en Google Cloud

1. Entrá a [console.cloud.google.com](https://console.cloud.google.com/).
2. Creá un proyecto: `auditart-demo`.
3. Vinculá billing / activá el trial.
4. Anotá el **Project ID**.

Habilitá APIs (APIs y servicios → Biblioteca):

- **Compute Engine API**
- **Gmail API**
- (Opcional más adelante) Cloud SQL, Secret Manager, Cloud Storage

---

## 3. Correos reales — General y Crónicos

### Mailboxes

| Canal | Email |
|-------|-------|
| General / operación normal | **`info@auditart.com.ar`** |
| Crónicos | Confirmar con Auditart (ej. `cronicos@auditart.com.ar`) |

Ambos deben poder autorizar OAuth en el mismo proyecto GCP (Workspace: agregar como usuarios de prueba o app interna).

### 3.1 Cliente OAuth (una sola app, dos refresh tokens)

1. APIs → **Pantalla de consentimiento OAuth**
   - Tipo: Interno (si es Workspace de Auditart) o Externo + Testing
   - Scopes: `https://www.googleapis.com/auth/gmail.modify`
2. Credenciales → **ID de cliente OAuth** → Aplicación web  
   Redirect URI:

   ```text
   https://developers.google.com/oauthplayground
   ```

3. Generá **un refresh token por buzón** con OAuth Playground (logueándote cada vez con ese correo).  
   Paso a paso detallado: [`GMAIL-REFRESH-TOKEN.md`](GMAIL-REFRESH-TOKEN.md).

### 3.2 Guardar en local (probar antes de la nube)

```powershell
cd C:\Projects\auditart\apps\api\src\Auditart.Api

# Canal General = info@
dotnet user-secrets set "Gmail:Enabled" "true"
dotnet user-secrets set "Gmail:Mailbox" "info@auditart.com.ar"
dotnet user-secrets set "Gmail:General:Mailbox" "info@auditart.com.ar"
dotnet user-secrets set "Gmail:General:Enabled" "true"
dotnet user-secrets set "Gmail:ClientId" "<CLIENT_ID>"
dotnet user-secrets set "Gmail:ClientSecret" "<CLIENT_SECRET>"
dotnet user-secrets set "Gmail:RefreshToken" "<REFRESH_INFO>"

# Canal Crónicos
dotnet user-secrets set "Gmail:Cronicos:Enabled" "true"
dotnet user-secrets set "Gmail:Cronicos:Mailbox" "<EMAIL_CRONICOS>"
dotnet user-secrets set "Gmail:Cronicos:ClientId" "<CLIENT_ID>"
dotnet user-secrets set "Gmail:Cronicos:ClientSecret" "<CLIENT_SECRET>"
dotnet user-secrets set "Gmail:Cronicos:RefreshToken" "<REFRESH_CRONICOS>"
```

Reiniciá API → Triage → **Sincronizar Gmail**.  
Si falla `invalid_grant`, regenerá el refresh token de ese buzón.

---

## 4. Crear la VM (Compute Engine)

### 4.1 Máquina sugerida (demo)

- Región: `southamerica-east1` (São Paulo) o `us-central1` (más barata / freebies)
- Tipo: **e2-small** (2 GB) o **e2-medium** (si el build Docker se queda corto)
- Disco: 30–40 GB SSD
- SO: **Ubuntu 22.04 LTS**
- Firewall: permitir **HTTP (80)** y **HTTPS (443)**; SSH (22) solo desde tu IP

### 4.2 En la VM (primera vez)

```bash
sudo apt update && sudo apt install -y docker.io docker-compose-v2 git
sudo usermod -aG docker $USER
# cerrar sesión SSH y volver a entrar

git clone https://github.com/DiegoVanegas0703/auditartportal.git
cd auditartportal
```

### 4.3 Archivo `.env` en el server (NO subir a Git)

Basado en `deploy/.env.prod.example` si existe, o crear:

```bash
# .env en la raíz del repo en la VM
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=db;Port=5432;Database=auditart;Username=auditart;Password=ELIGI_UNA_CLAVE_FUERTE
Jwt__Key=GENERAR_CLAVE_LARGA_RANDOM_MIN_32_CHARS
Jwt__Issuer=auditart
Jwt__Audience=auditart-web
Cors__Origins__0=https://demo.tudominio.com

Gmail__Enabled=true
Gmail__Mailbox=info@auditart.com.ar
Gmail__General__Enabled=true
Gmail__General__Mailbox=info@auditart.com.ar
Gmail__ClientId=...
Gmail__ClientSecret=...
Gmail__RefreshToken=...   # token de info@

Gmail__Cronicos__Enabled=true
Gmail__Cronicos__Mailbox=cronicos@...
Gmail__Cronicos__ClientId=...
Gmail__Cronicos__ClientSecret=...
Gmail__Cronicos__RefreshToken=...

Aws__S3__UseLocal=true
Aws__S3__Bucket=auditart-local
POSTGRES_PASSWORD=ELIGI_UNA_CLAVE_FUERTE
```

Permisos:

```bash
chmod 600 .env
```

### 4.4 Ajustar Compose para demo “todo en una VM”

En `docker-compose.prod.yml` **descomentá** el servicio `db` y hacé que `api` dependa de él (está documentado en el propio archivo).  
Si el front necesita proxy a la API, el Dockerfile de `apps/web` ya suele enrutar `/api` vía nginx — verificá al build.

Levantar:

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker compose -f docker-compose.prod.yml ps
curl -s http://127.0.0.1:8080/health   # o el puerto que exponga el proxy
```

### 4.5 HTTPS (recomendado para demo)

Opción fácil: **Cloudflare** delante (proxy naranja) + origen HTTP en la VM, o instalar **Caddy** en la VM como reverse proxy con Let’s Encrypt.

Agregá el origen HTTPS en:

- `Cors__Origins`
- Google OAuth (si volvés a usar login Google más adelante)
- Redirects que apliquen

---

## 5. Checklist de smoke test (antes de llamar al cliente)

| # | Prueba | OK? |
|---|--------|-----|
| 1 | Abrir URL pública del front | |
| 2 | Login `admin@auditart.local` / contraseña cambiada | |
| 3 | Sync Gmail trae mails de **info@** | |
| 4 | Canal crónicos sincroniza (si hay mail) | |
| 5 | Derivar → prestación Rojo + paciente | |
| 6 | Negociar precios (+50% / +100%) | |
| 7 | Cargar autorización | |
| 8 | Nueva prestación con tardía | |
| 9 | Editar datos paciente | |
| 10 | Adjuntar PDF | |

---

## 6. Costos y cómo no gastar de más

- Apagá la VM cuando no haya demos: Compute Engine → Stop.
- No abras Postgres (5432) a `0.0.0.0`.
- No uses Cloud SQL + Cloud Run + Load Balancer juntos **para esta primera demo** (más caro y más complejo).
- Configurá **Budget alert**.
- Al terminar el trial: exportá dump de Postgres si querés conservar datos.

```bash
docker exec -t <container_postgres> pg_dump -U auditart auditart > auditart-demo.sql
```

---

## 7. Alternativas GCP (más “cloud native”, después)

Cuando la demo cierre y haya presupuesto estable:

| Servicio | Uso |
|----------|-----|
| Cloud Run | API (y opcionalmente front) |
| Cloud SQL PostgreSQL | DB gestionada |
| Cloud Storage | Adjuntos (habría que adaptar storage de S3 → GCS) |
| Secret Manager | Tokens Gmail / JWT |
| Identity-Aware Proxy | Restringir quién entra a la demo |

Para **esta** demo, la VM única es la opción correcta.

---

## 8. Si algo falla

| Síntoma | Qué mirar |
|---------|-----------|
| Front carga pero API 401/CORS | `Cors__Origins` y URL exacta (con/sin www) |
| Gmail `invalid_grant` | Refresh token de ese mailbox; Prompt=Consent en Playground |
| Solo un canal sincroniza | `Gmail:Cronicos:Enabled` + RefreshToken propios |
| Build OOM en la VM | Subir a e2-medium o buildear imágenes en tu PC y `docker push` a Artifact Registry |
| Adjuntos no guardan | `Aws__S3__UseLocal=true` y volumen/permisos en contenedor API |

---

## 9. Próximo paso concreto (vos + yo en el otro chat)

1. Confirmar email exacto de **crónicos**.
2. Generar tokens OAuth de `info@` y crónicos.
3. Probar sync en local.
4. Crear VM GCP + `.env` + `docker compose up`.
5. Ensayo de 30 min con el cliente.
