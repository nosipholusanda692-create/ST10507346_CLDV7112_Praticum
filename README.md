# AgriStream IoT - Azure solution

ASP.NET Core 8 MVC web app + 4 Azure Functions (isolated worker) + Azure SQL, Blob, Table and Queue storage.

```
AgriStream/
  AgriStream.Web/                 <- (a)(b)(c) MVC app: login/roles (SQL), media upload (Blob), telemetry view (Table)
  AgriStream.Functions/           <- (d) Blob trigger, HTTP GET, HTTP POST, Queue trigger
  sql/schema.sql                  <- (b) optional manual schema (the app also creates it)
  loadtest/loadtest.js            <- (e) k6 script for Azure Load Testing
  scripts/azure-setup.sh          <- optional CLI version of the portal steps (incl. autoscale + alerts)
```

## Data flow
```
sensors / web app --POST--> HTTP function --> Storage Queue --> Queue function --> Table Storage
drone images ---> Web app --> Blob container "farm-media" --> Blob function --> MediaLog table
Web app / GET function <---------------------------------------- Table Storage
```

## Quick local run
1. Install .NET 8 SDK and Azure Functions Core Tools v4.
2. Fill in `AgriStream.Web/appsettings.json` and `AgriStream.Functions/local.settings.json`
   (SQL connection string and storage connection string).
3. Terminal 1: `cd AgriStream.Functions && func start`
4. Terminal 2: `cd AgriStream.Web && dotnet run`
5. Sign in with `admin` / `Admin@12345` (change `Seed:AdminPassword` first for anything real).

Full Azure deployment steps are in the PDF.
