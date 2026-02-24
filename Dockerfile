# EasySave v3.0 - Docker Log Centralization Server
# This Dockerfile builds the centralized log server service

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Code/LogServer/LogServer.csproj", "LogServer/"]
RUN dotnet restore "LogServer/LogServer.csproj"
COPY Code/LogServer/ LogServer/
WORKDIR "/src/LogServer"
RUN dotnet build "LogServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LogServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
VOLUME /app/Logs
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
ENTRYPOINT ["dotnet", "LogServer.dll"]