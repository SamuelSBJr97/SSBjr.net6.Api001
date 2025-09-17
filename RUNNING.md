RUNNING (Instruções rápidas)
=================================

Requisitos
- Docker Desktop (Windows)
- .NET SDK (para executar local sem Docker)

Com Docker (recomendado para este projeto)
1. Na raiz do repo:

```powershell
docker-compose -f src\docker-compose.yml up --build -d
```

2. Verifique containers:

```powershell
docker ps
```

3. Logs da API:

```powershell
docker logs -f src-api-1
```

Sem Docker (apenas desenvolvimento)
1. Abra o terminal na pasta do projeto API:

```powershell
cd src\src\SSBJr.Net6.Api001
dotnet restore
dotnet build
dotnet run
```

2. Swagger: http://localhost:5000/swagger
