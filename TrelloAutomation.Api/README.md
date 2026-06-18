# TrelloAutomation.Api

Mini backend en .NET 8 para recibir planes de proyecto en JSON y sincronizarlos con Trello creando listas, tarjetas, checklists y etiquetas. Está pensado para conectar un GPT personalizado con un tablero Trello sin guardar secretos en el repositorio.

## Requisitos

- .NET SDK 8 o superior.
- Una API Key y Token de Trello.
- El ID largo o `shortLink` del tablero Trello.
- Git.

## Crear o preparar el repositorio

Si GitHub CLI está disponible y autenticado:

```bash
gh repo create trello-chatgpt-automation-api --private --source . --remote origin --push
```

Si prefieres hacerlo manualmente, crea el repo en GitHub y luego ejecuta:

```bash
git remote add origin https://github.com/TU_USUARIO/trello-chatgpt-automation-api.git
git branch -M main
git push -u origin main
```

## Configuración local segura

No pongas valores reales en `appsettings.json`. Inicializa User Secrets desde la carpeta del proyecto:

```bash
cd TrelloAutomation.Api
dotnet user-secrets init
dotnet user-secrets set "Trello:ApiKey" "TU_TRELLO_API_KEY"
dotnet user-secrets set "Trello:Token" "TU_TRELLO_TOKEN"
dotnet user-secrets set "Trello:BoardId" "TU_TRELLO_BOARD_ID"
dotnet user-secrets set "Integration:ApiKey" "TU_INTEGRATION_API_KEY"
```

También puedes usar variables de entorno:

```text
Trello__ApiKey
Trello__Token
Trello__BoardId
Trello__BaseUrl
Integration__ApiKey
```

Generar `INTEGRATION_API_KEY`:

PowerShell:

```powershell
[guid]::NewGuid().ToString("N")
```

Bash/Linux/Mac:

```bash
uuidgen | tr -d '-'
```

## Ejecutar localmente

```bash
dotnet restore
dotnet build
dotnet run --project TrelloAutomation.Api
```

Abre Swagger en la URL que muestre la consola, normalmente:

```text
http://localhost:5000/swagger
```

## Endpoints

- `GET /health`: público.
- `POST /api/trello/preview-plan`: requiere `X-Integration-Key`, valida el plan y no llama a Trello.
- `GET /api/trello/board-context`: requiere `X-Integration-Key`, consulta listas y etiquetas del tablero configurado.
- `POST /api/trello/sync-plan`: requiere `X-Integration-Key`, crea o reutiliza listas, etiquetas, tarjetas y checklists.

## Probar con Swagger

1. Ejecuta la API.
2. Abre Swagger.
3. En los endpoints `/api/trello/*`, agrega el header `X-Integration-Key` con tu clave interna.
4. Usa el contenido de `sample-plan.json` como body.
5. Ejecuta primero `preview-plan`.
6. Cuando el resumen sea correcto, ejecuta `sync-plan`.

## Probar con curl

Preview:

```bash
curl -X POST http://localhost:5000/api/trello/preview-plan \
  -H "Content-Type: application/json" \
  -H "X-Integration-Key: TU_INTEGRATION_API_KEY" \
  --data @TrelloAutomation.Api/sample-plan.json
```

Sync:

```bash
curl -X POST http://localhost:5000/api/trello/sync-plan \
  -H "Content-Type: application/json" \
  -H "X-Integration-Key: TU_INTEGRATION_API_KEY" \
  --data @TrelloAutomation.Api/sample-plan.json
```

## Trello

Consigue tu API Key desde el portal de Trello para desarrolladores y genera un Token autorizado para tu cuenta. El `Trello:BoardId` puede ser el ID largo del tablero o su `shortLink`.

## Despliegue en Render

Render puede ejecutar esta API como Web Service usando el `Dockerfile` ubicado en la raiz del repositorio.

Configuracion sugerida:

- Service type: `Web Service`.
- Runtime: `Docker`.
- Root directory: dejar vacio si Render apunta a la raiz del repo.
- Dockerfile path: `Dockerfile`.
- Health check path: `/health`.

Variables de entorno requeridas en Render:

```text
Trello__ApiKey=TU_TRELLO_API_KEY
Trello__Token=TU_TRELLO_TOKEN
Trello__BoardId=TU_TRELLO_BOARD_ID
Trello__BaseUrl=https://api.trello.com/1
Integration__ApiKey=TU_INTEGRATION_API_KEY
ASPNETCORE_ENVIRONMENT=Production
```

No agregues estas variables al repositorio. Configuralas en el panel de Render, dentro de `Environment`.

La aplicacion lee la variable `PORT` que entrega Render y escucha en `http://0.0.0.0:{PORT}`. Si `PORT` no existe, usa `8080` como fallback. Swagger queda habilitado temporalmente para probar el despliegue:

```text
https://TU-SERVICIO.onrender.com/swagger
```

Prueba rapida despues del deploy:

```bash
curl https://TU-SERVICIO.onrender.com/health
```

Para probar los endpoints protegidos, envia el header:

```text
X-Integration-Key: TU_INTEGRATION_API_KEY
```

## Siguiente paso: GPT Actions

Publica la API en HTTPS, importa el OpenAPI de Swagger en tu GPT personalizado y configura el header `X-Integration-Key` como autenticación de la Action. Revisa `openapi-notes.md` para detalles de producción.
