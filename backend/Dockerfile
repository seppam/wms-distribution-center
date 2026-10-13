# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY WmsDistributionCenter.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish WmsDistributionCenter.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
# the aspnet image ships without curl/wget: probe the liveness endpoint with bash
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /healthz HTTP/1.0\r\n\r\n" >&3 && grep -q " 200 " <&3' || exit 1
USER $APP_UID
ENTRYPOINT ["dotnet", "WmsDistributionCenter.Api.dll"]
