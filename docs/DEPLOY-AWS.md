# Despliegue AWS de bajo costo — Auditart

Guía para poner Auditart en producción **esta semana**, priorizando **costo mínimo** y velocidad de entrega. Más adelante se puede migrar a ECS/Fargate + ALB sin reescribir la app.

---

## 1. Arquitectura recomendada (bajo costo)

### Qué NO hacer esta semana (caro / lento)
- ECS Fargate + Application Load Balancer (~USD 20–40/mes solo de ALB)
- RDS Multi-AZ / instancias grandes
- NAT Gateway (USD ~32/mes)
- Varias cuentas/regiones

### Qué SÍ hacer (staging / piloto)

```text
Internet
   │
   ▼
Cloudflare (gratis) ── HTTPS + DNS
   │
   ▼
EC2 t4g.small (o Lightsail 2 GB)   ← UNA sola máquina
   ├── Docker: nginx (web estático React)
   ├── Docker: API .NET 9
   └── (opcional) Postgres en la misma máquina  ← solo si querés ahorrar al máximo
        Ó
Amazon RDS PostgreSQL db.t4g.micro Single-AZ   ← recomendado si hay datos reales

S3 (adjuntos) + IAM Role en la EC2 (sin Access Keys en disco)
Secrets: archivo .env en la EC2 (chmod 600) o SSM Parameter Store
```

| Pieza | Elección | Estimación mensual* |
|-------|----------|---------------------|
| Compute | EC2 **t4g.small** (2 vCPU ARM, 2 GB) o Lightsail 2 GB | ~USD 12–15 |
| Disco | 30 GB gp3 | ~USD 2–3 |
| Base | **RDS db.t4g.micro** Single-AZ, 20 GB | ~USD 13–18 |
| Base alternativa | Postgres en Docker en la misma EC2 | ~USD 0 extra |
| S3 | Un bucket, lifecycle a Infrequent Access a 30 días | ~USD 1–3 |
| DNS/HTTPS | Cloudflare Free + Let's Encrypt (Caddy/nginx) | USD 0 |
| Transfer | Free tier / pocos GB | bajo |
| **Total típico** | EC2 + RDS + S3 | **~USD 30–40/mes** |
| **Total ultra-barato** | Solo EC2 + Postgres local + S3 | **~USD 15–20/mes** |

\*Precios orientativos `us-east-1` / `sa-east-1` suele ser un poco más caro. Revisá la calculadora AWS.

**Recomendación para esta semana:**  
- **Piloto interno / demo:** ultra-barato (Postgres en la misma EC2).  
- **Clientes reales / datos sensibles:** RDS `db.t4g.micro` + backups automáticos.

Región sugerida:
- Equipo / usuarios en Argentina/LatAm → `sa-east-1` (São Paulo) por latencia.
- Si el presupuesto manda → `us-east-1` (más barato y más cuota free tier).

---

## 2. Checklist previo (1–2 horas)

1. Cuenta AWS con acceso de administrador (o rol con EC2, RDS, S3, IAM).
2. Dominio (ej. `app.auditart.com`) o subdominio temporal.
3. Cuenta Cloudflare (gratis) apuntando el DNS al dominio (opcional pero muy recomendado).
4. Google Cloud OAuth:
   - Client ID de login web (ya tenés uno en `appsettings`).
   - Agregar orígenes autorizados: `https://app.tudominio.com`
   - Redirect URIs si aplica.
5. Gmail API: refresh tokens de los **correos corporativos** (ver `docs/GMAIL-REFRESH-TOKEN.md`).
6. En la máquina local: Docker Desktop, AWS CLI v2, Git.

---

## 3. Paso a paso — AWS

### Paso 3.1 — Crear bucket S3

1. Consola AWS → **S3** → Create bucket.
2. Nombre: `auditart-docs-prod` (debe ser globalmente único).
3. Región: la misma que la EC2.
4. **Block all public access**: ON (la API lee/escribe con IAM).
5. Versioning: opcional (cuesta un poco más).
6. Lifecycle rule sugerida:
   - Tras 30 días → Standard-IA
   - Tras 90 días → Glacier Instant Retrieval (si los adjuntos casi no se reabren)

### Paso 3.2 — Rol IAM para la EC2 (sin Access Keys)

1. IAM → Roles → Create role → **AWS service** → EC2.
2. Nombre: `auditart-ec2-role`.
3. Políticas:
   - Custom policy mínima S3:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::auditart-docs-prod/*"
    },
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": "arn:aws:s3:::auditart-docs-prod"
    }
  ]
}
```

4. (Opcional) `AmazonSSMManagedInstanceCore` para entrar por Session Manager sin abrir SSH.

### Paso 3.3 — Security Group

Crear SG `auditart-sg`:

| Tipo | Puerto | Origen | Nota |
|------|--------|--------|------|
| SSH | 22 | Tu IP `/32` | Solo si usás SSH |
| HTTP | 80 | `0.0.0.0/0` | Cloudflare o Let's Encrypt |
| HTTPS | 443 | `0.0.0.0/0` | Público |
| Postgres | 5432 | **solo** el SG de la EC2 | Si usás RDS |

No abras 5070 a Internet: nginx hace de reverse proxy.

### Paso 3.4 — EC2 (o Lightsail)

**EC2**
1. Launch instance.
2. AMI: **Ubuntu 24.04 LTS** (o Amazon Linux 2023).
3. Tipo: **t4g.small** (ARM/Graviton = más barato). Si preferís x86: `t3.small`.
4. Key pair: crear/descargar `.pem`.
5. Network: VPC default OK; asociá `auditart-sg`.
6. Storage: **30 GB gp3**.
7. Advanced → IAM instance profile: `auditart-ec2-role`.
8. Elastic IP: asigná una y asociála (IP fija para DNS).

**Lightsail (alternativa más simple)**
- Plan 2 GB RAM / 1 vCPU, Ubuntu.
- Abrí puertos 80/443 en el firewall de Lightsail.
- Menos flexible con IAM Role: ahí sí conviene Access Keys solo para S3 (o usuario IAM mínimo).

### Paso 3.5 — RDS PostgreSQL (recomendado si hay datos reales)

1. RDS → Create database → **PostgreSQL 16**.
2. Templates: **Free tier** si aplica, si no **Dev/Test**.
3. Instance: **db.t4g.micro**.
4. Storage: 20 GB gp3, **no** autoscaling agresivo.
5. **Multi-AZ: No**.
6. VPC: misma que la EC2.
7. Public access: **No**.
8. SG: solo tráfico desde `auditart-sg` al puerto 5432.
9. Master user: `auditart` / password fuerte (guardalo en un password manager).
10. DB name: `auditart`.
11. Backup retention: 7 días (suficiente y barato).

Anotá el endpoint: `auditart.xxxxx.rds.amazonaws.com`.

### Paso 3.6 — DNS

En Cloudflare (o Route53):
- `A` / `CNAME` `app.tudominio.com` → Elastic IP / Lightsail IP.
- Proxy Cloudflare (nube naranja) ON: te da HTTPS gratis y DDoS básico.
- Si usás Cloudflare Full (strict), necesitás certificado en origen (Let's Encrypt abajo).

---

## 4. Preparar la máquina (SSH)

```bash
ssh -i auditart.pem ubuntu@TU_ELASTIC_IP
```

### 4.1 Instalar Docker

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
# cerrá sesión y volvé a entrar
docker --version
docker compose version
```

En **t4g (ARM)** las imágenes deben ser `linux/arm64`. Los Dockerfiles del repo usan multi-arch (`dotnet` y `node` oficiales lo soportan).

### 4.2 Clonar el repo

```bash
cd /opt
sudo mkdir -p auditart && sudo chown ubuntu:ubuntu auditart
cd /opt/auditart
git clone https://github.com/DiegoVanegas0703/auditartportal.git .
# o subí un release/tag estable
```

---

## 5. Archivos de despliegue en el repo

En el monorepo tenés (o vas a usar):

| Archivo | Uso |
|---------|-----|
| `apps/api/Dockerfile` | Imagen de la API |
| `apps/web/Dockerfile` | Build React + nginx |
| `deploy/nginx.conf` | Reverse proxy `/` → web, `/api` y `/health` → API |
| `docker-compose.prod.yml` | Orquesta web + api (+ postgres opcional) |
| `deploy/.env.prod.example` | Plantilla de secretos |

### 5.1 Crear `.env` en el servidor (NUNCA lo subas a Git)

```bash
cd /opt/auditart
cp deploy/.env.prod.example .env
nano .env   # completar
chmod 600 .env
```

Variables críticas:

```bash
ASPNETCORE_ENVIRONMENT=Production

# Postgres (RDS)
ConnectionStrings__Default=Host=AUDITART.XXXX.rds.amazonaws.com;Port=5432;Database=auditart;Username=auditart;Password=***;SSL Mode=Require;Trust Server Certificate=true

# JWT (≥ 32 caracteres aleatorios)
Jwt__Key=PEGA_AQUI_UN_SECRETO_LARGO_Y_ALEATORIO
Jwt__Issuer=auditart
Jwt__Audience=auditart-web

# Google login
Google__ClientId=TU_CLIENT_ID.apps.googleusercontent.com

# CORS (tu dominio HTTPS)
Cors__Origins__0=https://app.tudominio.com

# S3 (sin keys si usás IAM Role)
Aws__Region=us-east-1
Aws__S3__Bucket=auditart-docs-prod
Aws__S3__UseLocal=false

# Gmail corporativo — canal general
Gmail__Enabled=true
Gmail__PollingEnabled=true
Gmail__Mailbox=operaciones@tuempresa.com
Gmail__ClientId=...
Gmail__ClientSecret=...
Gmail__RefreshToken=...

# Canal crónicos (si aplica)
Gmail__Cronicos__Enabled=true
Gmail__Cronicos__Mailbox=cronicos@tuempresa.com
Gmail__Cronicos__ClientId=...
Gmail__Cronicos__ClientSecret=...
Gmail__Cronicos__RefreshToken=...
```

En .NET, `__` = anidamiento de config (`Gmail:ClientId` → `Gmail__ClientId`).

### 5.2 Frontend: URL de la API

El web usa `VITE_API_URL`. En producción **misma origen** (nginx proxy), conviene:

```bash
# build-arg en docker-compose.prod.yml
VITE_API_URL=
# vacío o relativo: el browser llama /api/... al mismo host
```

Si preferís API en subdominio `api.tudominio.com`, entonces `VITE_API_URL=https://api.tudominio.com` y CORS debe incluir el origen del web.

---

## 6. Build y arranque

```bash
cd /opt/auditart
docker compose -f docker-compose.prod.yml --env-file .env build
docker compose -f docker-compose.prod.yml --env-file .env up -d
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
```

La API al arrancar:
1. Aplica migraciones EF (`MigrateAsync`).
2. Hace seed de usuarios (password inicial `Test` si estánían).
3. Arranca workers (Gmail polling, outbox, SLA, crónicos).

### 6.1 HTTPS con Caddy (simple) o Cloudflare

**Opción A — Cloudflare Flexible/Full:**  
Cloudflare termina HTTPS; origen en HTTP:80. Rápido para esta semana.

**Opción B — Let's Encrypt en la máquina (mejor):**  
Usá el servicio `caddy` del compose (si está habilitado) o certbot delante de nginx.

Verificá:

```bash
curl -s http://127.0.0.1/health
curl -s https://app.tudominio.com/health
```

---

## 7. Post-despliegue (checklist funcional)

1. Abrir `https://app.tudominio.com` → login.
2. Login email/password seed o Google OAuth.
3. **Cambiar contraseñas** de admin/jefatura/operadores.
4. Menú **Doctores**: confirmar listado / reimportar Excel si la DB es nueva.
5. Triage → **Sincronizar Gmail** (Admin/Jefatura).
6. Abrir un caso → enviar/responder mail de prueba.
7. Subir un adjunto y verificar que aparece en S3 (`aws s3 ls s3://auditart-docs-prod/ --recursive | head`).
8. Transición Rojo → Amarillo con selector de profesional.

### 7.1 Google OAuth en producción

Consola Google Cloud → Credenciales → tu Client ID web:
- Authorized JavaScript origins: `https://app.tudominio.com`
- Authorized redirect URIs: las que use el frontend (si hay)

### 7.2 Gmail OAuth

Los refresh tokens deben haberse generado **logueado con las cuentas corporativas**.  
Si ves `invalid_grant` en logs: renovar según `docs/GMAIL-REFRESH-TOKEN.md` y actualizar `.env` + `docker compose up -d`.

---

## 8. Actualizar (deploy de un cambio)

```bash
cd /opt/auditart
git pull
docker compose -f docker-compose.prod.yml --env-file .env build
docker compose -f docker-compose.prod.yml --env-file .env up -d
```

Migraciones: automáticas al iniciar la API. Hacé backup de RDS antes de releases grandes:

```bash
# snapshot manual RDS desde consola, o:
aws rds create-db-snapshot --db-instance-identifier auditart --db-snapshot-identifier auditart-$(date +%Y%m%d)
```

---

## 9. Costos: hábitos para no disparar la factura

1. **Una sola EC2**; no ALB ni NAT.
2. RDS **micro + Single-AZ**; apagá entornos de prueba de noche si no hacen falta (`aws ec2 stop-instances` / schedule).
3. S3 sin acceso público; lifecycle a IA.
4. CloudWatch: dejá logs básicos; evitá retención eterna (7–14 días).
5. Elastic IP: si apagás la EC2 sin EIP, perdés la IP (DNS se rompe).
6. Activá **AWS Budgets** alerta a USD 30 y USD 50.
7. Revisá Cost Explorer el día 3 y el día 7.

Apagar de noche (opcional, si el piloto no es 24/7):

```bash
# cron en otra máquina o EventBridge
aws ec2 stop-instances --instance-ids i-xxxxx
aws ec2 start-instances --instance-ids i-xxxxx
```

(Si RDS está separado, también se puede parar temporalmente en Dev/Test; en prod real mejor dejarlo.)

---

## 10. Plan de la semana (calendario)

| Día | Tarea |
|-----|--------|
| Lun | Cuenta AWS, S3, IAM role, Security Group, EC2 + Elastic IP |
| Mar | RDS (o Postgres Docker), DNS Cloudflare, Docker en EC2, `.env` |
| Mié | Primer `compose up`, HTTPS, login, seed users, cambio de passwords |
| Jue | Gmail corporativo, sync triage, prueba de envío/adjuntos S3 |
| Vie | Doctores + flujo Rojo→Amarillo, backup snapshot, Budget alert, handoff |

---

## 11. Evolución futura (cuando el costo lo permita)

```text
CloudFront + S3 (web)
     +
API en ECS Fargate (1 task) detrás de ALB
     +
RDS (mismo)
     +
Secrets Manager
```

Eso suma ~USD 40–80/mes pero mejora HA y deploys. **No lo necesitás esta semana.**

---

## 12. Troubleshooting rápido

| Síntoma | Qué mirar |
|---------|-----------|
| `connection refused` Postgres | SG RDS, password, SSL Mode, hostname |
| CORS error en browser | `Cors__Origins__0` exacto con `https://` |
| 502 nginx → API | `docker compose logs api`, puerto interno 8080 |
| Adjuntos fallan | IAM role, bucket name, `Aws__S3__UseLocal=false` |
| Gmail no importa | `Gmail__Enabled`, refresh token, logs `GmailPollingWorker` |
| EC2 sin memoria | t4g.small mínimo; `docker stats`; no corras build pesado y API a la vez |

```bash
docker compose -f docker-compose.prod.yml logs --tail=200 api
docker stats
df -h
free -h
```

---

## 13. Seguridad mínima (obligatoria antes de usuarios reales)

1. Cambiar todas las passwords seed.
2. `Jwt__Key` único y largo.
3. SSH solo tu IP; ideal Session Manager.
4. No subir `.env` a Git.
5. Backups RDS 7 días + snapshot pre-release.
6. Cloudflare WAF free / rate limit básico si hay abuso.

---

Si querés el siguiente paso operativo en el repo: ya deberían existir `Dockerfile`s + `docker-compose.prod.yml` + `deploy/.env.prod.example` para copiar/pegar en la EC2. Si algo falta en tu clone, pedime “generar archivos de deploy” y los dejamos listos.
