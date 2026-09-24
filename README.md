# ChambaPoint

Tu conexión con oficios locales. Marketplace de servicios (electricistas, gasfiteros, carpinteros, pintores).

**Stack:** ASP.NET Core 10 + Entity Framework Core + SQLite + SignalR + RabbitMQ (CloudAMQP) + Redis Cloud. Frontend HTML/CSS/JS vanilla.

## Endpoints

### Auth
| Metodo | Ruta | Auth | Body | Respuestas |
|--------|------|------|------|------------|
| POST | /api/auth/register | Publico | {name, email, password, role?, avatarUrl?} | 201, 400, 409 |
| POST | /api/auth/login | Publico | {email, password} | 200, 400, 401 |
| GET | /api/auth/me | Bearer | - | 200, 401 |
| POST | /api/auth/logout | Bearer | - | 200, 401 |

### Trabajadores
| Metodo | Ruta | Auth | Body/Query | Respuestas |
|--------|------|------|------------|------------|
| GET | /api/workers | Publico | ?q=&profession= | 200 |
| GET | /api/workers/{id} | Publico | - | 200, 404 |
| POST | /api/workers | Bearer Worker | {profession, experienceYears, distanceKm, jobsCount, about, certifications[], gallery[]} | 201, 400, 403, 409 |
| PUT | /api/workers | Bearer Worker | igual que POST | 200, 400, 403, 404 |
| DELETE | /api/workers | Bearer Worker | - | 200, 403, 404 |

### Reseñas
| Metodo | Ruta | Auth | Body/Query | Respuestas |
|--------|------|------|------------|------------|
| POST | /api/workers/{id}/reviews | Bearer Customer | {rating (1-5), text, photos[]} | 201, 400, 401, 403, 404 |
| GET | /api/workers/{id}/reviews | Publico | ?page=1&pageSize=10 | 200 |

### Solicitudes (Requests)
| Metodo | Ruta | Auth | Body/Query | Respuestas |
|--------|------|------|------------|------------|
| POST | /api/requests | Bearer Customer | {workerId?, category, description, photos[], urgency, scheduledAt?} | 201, 400, 401 |
| GET | /api/requests | Bearer | ?status=activas\|historial&page=1&pageSize=10 | 200, 401 |
| GET | /api/requests/{id} | Bearer | - | 200, 401, 403, 404 |
| PATCH | /api/requests/{id} | Bearer Worker | {status: "Aceptada"\|"Rechazada"\|"Completada"} | 200, 400, 401, 403, 404 |
| PATCH | /api/requests/{id}/status | Bearer Worker | {status: "Aceptada"\|"Rechazada"\|"Completada"} | 200, 400, 401, 403, 404 |

### Mensajes (Chat 1 a 1)
| Metodo | Ruta | Auth | Body/Query | Respuestas |
|--------|------|------|------------|------------|
| POST | /api/messages | Bearer | {receiverId, requestId?, text} | 201, 400, 401, 404 |
| GET | /api/messages | Bearer | ?withUserId={id}&page=1&pageSize=50 | 200, 401 |
| POST | /api/messages/read | Bearer | {withUserId} | 200, 401 |

### WebSocket / Tiempo real
| Ruta | Tipo | Descripcion |
|------|------|-------------|
| /hubs/notifications | SignalR | Notificaciones en vivo. Eventos: `newReview`, `newRequest`, `requestStatusChanged`, `newMessage`, `messagesRead`. Unirse al grupo `user:{userId}`. |

## Ejemplos rapidos (PowerShell)

```powershell
# Registrar
$b = @{ name="Pedro"; email="pedro@test.com"; password="chamba2026"; role="Customer" } | ConvertTo-Json
$r = Invoke-RestMethod -Uri http://localhost:5099/api/auth/register -Method Post -Body $b -ContentType application/json
$token = $r.token

# Login
$b = @{ email="pedro@test.com"; password="chamba2026" } | ConvertTo-Json
$r = Invoke-RestMethod -Uri http://localhost:5099/api/auth/login -Method Post -Body $b -ContentType application/json
$token = $r.token

# Buscar trabajadores
Invoke-RestMethod -Uri "http://localhost:5099/api/workers?q=electricista"

# Crear reseña (Customer)
$b = @{ rating=5; text="Excelente"; photos=@() } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5099/api/workers/1/reviews -Method Post -Body $b -ContentType "application/json" -Headers @{Authorization="Bearer $token"}
```

## Configuracion de servicios cloud

Copia `appsettings.Development.json` y pega tus credenciales (NUNCA subir ese archivo a git):

- **Redis Cloud** → `ConnectionStrings:Redis` (formato `rediss://default:PASSWORD@HOST:6379`)
- **CloudAMQP (RabbitMQ)** → `RabbitMQ:Uri` (formato `amqps://USER:PASS@HOST/vhost`)
- **PieSocket** → `PieSocket:ApiKey`, `PieSocket:Channel`

Si no se configuran, el API funciona con fallback local (cache InMemory, log en consola).

## Despliegue en Render

1. Subir repo a GitHub.
2. En Render: New → Blueprint → seleccionar el repo.
3. Detecta `render.yaml` automaticamente.
4. Pegar credenciales en Environment del servicio.
5. Deploy.

## Estructura

```
ChambaPoint.Api/
├── Controllers/    AuthController, WorkersController, ReviewsController
├── Models/         User, Worker, Review, Roles
├── Services/       TokenService, CacheService, WorkerService, ReviewService, RequestNotifier
├── Hubs/           NotificationsHub (SignalR)
├── Data/           AppDbContext, Migrations/
└── Program.cs      Configuracion de servicios
```

## Reglas del equipo

- Nunca push a `main`. Todo en rama `feat/*` + Pull Request.
- Cada PR: descripcion de lo que hace, tabla de endpoints, como probar.
- Credenciales solo en `appsettings.Development.json` local (git ignorado).
