FROM node:24.18.0-bookworm-slim AS frontend
WORKDIR /src/client
COPY src/client/package*.json ./
RUN npm ci
COPY src/client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend
WORKDIR /src
COPY global.json ./
COPY src/MiettisPark.Api/MiettisPark.Api.csproj src/MiettisPark.Api/packages.lock.json src/MiettisPark.Api/
RUN dotnet restore src/MiettisPark.Api/MiettisPark.Api.csproj --locked-mode
COPY src/MiettisPark.Api/ src/MiettisPark.Api/
RUN dotnet publish src/MiettisPark.Api/MiettisPark.Api.csproj -c Release --no-restore -o /publish -p:OpenApiGenerateDocuments=false
COPY --from=frontend /src/client/.output/public/ /publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
WORKDIR /app
COPY --from=backend /publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MiettisPark.Api.dll"]
