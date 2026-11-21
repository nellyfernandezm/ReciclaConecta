# Etapa 1: compilar y publicar con el SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ReciclaConecta.Api/ReciclaConecta.Api.csproj src/ReciclaConecta.Api/
RUN dotnet restore src/ReciclaConecta.Api/ReciclaConecta.Api.csproj
COPY src/ src/
RUN dotnet publish src/ReciclaConecta.Api/ReciclaConecta.Api.csproj -c Release -o /app --no-restore

# Etapa 2: imagen final solo con el runtime (más pequeña y con menos superficie de ataque)
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app
COPY --from=build /app .
# Carpeta de la base SQLite con permisos para el usuario sin privilegios
RUN mkdir -p /app/db && chown -R app:app /app/db
USER app
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReciclaConecta.Api.dll"]
