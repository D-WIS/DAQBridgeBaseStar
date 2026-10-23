# Build with this repository root as the Docker context.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish DWIS.DAQBridge.BaseStar.Server/DWIS.DAQBridge.BaseStar.Server.csproj -c Release -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .
VOLUME /home
ENV DWIS_CONFIG_PATH=/home/config.json
ENTRYPOINT ["dotnet", "DWIS.DAQBridge.BaseStar.Server.dll"]
