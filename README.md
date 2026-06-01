# gesterum-deploy-orchestrator

Base C# (.NET 8) para automatizacion de deploys y operaciones de infraestructura.

## Alcance inicial
- Cloudflare DNS: crear registros para subdominios.
- AWS SQS: crear colas.
- Nginx ops: test/reload/restart/status (local o SSH).
- Modo `DryRun` para ejecucion segura.

## Arquitectura
- API minimal en `src/Gesterum.Deploy.Orchestrator.Api`.
- Servicios desacoplados por provider:
  - `CloudflareService`
  - `SqsService`
  - `NginxService`
- Opciones por seccion (`Cloudflare`, `Aws`, `Nginx`).

## Endpoints
- `GET /health`
- `POST /api/cloudflare/dns`
- `POST /api/aws/sqs`
- `POST /api/nginx`

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
  -d '{"type":"A","name":"exfi.eldean.com.ar","content":"149.50.148.174","ttl":120,"proxied":true}'
```

Crear SQS:
```sh
curl -sS -X POST http://127.0.0.1:5000/api/aws/sqs \
  -H 'Content-Type: application/json' \
  -d '{"queueName":"gesterum-jobs","fifo":false,"visibilityTimeoutSeconds":30}'
```

Nginx test/reload:
```sh
curl -sS -X POST http://127.0.0.1:5000/api/nginx \
  -H 'Content-Type: application/json' \
  -d '{"action":"test"}'

curl -sS -X POST http://127.0.0.1:5000/api/nginx \
  -H 'Content-Type: application/json' \
  -d '{"action":"reload"}'
```

## Seguridad
- Mantener `DryRun=true` por defecto.
- No guardar secretos en git.
- Para SSH, usar usuario de minimo privilegio y rotacion de credenciales.

## Proyeccion funcional
- deploys por template (dotnet/node/python)
- provisionamiento de vhosts Nginx
- health checks y rollback automatizado
- runners asincronos y colas de jobs
- audit log y aprobaciones por entorno
