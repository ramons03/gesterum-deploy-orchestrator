# gesterum-deploy-orchestrator

Base C# (.NET 8) para automatizacion de deploys y operaciones de infraestructura.

## Alcance actual (fase 3)
- Cloudflare DNS: crear registros para subdominios.
- AWS SQS: crear colas.
- Nginx ops: test/reload/restart/status (local o SSH).
- Deploy template planner: plan base para despliegues dotnet/node/python.
- Job queue con persistencia SQLite.
- Estados de job: queued/approved/running/succeeded/failed/rejected.
- Aprobaciones para acciones peligrosas.
- API key auth opcional.
- Modo `DryRun` para ejecucion segura.

## Arquitectura
- API minimal en `src/Gesterum.Deploy.Orchestrator.Api`.
- Persistencia EF Core SQLite (`AppDbContext`).
- Orquestacion:
  - `JobOrchestratorService`
  - `JobQueueService`
  - `JobWorker`
  - `DeployExecutorService`

## Endpoints
- `GET /health`
- `POST /api/cloudflare/dns`
- `POST /api/aws/sqs`
- `POST /api/nginx`
- `POST /api/deploy/template`
- `POST /api/jobs/enqueue`
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `POST /api/jobs/{id}/approval`

## Auth
- `Auth:Enabled=false` por defecto.
- si se activa, enviar `X-API-Key`.

## Run local
```sh
cd src/Gesterum.Deploy.Orchestrator.Api
dotnet run
```

## Enqueue deploy job (ejemplo)
```sh
curl -sS -X POST http://127.0.0.1:5000/api/jobs/enqueue \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{
    "jobType":"deploy.execute",
    "payloadJson":"{\"runtime\":\"dotnet\",\"appPath\":\"/mnt/extra/devprojects/demo\",\"startCommand\":\"./start.sh\",\"healthUrl\":\"http://127.0.0.1:5050/health\",\"dangerous\":true}"
  }'
```

## Aprobar job
```sh
curl -sS -X POST http://127.0.0.1:5000/api/jobs/{JOB_ID}/approval \
  -H 'Content-Type: application/json' \
  -H 'X-API-Key: YOUR_KEY' \
  -d '{"approve":true}'
```

## Seguridad
- mantener `DryRun=true` por defecto.
- no guardar secretos en git.
- usar SSH key-based en lugar de password cuando pases a produccion.

## Roadmap siguiente
- ejecutor de deploy real por runtime con health check activo
- creacion automatica de vhost nginx + TLS
- rollback automatizado por version
- politicas RBAC por entorno
