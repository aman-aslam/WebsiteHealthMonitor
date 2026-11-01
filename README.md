# Website Health Monitor

A website availability and page-load monitoring project built with ASP.NET Core Web API, Playwright, and a plain HTML/CSS/JavaScript dashboard.

## What it does

- Checks configured websites using a real browser session.
- Measures page-load duration with Playwright and Stopwatch.
- Uses a bounded navigation timeout so an unresponsive page does not block a check indefinitely.
- Presents monitoring results in a browser-based dashboard.
- Supports focusing attention on unavailable websites rather than relying only on aggregate averages.

## Technology

- Backend: ASP.NET Core Web API
- Runtime: .NET 8
- Browser automation: Microsoft.Playwright
- Frontend: HTML, CSS, and vanilla JavaScript
- Browser engine: Playwright-managed Chromium, when configured by the application

## High-level architecture

Dashboard in browser → ASP.NET Core Web API → Monitoring service → Playwright browser → Configured websites

## Getting started

### Prerequisites

- .NET 8 SDK
- A supported operating system for Playwright
- Network access to the websites you want to monitor

### Run the API

1. Clone this repository.
2. Locate the API project file (.csproj) in the repository.
3. Restore, build, and run the project:

    dotnet restore path/to/project.csproj
    dotnet build path/to/project.csproj --configuration Release
    dotnet run --project path/to/project.csproj

4. Install the Playwright browser required by the project. Follow the Microsoft Playwright for .NET installation instructions for your operating system and browser.
5. Open the frontend using the static hosting approach configured in the repository, and point it to the running API.

A site may be reachable at the network level but still fail browser navigation. Review the returned status and error details together.

## Configuration

Use the project's existing configuration and environment-specific settings to manage target websites, API address, and monitoring options. Do not commit credentials, private URLs, or environment-specific secrets.

## Development notes

- Keep browser navigation timeouts bounded.
- Treat a failed browser navigation as a monitoring result, not proof that a remote host is permanently offline.
- Add tests for timeout handling, unreachable hosts, and successful page navigation when extending the service.

## Security

Please do not publish security vulnerabilities in public issues. See SECURITY.md for responsible disclosure guidance.
