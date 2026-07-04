# Contributing to TennisScoreWebApp

Thanks for your interest in contributing. TennisScoreWebApp is the Blazor Server frontend for an open-source live tennis scoring platform aimed at clubs, academies, associations, and amateur tournaments.

## Before You Start

- Check existing issues and pull requests to avoid duplicate work.
- For larger UX or architecture changes, open an issue first.
- Keep pull requests focused and reasonably small.

## Local Setup

Requirements:

- .NET 10 SDK
- A running TennisScoresAPI instance

Common commands:

```bash
dotnet restore
dotnet build
dotnet run --project TennisScoreWebApp/TennisScoreWebApp.csproj
```

By default, the WebApp reads API URLs from `TennisScoreWebApp/appsettings.json`. You can override them with:

```bash
export SCORE_API_URL="http://localhost:5227/"
export SCOREHUB_URL="http://localhost:5227/scoreHub"
dotnet run --project TennisScoreWebApp/TennisScoreWebApp.csproj
```

## API Client Generation

The API client is generated with NSwag from `TennisScoreWebApp/ApiDefinitions/swagger.json`.

When the API contract changes, regenerate the client and commit both the OpenAPI file and generated client:

```bash
bash TennisScoreWebApp/ApiDefinitions/command.sh
```

## Pull Request Guidelines

- Explain the problem and the solution clearly.
- Include screenshots or short videos for visible UI changes.
- Test the main flow manually: tournaments, players, match creation, scoring, and live updates.
- Keep API client changes aligned with the backend contract.
- Do not commit secrets, production passwords, local `.env` files, or generated personal data.

## Useful Contribution Areas

- Courtside scoring UX.
- Mobile and tablet responsive behavior.
- Real-time score update feedback.
- Accessibility.
- Deployment and operations documentation.
