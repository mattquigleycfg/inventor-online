# Azure Integration Testing Guide

This guide explains how to test the Azure Blob Storage and Model Derivative API integration.

## Prerequisites

1. **Application Running**: Make sure the application is running locally:
   ```bash
   cd WebApplication
   dotnet run
   ```

2. **Azure CLI**: Ensure you're logged into Azure CLI (as you mentioned you are)

3. **Valid SAS Token**: Your SAS token is valid until 2026-07-21

## Testing Methods

### Method 1: Web-based Test Page (Recommended)

1. **Open the test page** in your browser:
   ```
   http://localhost:5001/test-azure.html
   ```
   or if using HTTP:
   ```
   http://localhost:5000/test-azure.html
   ```

2. **Run the tests** in order:
   - **Test 1**: Verifies Azure configuration
   - **Test 2**: Lists blobs in your storage
   - **Test 3**: Generates SAS URLs
   - **Test 4**: Creates Forge buckets
   - **Test 5**: Full workflow test
   - **Test 6**: Check translation progress
   - **Test 7**: Azure models API

### Method 2: Using Postman or curl

Test the API endpoints directly:

```bash
# Test 1: Check Azure Configuration
curl http://localhost:5001/api/test/azure/check-config

# Test 2: List all blobs
curl http://localhost:5001/api/test/azure/list-blobs

# Test 3: List SVF blobs only
curl http://localhost:5001/api/test/azure/list-blobs?prefix=svf

# Test 4: Generate SAS URL
curl "http://localhost:5001/api/test/azure/generate-sas?blobName=test.txt"

# Test 5: Create test bucket (POST)
curl -X POST http://localhost:5001/api/test/azure/create-test-bucket

# Test 6: Test full workflow (POST)
curl -X POST "http://localhost:5001/api/test/azure/test-full-workflow?azureBlobName=MRConfigurator.zip"

# Test 7: Check translation progress
curl "http://localhost:5001/api/test/azure/check-translation?urn=YOUR_URN_HERE"

# Test 8: List Azure models
curl http://localhost:5001/api/azuremodels
```

### Method 3: React Component Test

If you want to test the full UI integration:

1. Import the test component in your app
2. Add a button or link to show/hide the `AzureIntegrationTest` component
3. The component includes the full `AzureModelManager` UI

## Testing Scenarios

### Scenario 1: Basic Azure Access
1. Run Test 1 (Check Config) - Should show "configured: true"
2. Run Test 2 (List Blobs) - Should list your blobs
3. Run Test 3 (Generate SAS) - Should return a valid URL

### Scenario 2: Model Translation
1. Ensure you have a file in Azure (e.g., "MRConfigurator.zip")
2. Run Test 5 (Full Workflow) with that filename
3. Copy the returned URN
4. Run Test 6 (Check Translation) with the URN
5. Monitor progress until "status: success"

### Scenario 3: Viewer Integration
1. Once translation is complete, test the viewer:
   ```
   http://localhost:5001/api/azuresvfproxy/svf/YOUR_PATH/bubble.json
   ```

## Expected Results

### Successful Configuration Check:
```json
{
  "status": "success",
  "message": "Azure Blob Service is configured and accessible",
  "isConfigured": true,
  "modelCount": 1,
  "models": [...]
}
```

### Successful Translation Start:
```json
{
  "status": "success",
  "message": "Full workflow test completed",
  "steps": {
    "azureBlobExists": true,
    "bucketCreated": "test-workflow-20240721153025",
    "fileUploaded": "urn:...",
    "translationStarted": "dXJuOi..."
  }
}
```

### Successful Translation Progress:
```json
{
  "status": "success",
  "progress": {
    "status": "success",
    "progress": "100%",
    "hasDerivatives": true
  }
}
```

## Troubleshooting

### Common Issues:

1. **"Azure Blob Service is not configured"**
   - Check `appsettings.json` has the correct SAS token
   - Verify the SAS token hasn't expired

2. **401 Unauthorized on Model Derivative**
   - Check Forge credentials in `appsettings.json`
   - Ensure `clientId` and `clientSecret` are valid

3. **404 Not Found on blob access**
   - Verify the blob name is correct
   - Check if the blob exists using Test 2

4. **CORS errors**
   - These are expected for direct Azure access
   - Use the proxy endpoints instead

### Debug Tips:

1. **Check logs**: 
   ```bash
   # In the terminal running dotnet
   # Look for [Information] and [Error] messages
   ```

2. **Browser Console**: 
   - Press F12 to open developer tools
   - Check the Network tab for failed requests
   - Check the Console tab for JavaScript errors

3. **Azure Portal**:
   - Verify your storage account and container
   - Check the SAS policy permissions
   - Monitor storage metrics

## Next Steps

After successful testing:

1. **Production Deployment**:
   - Set environment variables for sensitive data
   - Use Azure Key Vault for secrets
   - Configure proper CORS policies

2. **Performance Optimization**:
   - Enable caching for frequently accessed files
   - Use CDN for global distribution
   - Implement progress webhooks

3. **Error Handling**:
   - Add retry logic for transient failures
   - Implement proper logging
   - Set up monitoring alerts