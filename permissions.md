# Azure & Power Platform Permissions Configuration

This document outlines the required Azure Active Directory (Microsoft Entra ID), Azure Key Vault, and Power Platform permissions necessary for the Power Platform API project to function.

> **Auth model:** The application uses the **OAuth 2.0 Client Credentials** (app-only) flow via MSAL with an **Azure AD App Registration**. There is no user sign-in. All permissions below must be granted as **Application** permissions, not Delegated.
>
> **Key Vault:** The official project's existing **Key Vault Access Policy** is used — no new policy is required. The App Registration's service principal must be added to that existing policy with `Get` and `List` secret permissions.

---

## 1. Microsoft Entra ID App Registration

Add the following **Application** permissions in the Azure Portal under **App Registrations → API Permissions → Add a permission**. Grant admin consent after adding each permission.

### Power Platform & Flow APIs

| API | Permission | Type | Purpose |
|---|---|---|---|
| **Microsoft Flow Service** (`api.flow.microsoft.com`) | `Flows.Read.All` | Application | List and read flow metadata and definitions |
| **Power Platform API** (`api.bap.microsoft.com`) | `Environment.Read.All` | Application | List Power Platform environments |
| **Power Platform API** (`api.bap.microsoft.com`) | `Flow.Read.All` | Application | Read flow definitions per environment |

> **Note:** Do not add `PowerApps Service → User (Delegated)` or `App.Read.All` — these are not used by the application.

---

## 2. Azure Key Vault Access

The application uses the Azure SDK `SecretClient` with `ClientSecretCredential` (or `DefaultAzureCredential` as fallback). It calls both `GetPropertiesOfSecretsAsync` (list) and `GetSecretAsync` (get value), so both `Get` and `List` secret permissions are required.

### Current Setup — Existing Official Project Access Policy

The official project already has a Key Vault Access Policy in place. The App Registration's service principal must be added to that existing policy:

1. In the Azure Portal, navigate to your **Key Vault → Access policies**.
2. Click **Create** (or **Add Access Policy** on older vaults).
3. Under **Secret permissions**, select `Get` and `List`.
4. Under **Principal**, search for and select your **App Registration** by name or Client ID.
5. Click **Save**.

> No new Key Vault or policy needs to be created — simply add the App Registration to the existing official project policy.

---

## 3. Power Platform Management Registration

To allow environment discovery via the BAP API (`api.bap.microsoft.com`), the app's Client ID must be registered as an administrator application in your Power Platform tenant. A Power Platform/Tenant Administrator must run:

```powershell
# 1. Install the administration module
Install-Module -Name Microsoft.PowerApps.Administration.PowerShell

# 2. Authenticate as an administrator
Add-PowerAppsAccount -TenantID <YourTenantID>

# 3. Register the App Client ID or Managed Identity Client ID
New-PowerAppManagementApp -ApplicationId <YourClientID_or_ManagedIdentityClientID>
```

---

## 4. Key Vault URI Configuration (Required)

The application does **not** auto-discover Key Vaults via Azure Resource Manager. Vault URIs must be explicitly configured in `appsettings.json`. Add one or both of the following sections:

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

- `KeyVaults:Global` — vaults queried for every environment.
- `KeyVaults:{EnvironmentId}` — vaults scoped to a specific Power Platform environment ID.

> **Note:** `AzureAd:SubscriptionId` appears in the default `appsettings.json` but is not used by the current codebase. It can be omitted.

---

## 5. Azure Deployment (Managed Identity)

When deploying to Azure App Service, you can eliminate the stored `ClientSecret` using **Managed Identity**, but support is partial — see the table below.

| Service | Managed Identity supported? | Notes |
|---|---|---|
| `PowerPlatformService` | ✅ Yes | Falls back to `DefaultAzureCredential` when `ClientSecret` is empty or placeholder |
| `KeyVaultService` — vault access | ✅ Yes | Uses `DefaultAzureCredential` when `ClientSecret` is empty or placeholder |
| `KeyVaultService` — BAP/environment calls | ❌ No | `GetAccessTokenAsync` in `KeyVaultService` always uses MSAL client credentials; `ClientSecret` is required |

**Practical guidance:** `ClientSecret` is required as long as `KeyVaultService` makes BAP API calls. For full Managed Identity support, `KeyVaultService.GetAccessTokenAsync` would need to be updated to include the same `DefaultAzureCredential` fallback that `PowerPlatformService` uses.

### Step 1: Enable Managed Identity on App Service

1. Navigate to your **App Service → Settings → Identity**.
2. Under **System assigned**, switch **Status** to **On** and click **Save**.
3. Note the **Object ID** (Service Principal ID) and **Client ID**.

### Step 2: Grant Permissions to the Managed Identity

- **Key Vault:** Assign the `Key Vault Secrets User` role (or Access Policy `Get`/`List`) to the Managed Identity.
- **Power Platform:** Run the PowerShell command in Section 3 using the Managed Identity's **Client ID**.

### Step 3: Configure App Service Application Settings

| Setting | Value |
|---|---|
| `AzureAd:TenantId` | Your Tenant ID |
| `AzureAd:ClientId` | Your App Registration Client ID |
| `AzureAd:ClientSecret` | Required (see table above) |
| `KeyVaults:Global` | JSON array of vault URIs, e.g. `["https://myvault.vault.azure.net/"]` |
