# gesterum-deploy-orchestrator

Base C# (.NET 8) para automatizacion de deploys y operaciones de infraestructura.

## Alcance actual (fase 4)
- Cloudflare DNS: crear registros para subdominios.
- AWS SQS: crear colas.
- Nginx ops: test/reload/restart/status (local o SSH).
- Nginx vhost management: crear/actualizar vhost + rollback por snapshot.
- Deploy template planner + executor runtime-aware (dotnet/node/python).
- Job queue con persistencia SQLite.
- Estados de job: queued/approved/running/succeeded/failed/rejected.
- Aprobaciones por entorno (staging/prod) y por acciones peligrosas.
- API key auth opcional.
- Modo `DryRun` para ejecucion segura.

## Endpoints principales
- `GET /health`
- `POST /api/cloudflare/dns`
- `POST /api/aws/sqs`
- `POST /api/nginx`
- `POST /api/nginx/vhost`
- `POST /api/nginx/vhost/rollback`
- `POST /api/deploy/template`
- `POST /api/jobs/enqueue`
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `POST /api/jobs/{id}/approval`

## Ejemplo deploy.execute
```json
{
  "jobType": "deploy.execute",
  "payloadJson": "{\"runtime\":\"dotnet\",\"environment\":\"production\",\"appPath\":\"/mnt/extra/devprojects/my-api\",\"buildCommand\":\"dotnet build -c Release\",\"startCommand\":\"./start.sh\",\"healthUrl\":\"http://127.0.0.1:5070/health\",\"healthTimeoutSeconds\":30,\"domain\":\"api.eldean.com.ar\",\"port\":5070,\"createOrUpdateNginxVhost\":true,\"dangerous\":true}"
}
```

## Seguridad
- mantener `DryRun=true` por defecto.
- no guardar secretos en git.
- usar SSH key-based en lugar de password cuando pases a produccion.
- exigir aprobacion en produccion.

## Nota
- El executor actual realiza build/start/health y opcionalmente vhost nginx.
- Para operaciones de mayor riesgo, mantener aprobaciones y backups activos.
