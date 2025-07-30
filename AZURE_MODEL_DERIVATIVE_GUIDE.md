# Azure Model Derivative Integration Guide

## Overview

This guide explains how to process files stored in Azure Blob Storage using the Autodesk Model Derivative API. This integration allows you to:
- Store your Inventor files in Azure Blob Storage
- Process them through Autodesk's cloud services
- View them in the Autodesk Viewer without manual SVF extraction

## Architecture

```
Azure Blob Storage → Your Server → Forge OSS → Model Derivative API → Viewer
     (ZIP files)      (Download)    (Upload)     (Translation)        (URN)
```

## Setup Requirements

### 1. Azure Configuration
Ensure your Azure Blob Storage is configured in `appsettings.Local.json`:
```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=..."
  },
  "AzureBlobStorage": {
    "ContainerName": "models"
  }
}
```

### 2. Autodesk Forge Configuration
Ensure your Forge credentials are set:
```json
{
  "Forge": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

## Using the Integration

### Option 1: Web Interface

1. Navigate to the Azure Model Derivative Manager page
2. You'll see a list of all files in your Azure storage
3. Click "Process" next to any ZIP file to start translation
4. Monitor the status (Starting → Processing → Ready)
5. Click "View 3D" when the model is ready

### Option 2: API Endpoints

#### Process a Single File
```http
POST /api/azure/model-derivative/process
Content-Type: application/json

{
  "blobName": "MRConfigurator.zip",
  "bucketKey": "optional-custom-bucket" 
}
```

Response:
```json
{
  "success": true,
  "urn": "dXJuOmFkc2sub2JqZWN0czpvcy5vYmplY3Q6...",
  "status": "inprogress",
  "message": "Translation job started successfully"
}
```

#### Check Translation Status
```http
GET /api/azure/model-derivative/status/{urn}
```

Response:
```json
{
  "status": "complete",
  "progress": "100% complete",
  "hasDerivatives": true,
  "complete": true,
  "failed": false
}
```

#### Get Viewable URN
```http
GET /api/azure/model-derivative/viewable/{blobName}
```

Response:
```json
{
  "urn": "dXJuOmFkc2sub2JqZWN0czpvcy5vYmplY3Q6...",
  "blobName": "MRConfigurator.zip",
  "viewerReady": true
}
```

#### Process All ZIP Files
```http
POST /api/azure/model-derivative/process-all
```

## Workflow Details

### 1. File Storage
Store your Inventor files as ZIP archives in Azure Blob Storage:
```
models/
├── MRConfigurator.zip
├── WrenchAssembly.zip
└── WheelProject.zip
```

### 2. Processing Steps
When you process a file:

1. **Download**: File is downloaded from Azure to server memory
2. **Bucket Creation**: A unique Forge OSS bucket is created (if needed)
3. **Upload**: File is uploaded to Forge OSS
4. **Translation**: Model Derivative API processes the file
5. **Polling**: Status is monitored until complete
6. **Storage**: URN is stored for future viewing

### 3. Viewing Models
Once processed, models can be viewed using the standard Autodesk Viewer with the generated URN.

## Supported File Types

The Model Derivative API supports many file types. For Inventor:
- `.iam` - Inventor Assembly files
- `.ipt` - Inventor Part files
- `.idw` - Inventor Drawing files
- `.ipn` - Inventor Presentation files

Files should be packaged as ZIP archives with all dependencies included.

## Root File Detection

For ZIP files containing multiple Inventor files, the system attempts to detect the root file:
- Looks for common patterns (e.g., "Assembly.iam", "Main.iam")
- Can be specified manually in the API call
- If not specified, Model Derivative will attempt auto-detection

## Status Codes

- `inprogress` - Translation is running
- `complete` - Translation finished successfully
- `failed` - Translation failed
- `timeout` - Translation took too long

## Troubleshooting

### Model Not Processing
1. Check file exists in Azure: `GET /api/azure/models`
2. Verify it's a ZIP file
3. Check Forge credentials are valid
4. Review server logs for errors

### Translation Failed
1. Ensure ZIP contains all referenced files
2. Check file isn't corrupted
3. Verify Inventor version compatibility
4. Check Model Derivative API quotas

### Viewer Not Loading
1. Verify translation is complete
2. Check URN is valid
3. Ensure Forge access token is valid
4. Check browser console for errors

## Best Practices

1. **File Organization**: Keep related files in the same ZIP
2. **Naming**: Use clear, descriptive filenames
3. **Size**: Keep ZIP files under 200MB for optimal performance
4. **Dependencies**: Include all referenced files (parts, assemblies)
5. **Version**: Use compatible Inventor versions (2020-2024)

## Performance Considerations

- Translation time: 30 seconds to 5 minutes typically
- Large assemblies may take longer
- Multiple files can be processed in parallel
- URNs are cached after first translation

## Security

- Files are temporarily stored in Forge OSS
- Each file gets a unique bucket with limited lifetime
- Access tokens expire after 1 hour
- URNs are safe to share (require authentication to view)

## Example Implementation

```javascript
// Process a file from Azure
async function processAzureFile(blobName) {
  const response = await fetch('/api/azure/model-derivative/process', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ blobName })
  });
  
  const result = await response.json();
  if (result.success) {
    // Poll for completion
    await pollStatus(result.urn);
    // View the model
    viewModel(result.urn);
  }
}
```

## Next Steps

1. Upload your Inventor ZIP files to Azure Blob Storage
2. Use the web interface or API to process them
3. View the processed models in the integrated viewer
4. Integrate the URNs into your application workflow