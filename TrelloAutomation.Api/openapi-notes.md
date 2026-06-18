# OpenAPI notes para GPT Actions

Esta API puede conectarse a un GPT personalizado publicando el backend en una URL HTTPS y exponiendo el documento OpenAPI generado por Swagger.

El GPT usaría principalmente:

- `POST /api/trello/preview-plan` para validar y mostrar un resumen antes de tocar Trello.
- `POST /api/trello/sync-plan` para crear o actualizar listas, tarjetas, etiquetas y checklists.
- `GET /api/trello/board-context` para consultar listas y etiquetas existentes del tablero.

Todos los endpoints bajo `/api/trello/*` deben enviar el header:

```text
X-Integration-Key: TU_INTEGRATION_API_KEY
```

El JSON que enviaría el GPT debe seguir el formato de `sample-plan.json`: un `boardId` opcional, modo `append`, y una colección de listas con tarjetas, etiquetas, checklist, criterios de aceptación y notas para Codex.

Para producción:

- Publica solo mediante HTTPS.
- Guarda secretos en el proveedor seguro del hosting, no en archivos del repo.
- Rota `Integration:ApiKey` si se comparte accidentalmente.
- Limita CORS según el cliente real si agregas frontend.
- Agrega rate limiting y observabilidad antes de abrirlo a uso frecuente.
- Revisa manualmente los planes generados por GPT antes de ejecutar `sync-plan`.
