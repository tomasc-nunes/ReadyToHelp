# ReadyToHelp API

ReadyToHelp REST API, built with ASP.NET Core on .NET 8, Entity Framework Core, and PostgreSQL/PostGIS.

See the [project overview](../README.md), [web guide](../WEB/readytohelp/README.md), and [mobile guide](../MOBILE/README.md) for client setup. Commands below use PowerShell.

## Prerequisites

Install:

- [Git](https://git-scm.com/downloads).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), recommended for PostgreSQL.
- `dotnet-ef`, for applying and creating migrations. Match the project's Entity Framework Core **9.0.9** packages:

```powershell
dotnet tool install --global 
```

If an older version is already installed, update it with:

```powershell
dotnet tool update --global
```

If a newer major version is installed, uninstall that global tool before installing the pinned version above. Check your installations:

```powershell
dotnet --version
dotnet ef --version
docker --version
docker compose version
```

## 1. Get the code

From the directory where you want to store the project:

```powershell
git clone https://gitlab.com/grupo_07-lds-2526/lds_25_26.git ReadyToHelp
cd ReadyToHelp/API
```

If the repository is already cloned, open a terminal in its `API` directory. The remaining commands assume this working directory.

## 2. Configure PostgreSQL

### Recommended option: Docker Compose

Start Docker Desktop first. Compose starts PostgreSQL with PostGIS, which is required for the application's geographic data:

```powershell
docker compose up -d postgres
```

Default connection details:

| Property | Value |
| --- | --- |
| Host | `localhost` |
| Port | `5432` |
| Database | `readytohelp_db` |
| Username | `readytohelp` |
| Password | `readytohelppwd` |

Check that the container is running:

```powershell
docker compose ps
```

To stop only the database:

```powershell
docker compose stop postgres
```

To remove the services and persisted database data (destructive; only use when intentionally resetting the database):

```powershell
docker compose down -v
```

### Alternative: locally installed PostgreSQL

Install [PostgreSQL](https://www.postgresql.org/download/) and [PostGIS](https://postgis.net/install/). You do not need the Compose `postgres` service when using this option.

#### 2.1. Create the local database

Ensure the PostgreSQL service is running. On Windows, check **Services** or pgAdmin. Connect using the administrator account created during installation:

```powershell
psql -U postgres -h localhost -p 5432
```

Inside `psql`, create the application user and database, then enable PostGIS:

```sql
CREATE USER readytohelp WITH PASSWORD 'readytohelppwd';
CREATE DATABASE readytohelp_db OWNER readytohelp;
\c readytohelp_db
CREATE EXTENSION IF NOT EXISTS postgis;
```

If the `readytohelp` user already exists, change its password instead of creating it again:

```sql
ALTER USER readytohelp WITH PASSWORD 'readytohelppwd';
```

You can also use pgAdmin:

1. Create a login/role named `readytohelp` with a password.
2. Create `readytohelp_db` with `readytohelp` as its owner.
3. Execute `CREATE EXTENSION IF NOT EXISTS postgis;` in that database using an account permitted to create the extension.

#### 2.2. Verify the local installation

```powershell
psql -U readytohelp -h localhost -p 5432 -d readytohelp_db -c "SELECT PostGIS_Version();"
```

If this returns the PostGIS version, the database is ready for migrations.

## 3. Restore dependencies

```powershell
dotnet restore ./ReadyToHelpAPI/ReadyToHelpAPI.csproj
```

## 4. Apply database migrations

This step is the same for local PostgreSQL and Docker-hosted PostgreSQL. The application does not automatically create the schema; apply the existing Entity Framework migrations before running it.

Existing migrations are in `ReadyToHelpAPI/Migrations/App`:

```powershell
dotnet ef database update `
  --project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj `
  --startup-project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj
```

The command creates the required tables, relationships, indexes, and geographic types in `readytohelp_db`. Run it again after new migrations are added; only pending migrations are applied.

Check migration status:

```powershell
dotnet ef migrations list `
  --project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj `
  --startup-project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj
```

Create a new migration only when changing the application model:

```powershell
dotnet ef migrations add MigrationName `
  --project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj `
  --startup-project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj `
  --output-dir Migrations/App
```

## 5. Run the API

```powershell
dotnet run --project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj
```

The API listens on `http://localhost:5134`. In Development, open [Swagger](http://localhost:5134/swagger) to inspect and try endpoints.

To explicitly select the HTTP launch profile:

```powershell
dotnet run --project ./ReadyToHelpAPI/ReadyToHelpAPI.csproj --launch-profile http
```

Press `Ctrl+C` to stop the API.

## 6. Run the API and database with Docker

First apply migrations as described above using the database service and the local .NET tools.

**Current configuration issue:** `docker-compose.yml` publishes `8080:8080`, but `Program.cs` explicitly binds the API to port `5134`. Before running the API container, change the `app` service's port mapping to:

```yaml
ports:
  - "8080:5134"
```

This publishes host port `8080` to the port the application actually uses inside the container. Then start both services:

```powershell
docker compose up --build
```

With that correction, the API is reachable at `http://localhost:8080` and [Swagger](http://localhost:8080/swagger). To run in the background:

```powershell
docker compose up --build -d
```

View application logs:

```powershell
docker compose logs -f app
```

Stop and remove the containers while preserving the database volume:

```powershell
docker compose down
```

## 7. Tests

Run all tests in the solution:

```powershell
dotnet test ./ReadyToHelpAPI.sln
```

Run only tests with the respective category:

```powershell
dotnet test ./ReadyToHelpAPI.sln --filter "Category=Unit"
dotnet test ./ReadyToHelpAPI.sln --filter "Category=Integration"
```

Integration tests may require PostgreSQL or Docker-backed test infrastructure; check the selected tests' setup and keep Docker available for tests using Testcontainers.

## Additional configuration

- The API resolves the notification service URL from `NOTIFIER_URL`, then `Notifier:BaseUrl`, and finally `http://localhost:5088`. Notification-dependent workflows need that service to be reachable.
- Docker Compose sets `ASPNETCORE_ENVIRONMENT` to `Development`, enabling Swagger.
- The current application explicitly binds to `http://0.0.0.0:5134` in `Program.cs`. To change the listening port, update that setting and keep `Properties/launchSettings.json`, Docker port mappings, and frontend URLs consistent; changing the launch profile alone is insufficient.
- JWT authentication reads `Jwt:Secret`, `Jwt:Issuer`, and `Jwt:Audience` from ASP.NET Core configuration. `Jwt:Secret` must be present for startup. Environment overrides use `Jwt__Secret`, `Jwt__Issuer`, and `Jwt__Audience`.

## Troubleshooting

### Cannot connect to the database

Check that the container is running and port `5432` is available:

```powershell
docker compose ps
docker compose logs postgres
```

Also verify the `POSTGRES_HOST`, `POSTGRES_USERNAME`, and `POSTGRES_PASSWORD` values in the terminal running `dotnet ef` or `dotnet run`. For local PostgreSQL, confirm the service is running and PostGIS is installed in the target database.

### Port already in use

Stop the process using the port or change the host-side port mapping in `docker-compose.yml`. The API container's destination port must remain consistent with the application's listening port, currently `5134`. Avoid running both a local PostgreSQL service and the Compose database on host port `5432`.

### API container starts but cannot be reached

Use `docker compose logs app` to inspect its listening address. Correct the port mapping to `8080:5134` as described in step 7, then recreate the service with `docker compose up --build -d`.

### Swagger does not appear

Confirm the API is running in `Development` and open `/swagger` on the appropriate port:

- Local execution: [http://localhost:5134/swagger](http://localhost:5134/swagger).
- Docker Compose, after correcting the mapping: [http://localhost:8080/swagger](http://localhost:8080/swagger).
