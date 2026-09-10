FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY AirSms.sln ./
COPY src/AirSms.Application/AirSms.Application.csproj src/AirSms.Application/
COPY src/AirSms.Contracts/AirSms.Contracts.csproj src/AirSms.Contracts/
COPY src/AirSms.Domain/AirSms.Domain.csproj src/AirSms.Domain/
COPY src/AirSms.Infrastructure/AirSms.Infrastructure.csproj src/AirSms.Infrastructure/
COPY src/AirSms.Projections.Worker/AirSms.Projections.Worker.csproj src/AirSms.Projections.Worker/
RUN dotnet restore src/AirSms.Projections.Worker/AirSms.Projections.Worker.csproj

COPY . .
RUN dotnet publish src/AirSms.Projections.Worker/AirSms.Projections.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_ENVIRONMENT=Production
USER app
ENTRYPOINT ["dotnet", "AirSms.Projections.Worker.dll"]
