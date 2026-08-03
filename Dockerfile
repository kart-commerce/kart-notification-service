FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartNotificationService.sln .
COPY Directory.Build.props .
COPY src/Api/Kart.Notification.Api.csproj src/Api/
COPY src/Application/Kart.Notification.Application.csproj src/Application/
COPY src/Domain/Kart.Notification.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Notification.Infrastructure.csproj src/Infrastructure/
COPY tests/UnitTests/Kart.Notification.UnitTests.csproj tests/UnitTests/
COPY tests/IntegrationTests/Kart.Notification.IntegrationTests.csproj tests/IntegrationTests/
COPY tests/ContractTests/Kart.Notification.ContractTests.csproj tests/ContractTests/
COPY nuget.config .
COPY packages packages
RUN dotnet restore src/Api/Kart.Notification.Api.csproj

COPY . .
RUN dotnet publish src/Api/Kart.Notification.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Notification.Api.dll"]
