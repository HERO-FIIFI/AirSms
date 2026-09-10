FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY AirSms.sln ./
COPY src/AirSms.Api/AirSms.Api.csproj src/AirSms.Api/
COPY src/AirSms.Application/AirSms.Application.csproj src/AirSms.Application/
COPY src/AirSms.Contracts/AirSms.Contracts.csproj src/AirSms.Contracts/
COPY src/AirSms.Domain/AirSms.Domain.csproj src/AirSms.Domain/
COPY src/AirSms.Infrastructure/AirSms.Infrastructure.csproj src/AirSms.Infrastructure/
RUN dotnet restore src/AirSms.Api/AirSms.Api.csproj

COPY . .
RUN dotnet publish src/AirSms.Api/AirSms.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
RUN apk add --no-cache curl \
    && mkdir -p /app/App_Data/DataProtectionKeys \
    && chown -R app:app /app
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production
USER app
ENTRYPOINT ["dotnet", "AirSms.Api.dll"]
