# Azure Blob Storage SVF Integration Guide

## Overview

This guide explains how to properly store and serve SVF files from Azure Blob Storage for use with the Autodesk Viewer. After struggling with this integration for a week, here's a simplified approach that works.

## Key Concepts

### 1. SVF File Structure
SVF (Simple Viewing Format) files are not single files but a collection of resources:
- `bubble.json` - The manifest file that describes the model structure
- SVF files - The actual geometry data
- Property database files
- Texture/material files
- Thumbnail images

### 2. Directory Structure Requirements
The Autodesk Viewer expects SVF files to maintain a specific directory structure:

```
your-model/
├── bubble.json
├── output/
│   ├── geometry/
│   │   ├── *.svf
│   │   └── *.pack
│   ├── materials/
│   │   └── *.json
│   └── properties/
│       └── *.db
```

## Step-by-Step Setup

### 1. Configure Azure Blob Storage

First, ensure your Azure Blob Storage is properly configured in `appsettings.json`:

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccountname;AccountKey=yourkey;EndpointSuffix=core.windows.net",
    "ContainerName": "models",
    "BaseUrl": "https://youraccountname.blob.core.windows.net/models"
  }
}
```

Or use environment variables (recommended for production):
- `AZURE_BLOB_BASE_URL`
- `AZURE_BLOB_SAS_TOKEN`

### 2. Upload SVF Files to Azure

When uploading SVF files to Azure, maintain the directory structure:

```csharp
// Example: Upload a complete SVF package
public async Task UploadSvfPackage(string localSvfFolder, string azureModelPath)
{
    var files = Directory.GetFiles(localSvfFolder, "*", SearchOption.AllDirectories);
    
    foreach (var file in files)
    {
        var relativePath = Path.GetRelativePath(localSvfFolder, file);
        var azurePath = Path.Combine(azureModelPath, relativePath).Replace('\\', '/');
        
        await _azureBlobService.UploadFileAsync(file, azurePath);
    }
}
```

### 3. Use the Simplified Viewer

The `AzureSimpleViewer` component handles loading models from Azure:

```javascript
import AzureSimpleViewer from './components/azureSimpleViewer';

function MyComponent() {
    return (
        <AzureSimpleViewer
            azureBubbleUrl="mymodel/bubble.json"
            onModelLoaded={() => console.log('Model loaded')}
            onError={(error) => console.error('Loading failed:', error)}
        />
    );
}
```

### 4. How the Proxy Works

The Azure SVF Proxy controller (`/api/azuresvfproxy/`) handles:
1. CORS issues between the viewer and Azure Blob Storage
2. SAS token generation for secure access
3. Proper content-type headers for different file types
4. Path resolution for nested SVF resources

## Common Issues and Solutions

### Issue 1: Model Not Loading
**Symptom**: Viewer shows "Model not found" or network errors

**Solutions**:
- Verify the bubble.json path is correct
- Check that all SVF resources are uploaded with correct paths
- Ensure SAS token has read permissions
- Check browser console for specific file 404 errors

### Issue 2: CORS Errors
**Symptom**: Browser console shows CORS policy errors

**Solution**: Use the proxy endpoint instead of direct Azure URLs:
```javascript
// Wrong
azureBubbleUrl: "https://myaccount.blob.core.windows.net/models/bubble.json"

// Correct
azureBubbleUrl: "mymodel/bubble.json"  // Proxy will handle the full URL
```

### Issue 3: Authentication Errors
**Symptom**: 403 Forbidden errors

**Solutions**:
- Verify Azure connection string or SAS token
- Check SAS token expiration
- Ensure container has proper access policies

## Best Practices

1. **Organize Models**: Use a consistent folder structure:
   ```
   models/
   ├── project1/
   │   └── [svf files]
   ├── project2/
   │   └── [svf files]
   ```

2. **Cache SAS URLs**: Generate SAS URLs with reasonable expiration (30-60 minutes)

3. **Monitor Usage**: Log all proxy requests to track usage and errors

4. **Optimize Storage**: 
   - Use Azure CDN for better performance
   - Enable compression for text files (JSON, JS)
   - Set appropriate cache headers

## Testing Your Setup

1. **Test Direct Access**:
   ```bash
   # Test if bubble.json is accessible
   curl "https://youraccount.blob.core.windows.net/models/mymodel/bubble.json?[SAS_TOKEN]"
   ```

2. **Test Proxy Access**:
   ```bash
   # Test proxy endpoint
   curl "https://yourapp.com/api/azuresvfproxy/mymodel/bubble.json"
   ```

3. **Use the Demo Page**:
   Navigate to `/test-azure` to use the interactive demo page

## Migration from Forge OSS

If migrating from Forge Object Storage Service:

1. Download SVF files using Model Derivative API
2. Maintain the exact directory structure
3. Upload to Azure preserving paths
4. Update viewer to use Azure URLs

## Troubleshooting Checklist

- [ ] Azure Storage account is accessible
- [ ] Container exists and has proper permissions
- [ ] All SVF files are uploaded (check bubble.json references)
- [ ] Proxy controller is properly configured
- [ ] No CORS errors in browser console
- [ ] SAS tokens are valid and not expired
- [ ] File paths match exactly (case-sensitive)

## Example Implementation

See the following files for reference:
- `/WebApplication/Controllers/AzureSvfProxyController.cs` - Proxy implementation
- `/WebApplication/Services/AzureBlobService.cs` - Azure integration
- `/WebApplication/ClientApp/src/components/azureSimpleViewer.js` - Viewer component
- `/WebApplication/ClientApp/src/components/azureViewerDemo.js` - Demo page