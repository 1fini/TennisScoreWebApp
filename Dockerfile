# ----------------------
# Étape build
# ----------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier solution et projets
COPY *.sln .
COPY TennisScoreWebApp/*.csproj TennisScoreWebApp/
COPY TennisScoreWebApp.Tests/*.csproj TennisScoreWebApp.Tests/
RUN dotnet restore

# Copier le reste et compiler
COPY . .
RUN dotnet test TennisScoreWebApp.Tests/TennisScoreWebApp.Tests.csproj -c Release --no-restore
WORKDIR /src/TennisScoreWebApp
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ----------------------
# Étape runtime
# ----------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port par défaut Blazor Server
EXPOSE 8080

# Important pour ASP.NET Core dans container
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TennisScoreWebApp.dll"]
