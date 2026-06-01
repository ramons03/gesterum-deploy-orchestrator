# gesterum-deploy-orchestrator

Base C# (.NET 8) para automatizacion de deploys y operaciones de infraestructura.

## Alcance actual
- Cloudflare DNS: crear registros para subdominios.
- AWS SQS: crear colas.
- Nginx ops: test/reload/restart/status (local o SSH).
- Deploy template planner: plan base para despliegues dotnet/node/python.
- Job queue in-memory: encolar trabajos para ejecucion asincrona futura.
- API key auth opcional.
- Modo `DryRun` para ejecucion segura.

## Arquitectura
- API minimal en `src/Gesterum.Deploy.Orchestrator.Api`.
- Servicios desacoplados por provider:
  - `CloudflareService`
  - `SqsService`
  - `NginxService`
  - `DeployTemplateService`
  - `JobQueueService` + `JobWorker`
- Opciones por seccion (`Auth`, `Cloudflare`, `Aws`, `Nginx`, `DeployTemplates`).

## Endpoints
- `GET /health`
- `POST /api/cloudflare/dns`
- `POST /api/aws/sqs`
- `POST /api/nginx`
- `POST /api/deploy/template`
- `POST /api/jobs/enqueue`

## Auth
- Por defecto `Auth:Enabled=false`.
- Si activas auth, enviar header `X-API-Key` en endpoints protegidos.

## Run local
```sh
cd src/Gesterum.Deploy.Orchestrator.Api
dotnet run
```

## Ejemplos
Crear DNS (Cloudflare):
```sh
curl -sS -X POST http://127.0.0.1:5000/api/cloudflare/dns \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"type":"A","name":"exfi.eldean.com.ar","content":"149.50.148.174","ttl":120,"proxied":true}'
```

Crear SQS:
```sh
curl -sS -X POST http://127.0.0.1:5000/api/aws/sqs \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"queueName":"gesterum-jobs","fifo":false,"visibilityTimeoutSeconds":30}'
```

Nginx test/reload:
```sh
curl -sS -X POST http://127.0.0.1:5000/api/nginx \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"action":"test"}'

curl -sS -X POST http://127.0.0.1:5000/api/nginx \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"action":"reload"}'
```

Generar plan de deploy:
```sh
curl -sS -X POST http://127.0.0.1:5000/api/deploy/template \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"appName":"my-api","runtime":"dotnet","domain":"api.eldean.com.ar","port":5070,"healthPath":"/health"}'
```

## Seguridad
- Mantener `DryRun=true` por defecto.
- No guardar secretos en git.
- Para SSH, usar usuario de minimo privilegio y rotacion de credenciales.

## Roadmap inmediato
- persistencia de jobs y estados
- ejecutor real de deploy por runtime
- creacion automatica de vhost nginx + TLS
- rollback automatizado
- aprobaciones por entorno y auditoria
