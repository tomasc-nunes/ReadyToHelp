# ReadyToHelp

ReadyToHelp lets users report occurrences, confirm them and provide feedback in a Waze-style workflow, and track their status.

This repository contains three application components:

| Component | Technology | Setup guide |
| --- | --- | --- |
| API/backend | ASP.NET Core on .NET 8, Entity Framework Core, PostgreSQL/PostGIS | [API README](API/README.md) |
| Web frontend | Angular 19.2 and TypeScript | [Web README](WEB/readytohelp/README.md) |
| Mobile frontend | Kotlin, Jetpack Compose, and Android | [Mobile README](MOBILE/README.md) |

## Prerequisites

Install [Git](https://git-scm.com/downloads) and the tools for the components you intend to run:

- **API:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), the matching Entity Framework CLI described in the API guide, and [Docker Desktop](https://www.docker.com/products/docker-desktop/) or a local PostgreSQL installation with PostGIS.
- **Web:** [Node.js 22.x and npm](https://nodejs.org/en/download). Angular CLI and other frontend dependencies are installed locally through `npm ci`.
- **Mobile:** [Android Studio](https://developer.android.com/studio), JDK 17, Android SDK Platform 36, and an emulator or Android device with API 28 or newer. Use the included Gradle wrapper.

Both frontends use Mapbox and currently point to a hosted API. Their guides explain how to configure public map tokens and switch to a local backend.

## Get the code

```powershell
git clone https://gitlab.com/grupo_07-lds-2526/lds_25_26.git ReadyToHelp
cd ReadyToHelp
```

The explicit destination folder keeps the following paths consistent. If you already have a checkout, use its existing folder.

## Run locally

1. Follow the [API guide](API/README.md) to configure PostgreSQL/PostGIS, apply migrations, and start the backend. Local Swagger is normally available at [http://localhost:5134/swagger](http://localhost:5134/swagger).
2. Follow the [web guide](WEB/readytohelp/README.md) to configure its API URLs and maps, install dependencies, and run `npm start` from `WEB/readytohelp`. Open [http://localhost:4200](http://localhost:4200).
3. Follow the [mobile guide](MOBILE/README.md) to configure the SDK, maps, and device-specific API URL, then build and launch the app from Android Studio or the Gradle wrapper.

The frontend development tools do not start the API or database. Keep those services running when using a local backend. Each component guide includes build, test, and troubleshooting instructions.

## Repository structure

```text
API/                ASP.NET Core API and PostgreSQL Compose configuration
WEB/readytohelp/     Angular application
MOBILE/             Android application
.gitlab-ci.yml      CI pipeline configuration
```

## Contributing

Run the relevant component's tests before submitting a change and update its documentation when setup or behavior changes. Do not commit passwords, secret tokens, signing keys, or machine-specific configuration.
