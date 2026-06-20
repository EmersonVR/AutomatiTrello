FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NuGet.config ./
COPY TrelloAutomation.Api/TrelloAutomation.Api.csproj TrelloAutomation.Api/
RUN dotnet restore TrelloAutomation.Api/TrelloAutomation.Api.csproj

COPY . .
RUN dotnet publish TrelloAutomation.Api/TrelloAutomation.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TrelloAutomation.Api.dll"]
