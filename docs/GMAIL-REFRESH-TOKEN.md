# Actualizar el refresh token de Gmail

Guía para renovar las credenciales OAuth que usa Auditart para leer/enviar correo
con la cuenta de desarrollo (`dvelopmentcode@gmail.com`).

**No subas Client Secret ni Refresh Token a Git ni los pegues en el chat.**

## Cuándo hace falta

- En los logs de la API aparece `invalid_grant`, `Token has been expired or revoked`, o fallos del `GmailPollingWorker`.
- Revocaste el acceso de la app en [https://myaccount.google.com/permissions](https://myaccount.google.com/permissions).
- Cambiaste Client ID / Client Secret en Google Cloud.
- Es la primera vez que conectás Gmail real.

## Prerrequisitos (solo la primera vez)

1. Proyecto en [Google Cloud Console](https://console.cloud.google.com/).
2. **Gmail API** habilitada.
3. Pantalla de consentimiento OAuth (tipo **Externo** en desarrollo) con usuario de prueba `dvelopmentcode@gmail.com`.
4. Cliente OAuth tipo **Aplicación web** con URI de redirección:

   ```text
   https://developers.google.com/oauthplayground
   ```

5. Scope requerido:

   ```text
   https://www.googleapis.com/auth/gmail.modify
   ```

Si el Client ID / Secret ya existen, no hace falta crear otro cliente: solo regenerá el refresh token.

---

## Paso a paso — renovar el refresh token

### 1. Abrir OAuth Playground

1. Entrá a [https://developers.google.com/oauthplayground](https://developers.google.com/oauthplayground).
2. Clic en el engranaje ⚙️ (arriba a la derecha).
3. Activá **Use your own OAuth credentials**.
4. Pegá el **Client ID** y el **Client Secret** del cliente web de Gmail.
5. Access type: **Offline**.
6. Prompt: **Consent** (importante para que Google vuelva a emitir refresh token).
7. Cerrá el panel de configuración.

### 2. Autorizar el scope

1. En **Step 1**, expandí **Gmail API v1**.
2. Marcá:

   ```text
   https://www.googleapis.com/auth/gmail.modify
   ```

3. **Authorize APIs**.
4. Iniciá sesión con `dvelopmentcode@gmail.com` y aceptá los permisos.

### 3. Intercambiar el code por tokens

1. En **Step 2**, clic en **Exchange authorization code for tokens**.
2. Copiá el **Refresh token** (y, si cambió, Client ID / Secret).
3. No compartas esos valores.

### 4. Guardar en User Secrets (local)

En PowerShell:

```powershell
cd C:\Projects\auditart\apps\api\src\Auditart.Api

$id = Read-Host "Client ID"
$secret = Read-Host "Client Secret"
$token = Read-Host "Refresh Token"

dotnet user-secrets set "Gmail:ClientId" $id
dotnet user-secrets set "Gmail:ClientSecret" $secret
dotnet user-secrets set "Gmail:RefreshToken" $token
dotnet user-secrets set "Gmail:Mailbox" "dvelopmentcode@gmail.com"
dotnet user-secrets set "Gmail:Enabled" "true"

Remove-Variable id, secret, token
```

Debés ver cinco mensajes de “Successfully saved…”.

Verificar **solo nombres** (sin valores):

```powershell
dotnet user-secrets list | ForEach-Object {
  if ($_ -match '^(.*?) = ') { "$($matches[1])=CONFIGURADO" } else { $_ }
}
```

Esperado: `Gmail:Enabled`, `Gmail:Mailbox`, `Gmail:ClientId`, `Gmail:ClientSecret`, `Gmail:RefreshToken`.

### 5. (Opcional) Canal Crónicos

Si usás un buzón aparte, las claves van con prefijo de canal:

```powershell
dotnet user-secrets set "Gmail:Cronicos:Enabled" "true"
dotnet user-secrets set "Gmail:Cronicos:Mailbox" "<email-cronicos>"
dotnet user-secrets set "Gmail:Cronicos:ClientId" $id
dotnet user-secrets set "Gmail:Cronicos:ClientSecret" $secret
dotnet user-secrets set "Gmail:Cronicos:RefreshToken" $token
```

Sin `Gmail:Cronicos:RefreshToken`, el canal Crónicos queda deshabilitado.

### Correos corporativos (demo / producción)

Para la demo con el cliente:

| Canal | Mailbox |
|-------|---------|
| General | `info@auditart.com.ar` |
| Crónicos | confirmar (ej. `cronicos@auditart.com.ar`) |

1. Agregá ambos correos como usuarios de prueba en la pantalla de consentimiento OAuth (o usá app Interna en Workspace).
2. Generá **un refresh token por buzón** (Playground → login con cada cuenta → Consent + Offline).
3. General → `Gmail:Mailbox` / `Gmail:RefreshToken` (y opcional `Gmail:General:*`).
4. Crónicos → `Gmail:Cronicos:*` completo.
5. Detalle de despliegue en la nube: [`DEPLOY-GCP-DEMO.md`](DEPLOY-GCP-DEMO.md).

### 6. Reiniciar la API

Detener el proceso que escucha en el puerto **5070** y volver a levantar:

```powershell
cd C:\Projects\auditart\apps\api
dotnet run --project src\Auditart.Api
```

### 7. Probar

- Health: `GET http://localhost:5070/health`
- Sync (logueado como Admin/Jefatura): `POST http://localhost:5070/api/triage/sync-gmail`
- O el botón **Sincronizar Gmail** en Triage.

Si el token es válido, el worker deja de loguear fallos de autenticación y aparecen correos nuevos en triage.

---

## Alternativa: variables de entorno / `.env`

Para entornos que no usen User Secrets, las mismas claves se mapean así:

| User Secrets              | Env / `.env`              |
|---------------------------|---------------------------|
| `Gmail:Enabled`           | `Gmail__Enabled`          |
| `Gmail:Mailbox`           | `Gmail__Mailbox`          |
| `Gmail:ClientId`          | `Gmail__ClientId`         |
| `Gmail:ClientSecret`      | `Gmail__ClientSecret`     |
| `Gmail:RefreshToken`      | `Gmail__RefreshToken`     |
| `Gmail:Cronicos:…`        | `Gmail__Cronicos__…`      |

Plantilla: [`.env.example`](../.env.example). **No commitear** `.env` con secretos reales.

---

## Problemas frecuentes

| Síntoma | Qué hacer |
|---------|-----------|
| Playground no muestra refresh token | Prompt = **Consent**, Access type = **Offline**, y revocar acceso previo de la app en la cuenta Google antes de reautorizar. |
| `invalid_grant` tras guardar | Reiniciar la API; confirmar que el Client ID del Playground es el mismo que en User Secrets. |
| API sigue en stub / no lee mail | `Gmail:Enabled` debe ser `true` y reiniciar. |
| URI mismatch en Google | El redirect URI del cliente OAuth debe ser exactamente `https://developers.google.com/oauthplayground`. |
| App en modo Testing | Solo usuarios de prueba listados en la pantalla de consentimiento pueden autorizar. |
| **Acceso bloqueado: … no completó el proceso de verificación** | Ver sección siguiente (casi siempre: app en **Producción** sin verificar, o falta usuario de prueba). |

---

## Acceso bloqueado / “no completó la verificación de Google”

`gmail.modify` es un scope **restringido**. Google **bloquea** la autorización si la app (`auditartdemo`) está publicada en **Producción** sin haber pasado la verificación formal (lenta y no hace falta para una demo).

### Opción A — Recomendada para demo (app Externa)

1. [Google Cloud Console](https://console.cloud.google.com/) → proyecto **auditartdemo**.
2. **APIs y servicios** → **Pantalla de consentimiento de OAuth**.
3. En **Estado de publicación**, si dice **En producción** → clic en **Volver a prueba** / **Back to Testing**.
4. En **Usuarios de prueba** agregá **exactamente** las cuentas con las que vas a autorizar:
   - `info@auditart.com.ar`
   - el mail de crónicos
   - tu usuario personal si vas a loguearte vos en el Playground
5. Guardá.
6. Volvé a [OAuth Playground](https://developers.google.com/oauthplayground), autorizá con **esa** cuenta (debe estar en la lista).
7. Si aparece “app no verificada”, usá **Avanzado** → **Ir a auditartdemo (no seguro)** — eso solo aplica en Testing + test user.

En Testing podés tener hasta **100** usuarios de prueba; no hace falta verificación de Google.

### Opción B — Si Auditart tiene Google Workspace

En la pantalla de consentimiento, tipo de usuario **Interno**. Solo cuentas `@auditart.com.ar` del mismo Workspace pueden autorizar; **no** hace falta verificación ni lista de testers. (No sirve si `info@` es solo un alias Gmail personal fuera de Workspace.)

### Lo que NO hay que hacer para la demo

- No publicar la app en **Producción**.
- No iniciar el proceso de verificación de Google (semanas + políticas formales).
- No crear otro Client ID “por las dudas” sin corregir primero Testing + usuarios de prueba.

---

## Seguridad

- Los secretos viven en `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` (máquina local).
- No los copies al repositorio, a `appsettings.json` ni a issues/PRs.
- Si un token se filtró, revocalo en Google y generá uno nuevo con esta guía.

## Tags de triage → labels de Gmail

Al agregar o quitar tags en Triage, Auditart sincroniza labels en el hilo de Gmail
(mismo nombre, se crean si no existen). Requiere `Gmail:Enabled=true` y scope
`gmail.modify`. Si Gmail falla, el tag se guarda igual en Auditart (best-effort).
