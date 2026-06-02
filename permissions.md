# Azure & Power Platform Permissions Configuration

This document outlines the required Azure Active Directory (Microsoft Entra ID), Azure Key Vault, and Power Platform permissions necessary for the Power Platform API project to function.

---

## 1. Microsoft Entra ID App Registration API Permissions
Add the following API permissions to your App Registration in the Azure Portal under **App Registrations** > **API Permissions** > **Add a permission**:

### Power Platform & Flow APIs
*   **Microsoft Flow Service** (API Name: `Microsoft Flow Service`)
    *   `Flows.Read.All` (Delegated/Application) — Allows reading flow metadata.
*   **PowerApps Service** (API Name: `PowerApps Service`)
    *   `User` (Delegated) — Allows signing in and reading user profiles.
*   **Power Platform API** (Modern Power Platform Admin/Runtime API)
    *   `Environment.Read.All` (Application/Delegated) — Allows listing environments.
    *   `App.Read.All` (Application/Delegated) — Allows reading app definitions.
    *   `Flow.Read.All` (Application/Delegated) — Allows reading flow definitions.

### Azure Key Vault (Optional App Registration Permission)
*   **Azure Key Vault** (API Name: `Azure Key Vault`)
    *   `user_impersonation` (Delegated) — Required only if retrieving secrets in the context of the logged-in user.

---

## 2. Azure Key Vault Resource Access
For daemon / service-to-service calls using client credentials flow (Service Principal client secret), permissions must be assigned directly on the Key Vault resource itself in the Azure Portal:

### Option A: Azure RBAC Model (Recommended)
Assign the following built-in role to your App Registration (Service Principal) at the Key Vault, Resource Group, or Subscription scope:
*   **Role:** `Key Vault Secrets User`
*   *Includes:*
    *   `Microsoft.KeyVault/vaults/secrets/getSecret/action` (Read secret values)
    *   `Microsoft.KeyVault/vaults/secrets/readMetadata/action` (List secret properties/metadata)

### Option B: Vault Access Policies Model
Add an access policy on your Key Vault with the following selected permissions:
*   **Secret Permissions:** `Get` and `List`

---

## 3. Power Platform Management Registration
To enable environment discovery via the BAP API (`api.bap.microsoft.com`), you must register the App Registration (Service Principal) as an administrator application in your Power Platform tenant. 

A Power Platform/Tenant Administrator must execute the following PowerShell commands:

```powershell
# 1. Install the administration module
Install-Module -Name Microsoft.PowerApps.Administration.PowerShell

# 2. Authenticate as an administrator
Add-PowerAppsAccount -TenantID <YourTenantID>

# 3. Register the Service Principal Client ID
New-PowerAppManagementApp -ApplicationId <YourClientID>
```
