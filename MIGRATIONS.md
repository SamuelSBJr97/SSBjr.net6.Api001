MIGRATIONS (EF Core)
=====================

Este projeto usa Entity Framework Core com SQL Server.

Criar uma migração (local)
1. Abra o terminal na pasta do projeto API:

```powershell
cd src\src\SSBJr.Net6.Api001
dotnet tool install --global dotnet-ef # se ainda não instalado
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Obs: para aplicar migrações contra o container SQL Server, primeiro suba o `docker-compose` e então rode os comandos acima (a `ConnectionString` já aponta para `sqlserver` no compose).

Aplicar migrações automaticamente
- Atualmente o startup tenta aplicar migrações na inicialização com tentativas. Em produção prefira executar migrações via pipeline/CI.
