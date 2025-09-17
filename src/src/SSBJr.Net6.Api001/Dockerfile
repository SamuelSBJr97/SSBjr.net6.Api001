FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY ["SSBJr.Net6.Api001.csproj", "./"]
RUN dotnet restore "SSBJr.Net6.Api001.csproj"

COPY . .
RUN dotnet publish "SSBJr.Net6.Api001.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 80
ENTRYPOINT ["dotnet", "SSBJr.Net6.Api001.dll"]
