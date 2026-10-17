FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER root
RUN mkdir -p /home/pki/basestar-server /home/logs /app/config && chown -R $APP_UID /home /app/config
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DWIS.DAQBridge.BaseStar.Server/DWIS.DAQBridge.BaseStar.Server.csproj", "DWIS.DAQBridge.BaseStar.Server/"]
COPY ["DWIS.DAQBridge.BaseStar.Model/DWIS.DAQBridge.BaseStar.Model.csproj", "DWIS.DAQBridge.BaseStar.Model/"]
RUN dotnet restore "DWIS.DAQBridge.BaseStar.Server/DWIS.DAQBridge.BaseStar.Server.csproj"
COPY . .
WORKDIR "/src/DWIS.DAQBridge.BaseStar.Server"
RUN dotnet build "DWIS.DAQBridge.BaseStar.Server.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DWIS.DAQBridge.BaseStar.Server.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish /app/publish .
COPY ["DWIS.DAQBridge.BaseStar.Server/config/Quickstarts.ReferenceClient.Container.Config.xml", "/app/config/Quickstarts.ReferenceClient.Config.xml"]
VOLUME /home
ENTRYPOINT ["dotnet", "DWIS.DAQBridge.BaseStar.Server.dll"]
