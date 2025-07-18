# Design Automation v3 Deployment Guide

## Azure App Service Environment Variables

Based on the credentials.md file, ensure the following environment variables are set in your Azure App Service:

### Forge Configuration
```
Forge__AuthenticationAddress=https://developer.api.autodesk.com
Forge__clientId=bHUc6d6kNBuT5tAJZk70A8zNcMyM19xo
Forge__clientSecret=G7edb3afa909f437
Forge__DesignAutomation__BaseAddress=https://developer.api.autodesk.com/da/us-east/v3/
```

### Azure Storage Configuration
```
ConnectionStrings__AzureStorage=DefaultEndpointsProtocol=https;AccountName=YOUR_STORAGE_ACCOUNT;AccountKey=YOUR_ACCOUNT_KEY;BlobEndpoint=https://YOUR_STORAGE_ACCOUNT.blob.core.windows.net/;QueueEndpoint=https://YOUR_STORAGE_ACCOUNT.queue.core.windows.net/;TableEndpoint=https://YOUR_STORAGE_ACCOUNT.table.core.windows.net/;FileEndpoint=https://YOUR_STORAGE_ACCOUNT.file.core.windows.net/;

AzureBlobStorage__BaseUrl=https://YOUR_STORAGE_ACCOUNT.blob.core.windows.net/models
AzureBlobStorage__SasToken=YOUR_SAS_TOKEN_HERE
AzureBlobStorage__ContainerName=models
```

## Deployment Steps

1. **Update Environment Variables**
   - Navigate to Azure Portal → App Services → conform3d
   - Go to Configuration → Application settings
   - Update the environment variables above (especially the v3 endpoint)

2. **Run Migration Script**
   ```powershell
   .\migrate-to-v3.ps1 -ClientId "bHUc6d6kNBuT5tAJZk70A8zNcMyM19xo" -ClientSecret "G7edb3afa909f437" -DryRun
   ```
   
   If dry run looks good, run without -DryRun:
   ```powershell
   .\migrate-to-v3.ps1 -ClientId "bHUc6d6kNBuT5tAJZk70A8zNcMyM19xo" -ClientSecret "G7edb3afa909f437"
   ```

3. **Deploy Application**
   - Deploy the updated application code to Azure App Service
   - Monitor logs for any issues

4. **Verify Configuration**
   - Test Azure Blob Storage connectivity: `https://conform3d.azurewebsites.net/api/azure-standalone/test`
   - Test Design Automation endpoints
   - Verify all functionality works with v3 APIs

## Key Changes Made

### Configuration Updates
- ✅ Azure Storage connection string added
- ✅ SAS token updated with current token from credentials.md
- ✅ Forge DesignAutomation BaseAddress updated to v3
- ✅ MRConfigurator project URL updated with current SAS token

### Code Updates
- ✅ WorkItem.Arguments → WorkItem.Inputs/Outputs (v3 format)
- ✅ Engine path updated: `$(engine.path)` → `$(appbundles[0].engine.path)`
- ✅ Azure Blob Storage SDK integrated
- ✅ OSS header patching removed (now handled automatically)
- ✅ Publisher tests updated for v3 format

### Important Notes

1. **SAS Token Expiration**: The current SAS token expires on 2025-07-18T09:30:44Z. You'll need to generate a new one before it expires.

2. **Environment Variables vs appsettings.json**: Environment variables in Azure App Service override appsettings.json values, so make sure to update both.

3. **Storage Account**: All references now use the correct `conform3d` storage account name.

4. **v3 Endpoint**: The application now uses Design Automation v3 endpoints exclusively.

## Troubleshooting

If you encounter issues:

1. Check Azure Application Insights logs
2. Verify environment variables are set correctly
3. Ensure SAS token has not expired
4. Test Azure Blob Storage connectivity independently
5. Check Design Automation v3 endpoint accessibility

## Rollback Plan

If needed, you can rollback by:
1. Reverting the DesignAutomation BaseAddress to v2: `https://developer.api.autodesk.com/da/us-east/`
2. Deploying the previous version of the application
3. The migration script only removes aliases, so existing v2 resources should still work 