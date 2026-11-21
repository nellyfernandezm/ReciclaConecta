# ReciclaConecta ♻

Plataforma web que conecta a quienes generan residuos reciclables (hogares y negocios) con los **puntos de acopio** y con los **recolectores**.

- El **ciudadano** encuentra en un mapa el punto de acopio más cercano, qué materiales recibe y si está abierto ahora. Si no puede llevar el material, **publica una solicitud de recojo**.
- El **recolector** (muchas veces trabajador informal) ve las solicitudes **pendientes cerca de él**, acepta una, pasa a recogerla y registra los kg reales.
- El **administrador** verifica a los recolectores, mantiene los puntos de acopio y ve los **indicadores de impacto** (kg reciclados por material, solicitudes por estado).

## Stack

| Capa | Tecnología |
|---|---|
| Frontend | HTML + CSS + JavaScript (sin framework), mapa con Leaflet 1.9.4 y OpenStreetMap |
| Backend / API | ASP.NET Core 8 (Minimal APIs) |
| Base de datos | SQLite con Entity Framework Core 8 |
| Autenticación | JWT Bearer con roles (Ciudadano, Recolector, Admin); contraseñas con `PasswordHasher` (PBKDF2) |
| Pruebas | xUnit + `WebApplicationFactory` (unitarias e integración) |
| Contenedor | Docker multi-stage (imagen `aspnet:8.0`, usuario no root) |
| CI/CD | GitHub Actions: build, pruebas, Gitleaks, SCA de NuGet, CodeQL, Trivy, deploy a Render |
| Nube | Render (Web Service Docker, plan Free) |

## Estructura

```
src/ReciclaConecta.Api/
  Domain/        Entidades y reglas de negocio (estados, distancia, horarios)
  Data/          DbContext de EF Core y datos de demostración
  Endpoints/     API: auth, puntos, solicitudes, admin
  Security/      JWT y cabeceras de seguridad
  wwwroot/       Frontend (index, login, registro, ciudadano, recolector, admin)
tests/ReciclaConecta.Tests/
  ReglasTests.cs Pruebas unitarias de las reglas de negocio
  ApiTests.cs    Pruebas de integración HTTP contra la API
.github/workflows/ci-cd.yml   Pipeline DevSecOps
Dockerfile
```

## Ejecutar en local

Requisitos: .NET SDK 8.

```bash
dotnet run --project src/ReciclaConecta.Api
# abrir la URL que muestra la consola (p. ej. http://localhost:5xxx)
```

La base SQLite se crea sola en `src/ReciclaConecta.Api/db/` con datos de demostración (10 puntos de acopio en Lima, usuarios y solicitudes).

Con Docker:

```bash
docker build -t reciclaconecta .
docker run -p 8080:8080 -e PORT=8080 reciclaconecta
# http://localhost:8080
```

Pruebas:

```bash
dotnet test
```

### Cuentas de demostración

Contraseña de demostración para todas: `Demo2026!`

| Rol | Correo |
|---|---|
| Ciudadano | ciudadano@reciclaconecta.pe |
| Recolector (verificado) | recolector@reciclaconecta.pe |
| Recolector (sin verificar) | recolector2@reciclaconecta.pe |
| Administrador | admin@reciclaconecta.pe |

## API principal (`/api`)

| Método | Ruta | Rol | HU |
|---|---|---|---|
| POST | /auth/registro | público | HU01 |
| POST | /auth/login | público (10 intentos/min por IP) | HU02 |
| GET | /usuarios · PUT /usuarios/{id}/verificar · PUT /usuarios/{id}/activo | Admin | HU03 |
| GET | /materiales | público | — |
| GET | /puntos?material=&lat=&lng=&abiertoAhora= | público | HU04–HU06 |
| POST/PUT/DELETE | /puntos | Admin | HU07 |
| POST | /solicitudes | Ciudadano | HU08 |
| GET | /solicitudes/disponibles?lat=&lng=&radioKm=&material= | Recolector | HU09 |
| POST | /solicitudes/{id}/aceptar | Recolector | HU10 |
| POST | /solicitudes/{id}/recoger | Recolector | HU11 |
| POST | /solicitudes/{id}/cancelar · GET /solicitudes/mias | Ciudadano | HU12 |
| GET | /impacto/mio | Ciudadano | HU13 |
| POST | /solicitudes/{id}/calificar | Ciudadano | HU14 |
| GET | /admin/indicadores | Admin | HU15 |
| GET | /health | público | — |

### Reglas de negocio

- Estados: `Pendiente → Aceptada → Recogida`, o `Pendiente → Cancelada`.
- Solo un recolector **activo y verificado** acepta; si dos aceptan a la vez, el segundo recibe **409** (control de concurrencia con el estado como token).
- Solo el recolector asignado marca el recojo (kg reales entre 0 y 1000).
- Cantidad estimada entre 0.5 y 1000 kg; máximo 5 solicitudes pendientes por ciudadano.
- Calificación de 1 a 5, una sola vez, solo si fue recogida.
- “Abierto ahora” se calcula con la hora de Lima (UTC−5).

## Seguridad en la aplicación

- JWT firmado (HS256) con clave desde la variable de entorno `Jwt__Key` (nunca en el repositorio).
- Autorización por rol en cada endpoint; respuestas 401/403.
- Contraseñas con hash PBKDF2 (`PasswordHasher`), política mínima (8 caracteres, letras y números); mismo mensaje si falla el correo o la clave.
- Rate limiting en el login (fuerza bruta).
- Cabeceras: Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, HSTS en producción.
- Frontend sin `innerHTML` con datos del usuario (evita XSS); Leaflet cargado con Subresource Integrity.
- Validación de datos de entrada y errores sin detalles internos.

## Estrategia de ramas

- `main`: producción. Solo recibe cambios por **Pull Request** desde `test`, con el pipeline en verde. Cada push a `main` despliega en Render.
- `test`: integración y pruebas. Aquí se suben los commits de cada historia (`HUxx: …`) y el pipeline corre build, pruebas y seguridad.

## Pipeline CI/CD (`.github/workflows/ci-cd.yml`)

| Job | Qué hace | Falla si… |
|---|---|---|
| Build y pruebas | `dotnet build` + `dotnet test` (unitarias e integración), resumen y cobertura en el *Summary*, artefacto con los `.trx` | alguna prueba falla |
| Secretos (Gitleaks) | revisa todo el historial de git buscando claves, tokens y contraseñas | encuentra un secreto |
| Dependencias (SCA) | `dotnet list package --vulnerable --include-transitive` | hay un paquete High o Critical |
| SAST (CodeQL) | análisis estático de C# y JavaScript (consultas `security-extended`), resultados en *Security → Code scanning* | — (reporta alertas) |
| Imagen Docker + Trivy | construye la imagen, la escanea y prueba `/health` y `/api/puntos` en el contenedor | vulnerabilidad HIGH/CRITICAL con parche |
| Despliegue en Render | solo en push a `main` y con todo lo anterior en verde: llama al deploy hook y verifica `/health` en la URL pública | la app no responde |

Además, **Dependabot** (`.github/dependabot.yml`) propone actualizaciones semanales de NuGet, Actions y Docker hacia `test`.

## Despliegue en Render

1. En Render: **New → Web Service** → conectar este repositorio (o “Public Git repository”).
2. Runtime **Docker**, rama `main`, plan **Free**, Auto-Deploy **Off** (el despliegue lo dispara el pipeline).
3. Variables de entorno: `Jwt__Key` = una cadena aleatoria de 64 caracteres (Render → *Generate*).
4. Copiar el **Deploy Hook** (Settings → Deploy Hook).
5. En GitHub → Settings → Secrets and variables → Actions:
   - Secreto `RENDER_DEPLOY_HOOK` = la URL del deploy hook.
   - Variable `APP_URL` = la URL pública (p. ej. `https://reciclaconecta.onrender.com`).

> En el plan Free el servicio “duerme” tras 15 minutos sin uso (la primera visita tarda unos segundos) y el disco no es persistente: al redesplegar, la base SQLite vuelve a los datos de demostración.
