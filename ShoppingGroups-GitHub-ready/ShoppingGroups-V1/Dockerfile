FROM node:24-bookworm-slim AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci --ignore-scripts
COPY web/ ./
RUN npm test && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/ShoppingGroups.Domain/ShoppingGroups.Domain.csproj src/ShoppingGroups.Domain/
COPY src/ShoppingGroups.Api/ShoppingGroups.Api.csproj src/ShoppingGroups.Api/
RUN dotnet restore src/ShoppingGroups.Api
COPY src/ src/
COPY tests/ShoppingGroups.Domain.Tests/ tests/ShoppingGroups.Domain.Tests/
RUN dotnet run --project tests/ShoppingGroups.Domain.Tests -c Release
RUN dotnet publish src/ShoppingGroups.Api -c Release --no-restore -o /out /p:UseAppHost=false
COPY --from=web /web/dist/ /out/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production DataProtection__Path=/app/keys
COPY --from=build /out/ ./
RUN mkdir -p /app/keys && chown -R app:app /app/keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShoppingGroups.Api.dll"]
