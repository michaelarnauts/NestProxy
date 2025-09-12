
# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/NestProxy.Server/NestProxy.Server.csproj", "src/NestProxy.Server/"]
COPY ["src/NestProxy.Client/NestProxy.Client.csproj", "src/NestProxy.Client/"]
COPY ["src/NestProxy.Shared/NestProxy.Shared.csproj", "src/NestProxy.Shared/"]
RUN dotnet restore "./src/NestProxy.Server/NestProxy.Server.csproj"
COPY . .
WORKDIR "/src/src/NestProxy.Server"
RUN dotnet build "./NestProxy.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NestProxy.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NestProxy.Server.dll"]