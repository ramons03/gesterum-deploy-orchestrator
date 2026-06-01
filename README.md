# gesterum-deploy-orchestrator

Base C# (.NET 8) para automatizacion de deploys y operaciones de infraestructura.

## Alcance actual (fase 6)
- Cloudflare DNS: crear registros para subdominios.
- AWS SQS: crear colas.
- Nginx ops: test/reload/restart/status (local o SSH).
- Nginx vhost management: crear/actualizar vhost + rollback por snapshot.
- Deploy template planner + executor runtime-aware (dotnet/node/python).
- Job queue con persistencia SQLite.
- Estados de job: queued/approved/running/succeeded/failed/rejected.
- Aprobaciones por entorno (staging/prod) y por acciones peligrosas.
- RBAC con Identity roles: admin/operator/reviewer.
- Auth JWT.

## Endpoints auth
- `POST /api/auth/seed-admin` (bootstrap inicial)
- `POST /api/auth/login` (retorna JWT)

## Endpoints operativos
- `GET /health`
- `POST /api/cloudflare/dns` (operator/admin)
- `POST /api/aws/sqs` (operator/admin)
- `POST /api/nginx` (operator/admin)
- `POST /api/nginx/vhost` (operator/admin)
- `POST /api/nginx/vhost/rollback` (operator/admin)
- `POST /api/deploy/template` (operator/admin)
- `POST /api/jobs/enqueue` (operator/admin)
- `GET /api/jobs` (operator/admin)
- `GET /api/jobs/{id}` (operator/admin)
- `POST /api/jobs/{id}/approval` (reviewer/admin)

## Flujo recomendado
1) Seed admin
2) Login y obtener JWT
3) Enqueue de job deploy
4) Approval por reviewer/admin (si corresponde)
5) Worker ejecuta y actualiza estado

## Seguridad
- Cambiar `JWT:Key` antes de cualquier uso real.
- Mantener `DryRun=true` hasta validar flujos.
- Usar separacion de roles en produccion.

## Nota
Se mantiene `EnsureCreated` para bootstrap rapido. En fase siguiente conviene migraciones EF versionadas.
