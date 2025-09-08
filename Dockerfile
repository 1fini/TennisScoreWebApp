# ----------------------
# Étape build
# ----------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copier solution et projets
COPY *.sln .
COPY TennisScoreWebApp/*.csproj TennisScoreWebApp/
RUN dotnet restore

# Copier le reste et compiler
COPY . .
WORKDIR /src/TennisScoreWebApp
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ----------------------
# Étape runtime
# ----------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port par défaut Blazor Server
EXPOSE 8080

# Important pour ASP.NET Core dans container
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TennisScoreWebApp.dll"]
