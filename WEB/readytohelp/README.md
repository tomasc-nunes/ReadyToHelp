# ReadyToHelp Web

Angular frontend for ReadyToHelp, with an occurrence map, occurrence details and history, login, and user management.

See the [project overview](../../README.md) and [API setup guide](../../API/README.md) for the other components. Commands below run from `WEB/readytohelp` unless stated otherwise.

## Prerequisites

Install:

- [Git](https://git-scm.com/downloads).
- [Node.js](https://nodejs.org/en/download) **22.x**, including npm. This version is compatible with the project's Angular 19.2 dependencies; see the [Angular compatibility table](https://angular.dev/reference/versions).
- Google Chrome for the Karma unit tests, and a browser with WebGL support for the map.
- Access to a ReadyToHelp API and a working Mapbox public access token for map features.

Angular CLI, TypeScript, Cypress, and the other JavaScript dependencies are installed locally by npm. A global Angular CLI installation is unnecessary.

Verify your tools:

```powershell
git --version
node --version
npm --version
```

## 1. Get the code

From the directory where you want to store the project:

```powershell
git clone https://gitlab.com/grupo_07-lds-2526/lds_25_26.git ReadyToHelp
cd ReadyToHelp/WEB/readytohelp
```

For an existing checkout, open a terminal in `WEB/readytohelp`.

## 2. Install dependencies

```powershell
npm ci
npx ng version
```

`npm ci` installs the versions recorded in `package-lock.json`. It also installs the Cypress binary, so the first installation needs internet access and may take several minutes. Use `npm install` when intentionally changing dependencies, and commit the resulting lockfile changes with `package.json`.

## 3. Configure the API

The application currently has hardcoded service URLs pointing to `https://readytohelp-api.up.railway.app`. There is no `.env` loader, Angular environment-file configuration, or development proxy configured for these URLs.

### Use the hosted API

Leave the service URLs unchanged. Availability and account access depend on the hosted service. Requests made through the application affect that backend's data.

### Use a local API

First follow the [API README](../../API/README.md) to install the backend tools, start PostgreSQL/PostGIS, apply migrations, and run the API. Verify [local Swagger](http://localhost:5134/swagger) opens before starting the frontend.

Update the following values, preserving each endpoint path:

| File | Property | Local value |
| --- | --- | --- |
| `src/app/services/auth.service.ts` | `baseAuthUrl` | `http://localhost:5134/api/auth` |
| `src/app/services/occurrence.service.ts` | `apiUrl` | `http://localhost:5134/api/occurrence` |
| `src/app/services/user.service.ts` | `apiUrl` | `http://localhost:5134/api/user` |

If you expose the API on another port, change all three URLs consistently. For a containerized API, see the port-mapping note in the API guide before using port `8080`.

The backend currently enables CORS through its `AllowAllOrigins` policy. If using another deployment, its CORS configuration must allow your frontend origin, normally `http://localhost:4200`.

## 4. Configure maps

Mapbox tokens are currently defined in two source files:

- `src/app/components/map/map.component.ts`: the map's `accessToken` option.
- `src/app/components/occurence-detail/occurence-detail.component.ts`: `mapboxAccessToken` for static map images.

For your own setup, replace both with a public token from your [Mapbox account](https://account.mapbox.com/). Ensure any URL restrictions allow your development origin and eventual deployment origin. Mapbox requests require internet access even when the API is local. Do not put a secret Mapbox token in browser code.

## 5. Start the development server

```powershell
npm start
```

Open [http://localhost:4200](http://localhost:4200). The root route redirects to `/map`, and source changes trigger a reload. Stop the server with `Ctrl+C`.

To use a different port:

```powershell
npm start -- --port 4300
```

Useful routes:

| Route | Purpose |
| --- | --- |
| `/map` | Occurrence map |
| `/login` | Login form |
| `/occurrence/:id` | Details for an existing occurrence |
| `/occurrences/history` | Occurrence history |
| `/users` | User management; protected by an authentication guard |

Use an account accepted by the selected backend's web login endpoint. Credentials in mocked Cypress tests are not seeded application accounts. After switching backends, sign out and sign in again so a token from the previous backend is not reused.

## 6. Build the application

```powershell
npm run build
```

The default build configuration is `production`, and output is written to `dist/readytohelp/`. For development builds:

```powershell
npm run build -- --configuration development
npm run watch
```

`watch` rebuilds files when they change; use `npm start` when you also need a development server.

For deployment, serve the generated files with a static web server and configure an `index.html` fallback for Angular routes. API addresses and Mapbox tokens are compiled into the bundle, so configure them before building. An HTTPS deployment needs an HTTPS API to avoid mixed-content blocking.

The current build and serve targets both load `cypress-coverage.webpack.js`, including production builds. Review that coverage instrumentation when preparing a release. Production bundle and component-style size budgets are defined in `angular.json`; exceeding an error budget fails the build.

## 7. Run tests

### Unit tests: Jasmine and Karma

```powershell
npm test
```

This starts the test runner in watch mode. For a single headless run and optional coverage:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless
npm test -- --watch=false --browsers=ChromeHeadless --code-coverage
```

Chrome must be installed. Coverage output is placed under `coverage/`.

### End-to-end tests: Cypress

Option A: let the Angular Cypress target start the development server and open Cypress:

```powershell
npm run e2e
```

Option B: run `npm start` in one terminal, then use another terminal in `WEB/readytohelp`:

```powershell
npm run cypress:open
```

For a headless run against that running server:

```powershell
npm run cypress:run
```

The standalone Cypress scripts do not start Angular. `cypress.config.ts` expects `http://localhost:4200`. If using port `4300`, override it:

```powershell
npm run cypress:run -- --config baseUrl=http://localhost:4300
```

Tests live in `cypress/e2e/` and cover login, maps, navigation, occurrence details/history, and user management. Some requests are mocked; do not assume every test is isolated from the configured API. Use a development backend for test runs that send real requests. Coverage uses the Istanbul webpack loader and `@cypress/code-coverage` tasks.

## Project structure

```text
src/app/components/          Screens and shared components
src/app/services/            API calls and authentication state
src/app/guards/              Route guards
src/app/app.routes.ts        Application routes
src/app/app.config.ts        Application providers
src/assets/ and public/      Static assets
cypress/e2e/                 End-to-end tests
cypress.config.ts            Cypress URL and coverage tasks
cypress-coverage.webpack.js  Source instrumentation
angular.json                Build, serve, and test targets
package.json                Scripts and dependency ranges
package-lock.json           Locked dependency versions
```

To generate a component using the installed CLI:

```powershell
npx ng generate component components/component-name
```

## Troubleshooting

- **`npm` is not recognized:** install Node.js and reopen the terminal. In PowerShell, if script execution blocks `npm.ps1`, use `npm.cmd` (and `npx.cmd`) for the same commands.
- **Dependency or CLI version errors:** confirm Node.js 22.x is active, run `npm ci`, and use the local CLI through npm scripts or `npx ng`.
- **Port 4200 is occupied:** stop the previous server or use `npm start -- --port 4300`; update Cypress's base URL accordingly.
- **API requests fail:** check the browser Network tab, all three service URLs, the backend process, database migrations, and CORS. A frontend build does not start the backend.
- **Login returns 401/403:** verify the account and its permissions on the selected backend; sign out after changing backend URLs.
- **Map or static preview is blank:** check both public tokens, their origin restrictions, internet access, and WebGL support.
- **Karma cannot find Chrome:** install Chrome or set `CHROME_BIN` to its executable path in the test terminal.
- **Cypress cannot connect:** start Angular first for `cypress:open`/`cypress:run`, and check the configured base URL. If its binary is missing, run `npx cypress install` followed by `npx cypress verify`.
