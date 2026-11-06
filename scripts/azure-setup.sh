#!/usr/bin/env bash
# AgriStream IoT - OPTIONAL Azure CLI version of the portal steps in the PDF guide.
# Run in Azure Cloud Shell (Bash) or any shell where you have done `az login`.
# Edit the variables first. Your SQL Database and Storage Account already exist, so this script
# only creates: Log Analytics + Application Insights, App Service plan + Web App, Function App,
# app settings, autoscale rules and alerts.
set -euo pipefail

# ---------- EDIT THESE ----------
RG="rg-agristream"                  # resource group that already holds your SQL DB + storage account
LOC="southafricanorth"              # use the same region as your existing resources
STORAGE="<your-storage-account>"    # existing storage account name
SQL_CONN="<your-sql-connection-string>"
STORAGE_CONN="<your-storage-connection-string>"
SUFFIX="$RANDOM"                    # makes names globally unique
WEBAPP="agristream-web-$SUFFIX"
FUNCAPP="agristream-func-$SUFFIX"
PLAN="plan-agristream"
LAW="law-agristream"
AI="ai-agristream"
ALERT_EMAIL="<you@example.com>"
# --------------------------------

echo "== Log Analytics + Application Insights =="
az monitor log-analytics workspace create -g "$RG" -n "$LAW" -l "$LOC" >/dev/null
LAW_ID=$(az monitor log-analytics workspace show -g "$RG" -n "$LAW" --query id -o tsv)
az extension add --name application-insights --only-show-errors 2>/dev/null || true
az monitor app-insights component create --app "$AI" -l "$LOC" -g "$RG" --workspace "$LAW_ID" --kind web >/dev/null
AI_CONN=$(az monitor app-insights component show --app "$AI" -g "$RG" --query connectionString -o tsv)

echo "== (a) App Service plan (S1 - required for autoscale) + Web App =="
az appservice plan create -g "$RG" -n "$PLAN" -l "$LOC" --sku S1 --is-linux >/dev/null
az webapp create -g "$RG" -p "$PLAN" -n "$WEBAPP" --runtime "DOTNETCORE:8.0" >/dev/null
az webapp config set -g "$RG" -n "$WEBAPP" --always-on true --generic-configurations '{"healthCheckPath": "/health"}' >/dev/null

echo "== (d) Function App (Consumption, .NET 8 isolated) =="
az functionapp create -g "$RG" -n "$FUNCAPP" --storage-account "$STORAGE" \
  --consumption-plan-location "$LOC" --runtime dotnet-isolated --runtime-version 8.0 \
  --functions-version 4 --os-type Linux --app-insights "$AI" >/dev/null
az functionapp config appsettings set -g "$RG" -n "$FUNCAPP" \
  --settings "StorageConnection=$STORAGE_CONN" >/dev/null

echo "== Web App settings =="
az webapp config appsettings set -g "$RG" -n "$WEBAPP" --settings \
  "ConnectionStrings__SqlDb=$SQL_CONN" \
  "Storage__ConnectionString=$STORAGE_CONN" \
  "Functions__BaseUrl=https://$FUNCAPP.azurewebsites.net/api" \
  "APPLICATIONINSIGHTS_CONNECTION_STRING=$AI_CONN" \
  "Seed__AdminPassword=ChangeMe!12345" >/dev/null

echo "== (g) Autoscale for the App Service plan =="
az monitor autoscale create -g "$RG" --resource "$PLAN" --resource-type Microsoft.Web/serverfarms \
  -n "autoscale-$PLAN" --min-count 1 --max-count 5 --count 1 >/dev/null
az monitor autoscale rule create -g "$RG" --autoscale-name "autoscale-$PLAN" \
  --condition "CpuPercentage > 70 avg 5m" --scale out 1 >/dev/null
az monitor autoscale rule create -g "$RG" --autoscale-name "autoscale-$PLAN" \
  --condition "CpuPercentage < 30 avg 10m" --scale in 1 >/dev/null

echo "== (f) Alerts =="
az monitor action-group create -g "$RG" -n ag-agristream --short-name agri \
  --action email admin "$ALERT_EMAIL" >/dev/null
WEB_ID=$(az webapp show -g "$RG" -n "$WEBAPP" --query id -o tsv)
az monitor metrics alert create -g "$RG" -n "web-http5xx" --scopes "$WEB_ID" \
  --condition "total Http5xx > 10" --window-size 5m --evaluation-frequency 1m \
  --action ag-agristream --description "More than 10 server errors in 5 minutes" >/dev/null
PLAN_ID=$(az appservice plan show -g "$RG" -n "$PLAN" --query id -o tsv)
az monitor metrics alert create -g "$RG" -n "plan-high-cpu" --scopes "$PLAN_ID" \
  --condition "avg CpuPercentage > 85" --window-size 5m --evaluation-frequency 1m \
  --action ag-agristream --description "App Service plan CPU above 85%" >/dev/null

echo
echo "Done. Web app:  https://$WEBAPP.azurewebsites.net"
echo "      Function: https://$FUNCAPP.azurewebsites.net/api"
echo "Next: deploy code (see PDF section 7), then copy the function key into Web App setting Functions__Key."
