# AGENT.md — Power Platform Flow Discovery

## Project Overview

This repository contains two projects that together form a Power Platform monitoring and discovery tool:

- **`PowerPlatformApi/`** — ASP.NET Core 8 Web API: proxies calls to the Microsoft Power Platform and Azure Key Vault REST APIs using client-credential authentication via an Azure AD App Registration.
- **`power-platform-ui/`** — Angular 16 SPA: presents environments, flows, flow definitions, and Key Vault secrets in a browser UI.

The two projects communicate over HTTP. The Angular app points at `https://localhost:5001/api` in development.

---

## Architecture

```
Browser (Angular 16)
    │
    │  HTTP (REST/JSON)
    ▼
ASP.NET Core 8 API  (PowerPlatformApi)
    │                        │
    ▼                        ▼
Power Platform          Azure Key Vault
(BAP / Flow API)        (existing official project Access Policy)
```

Authentication is handled entirely in the backend via an **Azure AD App Registration** using the **OAuth 2.0 Client Credentials** (app-only) flow. The Angular frontend never holds tokens.

**Key Vault access** uses the **existing official project's Key Vault Access Policy** — the App Registration service principal is added to that policy with `Get` and `List` secret permissions. No new Key Vault or policy is created.

---

## Backend — PowerPlatformApi

### Stack & Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 8.0.5 | JSON serialization (NullValueHandling.Ignore) |
| `Microsoft.Identity.Client` 4.61.3 | MSAL — client-credential token acquisition |
| `Microsoft.Extensions.Http.Polly` 8.0.5 | Retry + circuit-breaker on all `HttpClient`s |
| `Swashbuckle.AspNetCore` 6.6.2 | Swagger/OpenAPI UI (served at `/`) |
| `Newtonsoft.Json` 13.0.3 | JSON parsing of upstream API responses |

Target framework: `net8.0`. Nullable reference types enabled.

### Configuration (`appsettings.json`)

```json
{
  "AzureAd": {
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "KeyVaults": {
    "Global": [
      "https://<vault-name>.vault.azure.net/"
    ],
    "<EnvironmentId>": [
      "https://<env-specific-vault>.vault.azure.net/"
    ]
  }
}
```

- `AzureAd` — App Registration credentials. `ClientSecret` is required (see Authentication below).
- `KeyVaults:Global` — vault URIs queried for every environment.
- `KeyVaults:{EnvironmentId}` — vault URIs scoped to a specific Power Platform environment ID.
- `AzureAd:SubscriptionId` — present in the default template but not used by the current code; can be omitted.

### Authentication

`PowerPlatformService` and `KeyVaultService` both use MSAL `ConfidentialClientApplicationBuilder` (client credentials) when `ClientSecret` is set and not a placeholder. If `ClientSecret` is empty/placeholder, `PowerPlatformService` and the vault access path in `KeyVaultService` fall back to `DefaultAzureCredential` (Managed Identity). The BAP API calls inside `KeyVaultService.GetAccessTokenAsync` always require `ClientSecret`.

### Resilience (Program.cs)

Both `HttpClient`s registered via DI apply:
- **Retry**: 3 attempts, exponential back-off (2s, 4s, 8s), also triggered on HTTP 429.
- **Circuit breaker**: opens after 5 consecutive failures, resets after 30s.

### Controllers

#### `GET /api/environments`
Returns all Power Platform environments visible to the service principal.

**Response:** `EnvironmentModel[]`
```json
[{ "environmentId": "...", "environmentName": "...", "location": "...", "type": "..." }]
```

#### `GET /api/flows/environment/{envId}`
Returns all flows in the given environment.

**Response:** `FlowModel[]`
```json
[{ "flowId": "...", "flowName": "...", "state": "Started|Stopped" }]
```

#### `GET /api/flows/{envId}/{flowId}`
Returns the full flow definition (Logic App workflow JSON) for a single flow.

**Response:** `FlowDefinitionModel`
```json
{ "properties": { "definition": { ... } } }
```

#### `GET /api/keyvault/secrets`
Lists secret names, metadata, and values from configured Key Vaults. Vault URIs are read from `appsettings.json` (`KeyVaults:Global` / `KeyVaults:{envId}`). Uses the existing official project Key Vault Access Policy.

**Response:** `KeyVaultSecret[]`
```json
[{
  "secretName": "...", "secretValue": "...", "vaultName": "...",
  "environmentName": "...", "environmentId": "...",
  "contentType": "...", "enabled": true, "expiresOn": "..."
}]
```

### Services

#### `PowerPlatformService` (`IPowerPlatformService`)
- `GetEnvironmentsAsync()` — calls `https://api.bap.microsoft.com/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments?api-version=2020-10-01`
- `GetFlowsAsync(environmentId)` — calls `https://api.flow.microsoft.com/providers/Microsoft.ProcessSimple/environments/{envId}/flows?api-version=2016-11-01`
- `GetFlowDefinitionAsync(environmentId, flowId)` — same base with `/{flowId}`

In-memory token cache keyed by OAuth scope with 5-minute expiry buffer.

#### `KeyVaultService` (`IKeyVaultService`)
- `GetAllSecretsAsync()` — two-step:
  1. Calls `GetEnvironmentsInternalAsync()` via the BAP API to get environment list.
  2. Resolves vault URIs per environment from config (`GetVaultUrisForEnvironment`), then fans out with `Task.WhenAll` using the Azure SDK `SecretClient`.
- Calls `GetPropertiesOfSecretsAsync` (list) then `GetSecretAsync` (get value) — both permissions needed on the Key Vault policy.

### Models

| Class | Fields |
|---|---|
| `EnvironmentModel` | `EnvironmentId`, `EnvironmentName`, `Location`, `Type` |
| `FlowModel` | `FlowId`, `FlowName`, `State` |
| `FlowDefinitionModel` | `Properties.Definition` (raw `object`) |
| `KeyVaultSecret` | `SecretName`, `SecretValue`, `VaultName`, `EnvironmentName`, `EnvironmentId`, `ContentType`, `Enabled`, `ExpiresOn` |

---

## Frontend — power-platform-ui

### Stack

- Angular **16.2** (NgModules), RxJS 7.8, TypeScript, Zone.js 0.13

### Running

```bash
cd power-platform-ui
npm install
ng serve          # http://localhost:4200
```

API base URL is in `src/environments/environment.ts`:
```ts
export const environment = {
  production: false,
  apiUrl: 'https://localhost:5001/api'
};
```

### Routing

| Path | Component | Description |
|---|---|---|
| `/` | → `/environments` | Default redirect |
| `/environments` | `EnvironmentsComponent` | Lists all Power Platform environments |
| `/flows/:envId` | `FlowsComponent` | Lists flows for an environment |
| `/flow-definition/:envId/:flowId` | `FlowDefinitionComponent` | Shows flow definition JSON |
| `/all-flows` | `AllFlowsComponent` | Aggregates flows across all environments |
| `/keyvaults` | `KeyVaultsComponent` | Lists Key Vault secret metadata |
| `**` | → `/environments` | Catch-all redirect |

### Service — `PowerPlatformService`

Singleton (`providedIn: 'root'`). All methods return `Observable<T>` with a `catchError` fallback to hardcoded dummy data when the API is unreachable — the UI works for demo/dev without a running backend.

| Method | Endpoint | Return type |
|---|---|---|
| `getEnvironments()` | `GET /api/environments` | `Observable<Environment[]>` |
| `getFlows(envId)` | `GET /api/flows/environment/{envId}` | `Observable<Flow[]>` |
| `getFlowDefinition(envId, flowId)` | `GET /api/flows/{envId}/{flowId}` | `Observable<FlowDefinition>` |
| `getKeyVaultSecrets()` | `GET /api/keyvault/secrets` | `Observable<KeyVaultSecret[]>` |

### Models (`src/app/models/power-platform.models.ts`)

```ts
interface Environment    { environmentId, environmentName, location, type }
interface Flow           { flowId, flowName, state }
interface FlowDefinition { properties: { definition: any } }
interface KeyVaultSecret { secretName, secretValue, vaultName, environmentName, environmentId, contentType?, enabled, expiresOn? }
```

### Components

| Component | Route | Responsibility |
|---|---|---|
| `AppComponent` | shell | Nav + `<router-outlet>` |
| `EnvironmentsComponent` | `/environments` | Load and list environments |
| `FlowsComponent` | `/flows/:envId` | Load flows for route param `envId` |
| `FlowDefinitionComponent` | `/flow-definition/:envId/:flowId` | Render flow definition JSON |
| `AllFlowsComponent` | `/all-flows` | Aggregate flows across all environments |
| `KeyVaultsComponent` | `/keyvaults` | Display Key Vault secret metadata |

---

## Development Setup

### Prerequisites

- .NET 8 SDK
- Node.js ≥ 18, npm
- Angular CLI (`npm install -g @angular/cli`)
- Azure AD App Registration with permissions documented in `permissions.md`
- Access added to the official project's existing Key Vault Access Policy

### Backend

```bash
cd PowerPlatformApi
# fill appsettings.json or use dotnet user-secrets
dotnet run
# Swagger UI: https://localhost:5001
```

### Frontend

```bash
cd power-platform-ui
npm install
ng serve
# App: http://localhost:4200
```

When the backend is not running, the Angular service returns built-in dummy data automatically.

---

## Security Notes

- `ClientSecret` must not be committed to source control. Use `dotnet user-secrets` locally or environment variables in production.
- The CORS policy (`AllowAll`) should be tightened for production deployments.
- The API has no authentication of its own — it should sit behind a gateway or network boundary in production.
- Key Vault access uses the existing official project Access Policy (`Get` + `List`). Secret values are retrieved by the API — ensure network and role boundaries are appropriate for your environment.
