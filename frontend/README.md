# ChambaPoint Frontend

UI mobile-first (390px) en HTML5 + CSS3 + JS vanilla ES6+. Consume la API ASP.NET Core en `http://localhost:5020`.

## Requisitos

- Backend corriendo en `:5020` (CORS `AllowAnyOrigin`)
- Servidor estático para `frontend/`

## Levantar

```bash
# desde frontend/
python -m http.server 5173
# abrir http://127.0.0.1:5173
```

## Config

`js/config.js`:

- `API_BASE` → `http://localhost:5020`
- `HUB_URL` → `http://localhost:5020/hubs/notifications` (SignalR)
- Tokens en `localStorage`: `cp_token`, `cp_user`

## Pantallas

| Archivo | Rol | Descripción |
|---------|-----|-------------|
| `index.html` | - | Splash / home |
| `create-profile.html` | - | Onboarding de rol |
| `login.html` / `register.html` | - | Auth JWT |
| `explore.html` | Customer | Buscar técnicos, chips, mapa |
| `worker.html` | Customer | Perfil, reseñas, modal solicitud |
| `requests.html` | Ambos | Activas / historial + citas |
| `conversations.html` | Ambos | Lista de chats |
| `chat.html?with={userId}` | Ambos | Mensajes 1 a 1 en vivo |
| `dashboard.html` | Worker | Ingresos, gráfico, citas, actividad |

## Usuarios demo

Password: `chamba2026`

| Email | Rol |
|-------|-----|
| `ana.cli@example.com` | Customer |
| `carlos.elec2@example.com` | Worker (Electricidad) |
| `maria.gas@example.com` | Worker (Plomería) |
| `lucia.pint@example.com` | Worker (Pintura) |
| `pedro.elec@example.com` | Worker (Electricidad) |
| `jorge.carp@example.com` | Worker (Carpintería) |

## Endpoints usados

- Auth: `POST /api/auth/login`, `POST /api/auth/register`, `GET /api/auth/me`
- Trabajadores: `GET /api/workers`, `GET /api/workers/{id}`
- Reseñas: `GET|POST /api/workers/{id}/reviews`
- Solicitudes: `GET /api/requests`, `PATCH /api/requests/{id}/status`, `POST /api/requests`
- Citas: `GET|POST /api/appointments`, `PATCH /api/appointments/{id}/status`
- Chat: `GET /api/messages`, `POST /api/messages`, `POST /api/messages/read`
- Dashboard: `GET /api/dashboard/worker`
- SignalR: `JoinUserGroup(userId)` → eventos `newMessage`, `messagesRead`, `newRequest`, `requestStatusChanged`, `newAppointment`, `appointmentStatusChanged`

## Estructura

```
frontend/
├── *.html
├── css/
│   ├── tokens.css base.css components.css
│   └── screens/{auth,explore,requests,chat,dashboard}.css
└── js/
    ├── config.js api.js auth.js ui.js components.js hub.js
    └── screens/*.js
```

## Capturas

`docs/screenshots/` — flujo completo splash → dashboard.
