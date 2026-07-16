# Auditart Portal — POC Frontend

Prueba de concepto del portal de gestión de auditorías médicas para **AUDITART SAS**.

## Inicio rápido

```bash
npm install
npm run dev
```

Abrir http://localhost:5173

## Usuarios simulados

| Usuario | Rol | Acceso |
|---------|-----|--------|
| **Diego Santamarina** (default) | Admin | Todo |
| María González | Jefatura | Triage + Tablero |
| Pablo / Laura / Martín | Operadores | Tablero general |
| Aylen Martínez | Telemedicina | Cola telemedicina |
| Carolina Ruiz | Crónicos | Cola crónicos |
| Damián López | Facturación | Módulo verde |

## Módulos

- **Login** — Selección de usuario con sesión en `sessionStorage`
- **Dashboard** — KPIs por color de estado
- **Bandeja Triage** — Emails simulados → derivación a colas
- **Tablero Operativo** — Grilla con código de colores (reemplazo Excel)
- **Facturación** — Consolidación de registros verdes
- **Detalle de servicio** — Transiciones de estado y checklist

## Paleta (alineada a auditart.com.ar)

- Azul primario: `#5D92B1`
- Navy: `#2C3E50`
- Verde accent: `#0DB26B`
- Estados: rojo / amarillo / azul / verde

## Stack

React 19 · TypeScript · Vite · Tailwind CSS v4 · React Router · Lucide Icons
