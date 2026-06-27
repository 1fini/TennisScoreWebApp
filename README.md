# TennisScoreWebApp

Blazor Server web application for managing tennis tournaments, players, matches, and live scoring.

TennisScoreWebApp is the user-facing part of the TennisScore MVP. It consumes the TennisScoresAPI backend, listens to live score updates over SignalR, and provides a compact interface for running tournament matches point by point.

## Features

- Tournament list and tournament detail pages.
- Player list, player creation, and player deletion.
- Match creation inside a tournament.
- Live match detail page with point entry.
- Point types for winners, aces, double faults, forced errors, unforced errors, and time violations.
- Completed match display with winner and final score.
- Tournament match cards with status filtering: `All`, `Not started`, `Live`, `Finished`.
- Confirmation prompts before destructive actions.
- API client generated from the TennisScoresAPI OpenAPI contract with NSwag.
- Production Docker Compose stack with WebApp, API, PostgreSQL, and Traefik.

## Architecture

```text
Browser
  |
  | HTTPS
  v
Traefik
  |
  | HTTP, Docker internal network
  v
TennisScoreWebApp
  |
  | HTTP API + SignalR hub
  v
TennisScoresAPI
  |
  | Npgsql
  v
PostgreSQL
```

The WebApp is the only public container in the production stack. The API and PostgreSQL services are kept on Docker internal networks.

## Tech Stack

- .NET 10
- ASP.NET Core Blazor Server
- SignalR client
- NSwag generated API client
- Docker multi-stage builds
- Docker Compose
- Traefik reverse proxy
- PostgreSQL

## Repository Layout

```text
.
├── Dockerfile
├── docker-compose.prod.yml
├── .env.prod.example
├── TennisScoreWebApp.sln
└── TennisScoreWebApp/
    ├── Components/
    ├── Services/
    ├── ApiDefinitions/
    ├── Program.cs
    └── appsettings.json
```

## Requirements

For local development:

- .NET 10 SDK
- A running TennisScoresAPI instance

For production deployment:

- Docker
- Docker Compose
- DNS record pointing to the host running Traefik
- Public ports `80` and `443` available for Traefik

## Local Development

Restore and build:

```bash
dotnet restore
dotnet build
```

Run the WebApp:

```bash
dotnet run --project TennisScoreWebApp/TennisScoreWebApp.csproj
```

By default, the application reads these settings from `TennisScoreWebApp/appsettings.json`:

```json
{
  "SCORE_API_URL": "https://localhost:7277/",
  "SCOREHUB_URL": "http://localhost:5227/scoreHub"
}
```

Override them with environment variables when running against another API instance:

```bash
export SCORE_API_URL="http://localhost:5227/"
export SCOREHUB_URL="http://localhost:5227/scoreHub"
dotnet run --project TennisScoreWebApp/TennisScoreWebApp.csproj
```

## API Client Generation

The API client is generated with NSwag from:

```text
TennisScoreWebApp/ApiDefinitions/swagger.json
```

Regenerate it with:

```bash
bash TennisScoreWebApp/ApiDefinitions/command.sh
```

Commit both the updated `swagger.json` and generated `Services/TennisApiClient.cs` when the API contract changes.

## Production Deployment

The repository includes a production-oriented Docker Compose file:

```text
docker-compose.prod.yml
```

Create a production environment file from the example:

```bash
cp .env.prod.example .env.prod
```

Edit `.env.prod`:

```env
TRAEFIK_HOST=tennis.example.com
TRAEFIK_ACME_EMAIL=admin@example.com
TRAEFIK_CERT_RESOLVER=letsencrypt
TRAEFIK_LOG_LEVEL=INFO

API_IMAGE=1fini/tennisscoreapi:latest
MIGRATIONS_IMAGE=1fini/tennisscoreapi-migrations:latest
WEBAPP_IMAGE=1fini/tennisscore-webapp:latest

ASPNETCORE_ENVIRONMENT=Production

DB_NAME=tennisscore
DB_USER=tennisscore
DB_PASSWORD=change-me
```

Start the stack:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
```

Apply database migrations explicitly when needed:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml --profile migrations run --rm migrations
```

The migration service is behind the `migrations` profile, so it does not run during a normal `up -d`. It waits for the PostgreSQL health check before applying migrations.

Inspect logs:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f
```

Stop the stack:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml down
```

PostgreSQL data and Traefik certificates are stored in Docker volumes:

- `postgres_data`
- `traefik_letsencrypt`

Do not remove those volumes unless you intentionally want to reset data/certificates.

## Docker Images

The production compose file expects these images by default:

- `1fini/tennisscore-webapp:latest`
- `1fini/tennisscoreapi:latest`
- `1fini/tennisscoreapi-migrations:latest`

The GitHub workflow builds and publishes multi-architecture images for:

- `linux/amd64`
- `linux/arm64`

This makes the stack suitable for Raspberry Pi deployments.

## Configuration

| Variable | Description | Example |
| --- | --- | --- |
| `SCORE_API_URL` | Internal API base URL used by the WebApp | `http://api:8080/` |
| `SCOREHUB_URL` | Internal SignalR hub URL used by the WebApp | `http://api:8080/scoreHub` |
| `MIGRATIONS_IMAGE` | EF Core migration bundle image | `1fini/tennisscoreapi-migrations:latest` |
| `TRAEFIK_HOST` | Public hostname served by Traefik | `tennis.example.com` |
| `TRAEFIK_ACME_EMAIL` | Email used for Let's Encrypt certificates | `admin@example.com` |
| `DB_NAME` | PostgreSQL database name | `tennisscore` |
| `DB_USER` | PostgreSQL user | `tennisscore` |
| `DB_PASSWORD` | PostgreSQL password | `change-me` |

## Production Notes

- HTTPS is terminated by Traefik, not by the application containers.
- The WebApp and API listen on HTTP port `8080` inside Docker.
- The API is not exposed publicly by the compose file.
- Database migrations are explicit and run through the `migrations` compose profile.
- PostgreSQL has a Docker health check used by the migration container.

## Roadmap

- Add health checks for WebApp and API.
- Add authentication for MVP users.
- Harden production headers and forwarded header handling behind Traefik.
- Improve observability with structured logs and deployment documentation.

## Related Repository

Backend API:

- https://github.com/1fini/TennisScoresAPI

## License

This project is licensed under the terms of the repository license.
