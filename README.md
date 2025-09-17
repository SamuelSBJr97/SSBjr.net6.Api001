SSBJr.Net6.Api001
=================

Projeto .NET 6 Web API com EF Core (SQL Server), Swagger e projeto de testes MSTest.

Estrutura
- `src/src/SSBJr.Net6.Api001` - Projeto Web API
- `src/src/SSBJr.Net6.Api001.Tests` - Projeto de testes (MSTest)

Visão geral rápida
- A API é orquestrada via Docker Compose junto com um container SQL Server.
- Connection string padrão (em `appsettings.json`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=sqlserver,1433;Database=Api001Db;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
}
```

Rodando localmente com Docker Compose
------------------------------------

1. Tenha o Docker Desktop instalado.
2. Na raiz do repositório execute (PowerShell):

```powershell
docker-compose -f src\docker-compose.yml up --build -d
```

3. Acesse Swagger: http://localhost:5000/swagger

Notas de segurança
- A senha `sa` está em texto no `docker-compose.yml` para facilitar testes locais. Em ambientes reais, utilize secrets ou variáveis de ambiente seguras.

Links úteis
- Projeto: `src/src/SSBJr.Net6.Api001`
# SSBjr.net6.Api001