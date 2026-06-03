# Azure & Power Platform Permissions Configuration

This document outlines the required Azure Active Directory (Microsoft Entra ID), Azure Key Vault, and Power Platform permissions necessary for the Power Platform API project to function.

---

## 1. Microsoft Entra ID App Registration (Local Development)
For local development, add the following API permissions to your App Registration in the Azure Portal under **App Registrations** > **API Permissions** > **Add a permission**:

### Power Platform & Flow APIs
*   **Microsoft Flow Service** (API Name: `Microsoft Flow Service`)
    *   `Flows.Read.All` (Delegated/Application) — Allows reading flow metadata.
*   **PowerApps Service** (API Name: `PowerApps Service`)
    *   `User` (Delegated) — Allows signing in and reading user profiles.
*   **Power Platform API** (Modern Power Platform Admin/Runtime API)
    *   `Environment.Read.All` (Application/Delegated) — Allows listing environments.
    *   `App.Read.All` (Application/Delegated) — Allows reading app definitions.
    *   `Flow.Read.All` (Application/Delegated) — Allows reading flow definitions.

---

## 2. Azure Key Vault Resource Access
For service-to-service calls, permissions must be assigned directly on the Key Vault resource itself in the Azure Portal. Assign this to your **App Registration** (for local development) and/or your **Managed Identity** (for Azure deployment):

### Option A: Azure RBAC Model (Recommended)
Assign the following built-in role to your App/Identity at the Key Vault, Resource Group, or Subscription scope:
*   **Role:** `Key Vault Secrets User`
*   *Includes:*
    *   `Microsoft.KeyVault/vaults/secrets/getSecret/action` (Read secret values)
    *   `Microsoft.KeyVault/vaults/secrets/readMetadata/action` (List secret properties/metadata)

### Option B: Vault Access Policies Model
Add an access policy on your Key Vault with the following selected permissions:
*   **Secret Permissions:** `Get` and `List`

---

## 3. Power Platform Management Registration
To enable environment discovery via the BAP API (`api.bap.microsoft.com`), you must register the App Registration Client ID (for local development) and/or the Managed Identity App ID (for Azure deployment) as an administrator application in your Power Platform tenant. 

A Power Platform/Tenant Administrator must execute the following PowerShell commands:

```powershell
# 1. Install the administration module
Install-Module -Name Microsoft.PowerApps.Administration.PowerShell

# 2. Authenticate as an administrator
Add-PowerAppsAccount -TenantID <YourTenantID>

# 3. Register the App Client ID or Managed Identity Client ID
New-PowerAppManagementApp -ApplicationId <YourClientID_or_ManagedIdentityClientID>
```

---

## 4. Azure Deployment (Managed Identity)
When deploying this web API to Azure App Service, it is best practice to use **Managed Identities** to eliminate the need for storing client secrets in configuration files. The application's backend automatically falls back to `DefaultAzureCredential` when no client secret is specified.

### Step 1: Enable Managed Identity on App Service
1. In the Azure Portal, navigate to your **App Service**.
2. Under **Settings**, click on **Identity**.
3. Under the **System assigned** tab, switch **Status** to **On** and click **Save**.
4. Take note of the **Object ID** (Service Principal ID) and the **Client ID** of the generated identity.

### Step 2: Grant Resource Permissions to the Managed Identity
*   **For Key Vault:** Assign the `Key Vault Secrets User` role (or Access Policy `Get`/`List` permissions) to the App Service's Managed Identity principal.
*   **For Power Platform Admin API:** Run the PowerShell registration command (shown in Section 3) using the Managed Identity's **Client ID** as the `-ApplicationId`.

### Step 3: Configure Environment Variables in App Service
In the Azure Portal, navigate to **App Service** > **Settings** > **Configuration** > **Application settings** (or Environment variables) and add:
*   `AzureAd:TenantId` = `<Your Tenant ID>`
*   `AzureAd:ClientId` = (Only required if using a User-Assigned Managed Identity. Leave empty/undefined for System-Assigned Managed Identity)
*   `AzureAd:ClientSecret` = (Leave empty/undefined. Do not store secrets here!)
