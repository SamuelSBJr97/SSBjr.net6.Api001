DOCKER
======

O `docker-compose.yml` localizado em `src/docker-compose.yml` orquestra dois serviços:
- `sqlserver` - imagem oficial `mcr.microsoft.com/mssql/server:2019-latest`
- `api` - nossa aplicação ASP.NET Core

Portas exposas por padrão:
- API: localhost:5000 -> container:80
- SQL Server: localhost:1433 -> container:1433

Trocar senha SA
- Altere a variável `SA_PASSWORD` no `docker-compose.yml` antes de usar em ambientes não-locais.

Usar Docker secrets (exemplo rápido)
1. Crie um segredo:

```powershell
docker secret create sa_password - < password.txt
```

2. Consuma o secret no compose (não presente atualmente, implementação pendente).
