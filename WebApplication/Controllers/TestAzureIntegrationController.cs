using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebApplication.Services;
using Autodesk.Forge.Model;

namespace WebApplication.Controllers
{
    /// <summary>
    /// Test controller for Azure integration and Model Derivative API.
    /// Use these endpoints to verify the implementation works correctly.
    /// </summary>
    [ApiController]
    [Route("api/test/azure")]
    public class TestAzureIntegrationController : ControllerBase
    {
        private readonly ILogger<TestAzureIntegrationController> _logger;
        private readonly IAzureBlobService _azureBlobService;
        private readonly IModelDerivativeService _modelDerivativeService;
        private readonly IConfiguration _configuration;

        public TestAzureIntegrationController(
            ILogger<TestAzureIntegrationController> logger,
            IAzureBlobService azureBlobService,
            IModelDerivativeService modelDerivativeService,
            IConfiguration configuration)
        {
            _logger = logger;
            _azureBlobService = azureBlobService;
            _modelDerivativeService = modelDerivativeService;
            _configuration = configuration;
        }

        /// <summary>
        /// Test 1: Check Azure Blob Service configuration
        /// </summary>
        [HttpGet("check-config")]
        public async Task<IActionResult> CheckAzureConfig()
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                
                if (isConfigured)
                {
                    // Try to list models to verify access
                    var models = await _azureBlobService.ListModelsAsync();
                    
                    return Ok(new
                    {
                        status = "success",
                        message = "Azure Blob Service is configured and accessible",
                        isConfigured = isConfigured,
                        modelCount = models.Count,
                        models = models
                    });
                }
                else
                {
                    return Ok(new
                    {
                        status = "error",
                        message = "Azure Blob Service is not configured",
                        isConfigured = isConfigured
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Azure configuration");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to check Azure configuration",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test 2: List all blobs in Azure storage
        /// </summary>
        [HttpGet("list-blobs")]
        public async Task<IActionResult> ListBlobs([FromQuery] string prefix = null)
        {
            try
            {
                var blobs = await _azureBlobService.ListBlobsAsync(prefix);
                
                return Ok(new
                {
                    status = "success",
                    count = blobs.Count,
                    prefix = prefix ?? "none",
                    blobs = blobs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing blobs");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to list blobs",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test 3: Generate a SAS URL for a blob
        /// </summary>
        [HttpGet("generate-sas")]
        public async Task<IActionResult> GenerateSasUrl([FromQuery] string blobName)
        {
            try
            {
                if (string.IsNullOrEmpty(blobName))
                {
                    return BadRequest(new { error = "blobName is required" });
                }

                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(
                    blobName,
                    Azure.Storage.Sas.BlobSasPermissions.Read,
                    expirationMinutes: 30
                );

                return Ok(new
                {
                    status = "success",
                    blobName = blobName,
                    sasUrl = sasUrl,
                    expiresIn = "30 minutes"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating SAS URL for {blobName}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to generate SAS URL",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test 4: Create a test bucket in Forge OSS
        /// </summary>
        [HttpPost("create-test-bucket")]
        public async Task<IActionResult> CreateTestBucket()
        {
            try
            {
                var bucketKey = $"test-azure-integration-{Guid.NewGuid().ToString().Substring(0, 8)}";
                var result = await _modelDerivativeService.CreateBucketAsync(bucketKey);

                return Ok(new
                {
                    status = "success",
                    message = "Test bucket created successfully",
                    bucketKey = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test bucket");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to create test bucket",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test 5: Full workflow test - Upload file from Azure to Forge and translate
        /// </summary>
        [HttpPost("test-full-workflow")]
        public async Task<IActionResult> TestFullWorkflow([FromQuery] string azureBlobName)
        {
            try
            {
                if (string.IsNullOrEmpty(azureBlobName))
                {
                    return BadRequest(new { error = "azureBlobName is required" });
                }

                _logger.LogInformation($"Starting full workflow test for blob: {azureBlobName}");

                // Step 1: Check if blob exists
                _logger.LogInformation("Step 1: Checking if blob exists...");
                var exists = await _azureBlobService.BlobExistsAsync(azureBlobName);
                if (!exists)
                {
                    return NotFound(new { error = $"Blob '{azureBlobName}' not found in Azure storage" });
                }
                _logger.LogInformation($"Blob exists: {exists}");

                // Step 2: Create a test bucket
                _logger.LogInformation("Step 2: Creating test bucket...");
                // Forge bucket keys must be globally unique, 3-128 chars, lowercase letters, numbers, and dashes only
                var bucketKey = $"test-workflow-{Guid.NewGuid().ToString("N").Substring(0, 8)}".ToLowerInvariant();
                
                try
                {
                    await _modelDerivativeService.CreateBucketAsync(bucketKey);
                    _logger.LogInformation($"Bucket created: {bucketKey}");
                }
                catch (Exception bucketEx)
                {
                    _logger.LogError(bucketEx, "Failed to create bucket");
                    // Try with a different bucket name
                    bucketKey = $"test-wf-{DateTime.UtcNow.Ticks}".ToLowerInvariant();
                    await _modelDerivativeService.CreateBucketAsync(bucketKey);
                    _logger.LogInformation($"Bucket created with alternate name: {bucketKey}");
                }

                // Step 3: Download from Azure and upload to Forge
                _logger.LogInformation("Step 3: Downloading blob from Azure...");
                using var stream = await _azureBlobService.DownloadBlobAsync(azureBlobName);
                
                // Copy to memory stream to ensure seekability
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                _logger.LogInformation($"Downloaded blob, size: {memoryStream.Length} bytes");
                
                var objectName = $"test_{Guid.NewGuid()}_{azureBlobName}";
                _logger.LogInformation($"Uploading to Forge with name: {objectName}");
                
                // Add retry logic for large file uploads
                ObjectDetails uploadResult = null;
                int retryCount = 3;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        memoryStream.Position = 0; // Reset stream position for each retry
                        uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, memoryStream);
                        break; // Success, exit loop
                    }
                    catch (Exception uploadEx) when (i < retryCount - 1)
                    {
                        _logger.LogWarning($"Upload attempt {i + 1} failed: {uploadEx.Message}. Retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff
                    }
                }
                
                if (uploadResult == null)
                {
                    _logger.LogError("Upload result is null");
                    throw new Exception("Upload result is null");
                }
                _logger.LogInformation($"Upload complete, ObjectId: {uploadResult.ObjectId}");

                // Step 4: Start translation
                _logger.LogInformation("Step 4: Starting translation...");
                // For ZIP files, we need to specify the root filename
                string rootFilename = null;
                if (azureBlobName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // For MRConfigurator.zip, the main assembly is GalaxyConfigurator.iam
                    if (azureBlobName.Equals("MRConfigurator.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        rootFilename = "GalaxyConfigurator.iam";
                    }
                    else
                    {
                        // For other ZIP files, try to guess based on filename
                        rootFilename = azureBlobName.Replace(".zip", ".iam", StringComparison.OrdinalIgnoreCase);
                    }
                    _logger.LogInformation($"ZIP file detected, using rootFilename: {rootFilename}");
                }
                
                var urn = await _modelDerivativeService.TranslateFileAsync(uploadResult.ObjectId, rootFilename);
                _logger.LogInformation($"Translation started, URN: {urn}");

                return Ok(new
                {
                    status = "success",
                    message = "Full workflow test completed",
                    steps = new
                    {
                        azureBlobExists = exists,
                        bucketCreated = bucketKey,
                        fileUploaded = uploadResult.ObjectId,
                        translationStarted = urn,
                        rootFilename = rootFilename ?? "not specified"
                    },
                    nextStep = $"Check translation progress at: /api/test/azure/check-translation?urn={urn}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in full workflow test");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Full workflow test failed",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Create and upload a small test file to isolate upload issues
        /// </summary>
        [HttpPost("test-small-file-upload")]
        public async Task<IActionResult> TestSmallFileUpload()
        {
            try
            {
                // Create a small test file
                var testContent = "This is a test file for Forge upload validation.";
                var testBytes = System.Text.Encoding.UTF8.GetBytes(testContent);
                var testStream = new MemoryStream(testBytes);
                
                // Create bucket
                var bucketKey = $"test-small-{Guid.NewGuid().ToString("N").Substring(0, 8)}".ToLowerInvariant();
                await _modelDerivativeService.CreateBucketAsync(bucketKey);
                
                // Upload small file
                var objectName = "test-small-file.txt";
                var uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, testStream);
                
                return Ok(new
                {
                    status = "success",
                    message = "Small file upload test completed",
                    bucketKey,
                    objectName,
                    fileSize = testBytes.Length,
                    objectId = uploadResult?.ObjectId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in small file upload test");
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Test 6: Check translation progress
        /// </summary>
        [HttpGet("check-translation")]
        public async Task<IActionResult> CheckTranslation([FromQuery] string urn)
        {
            try
            {
                if (string.IsNullOrEmpty(urn))
                {
                    return BadRequest(new { error = "urn is required" });
                }

                var progress = await _modelDerivativeService.GetTranslationProgressAsync(urn);
                var manifest = await _modelDerivativeService.GetManifestAsync(urn);

                return Ok(new
                {
                    status = "success",
                    urn = urn,
                    progress = progress,
                    manifest = manifest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking translation for {urn}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to check translation",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test 7: Test Azure SVF Proxy
        /// </summary>
        [HttpGet("test-proxy")]
        public IActionResult TestProxy()
        {
            var testPaths = new List<object>
            {
                new { 
                    description = "Test bubble.json access",
                    url = "/api/azuresvfproxy/svf/MRConfigurator/output/bubble.json"
                },
                new { 
                    description = "Test ZIP extraction",
                    url = "/api/azuresvfproxy/extract/testproject/testhash/output/bubble.json"
                }
            };

            return Ok(new
            {
                status = "info",
                message = "Test these proxy endpoints manually",
                endpoints = testPaths
            });
        }

        /// <summary>
        /// Test 8: List ZIP contents to find root file
        /// </summary>
        [HttpGet("list-zip-contents")]
        public async Task<IActionResult> ListZipContents([FromQuery] string azureBlobName)
        {
            try
            {
                if (string.IsNullOrEmpty(azureBlobName))
                {
                    return BadRequest(new { error = "azureBlobName is required" });
                }

                if (!azureBlobName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = "File must be a ZIP file" });
                }

                // Download the ZIP file from Azure
                using var stream = await _azureBlobService.DownloadBlobAsync(azureBlobName);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // List ZIP contents
                var entries = new List<object>();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        entries.Add(new
                        {
                            fullName = entry.FullName,
                            name = entry.Name,
                            length = entry.Length,
                            compressedLength = entry.CompressedLength,
                            isDirectory = string.IsNullOrEmpty(entry.Name)
                        });
                    }
                }

                // Find potential root files
                var potentialRootFiles = entries
                    .Cast<dynamic>()
                    .Where(d => !d.isDirectory)
                    .Where(d => ((string)d.name).EndsWith(".iam") || ((string)d.name).EndsWith(".ipt") || ((string)d.name).EndsWith(".dwg"))
                    .ToList();

                return Ok(new
                {
                    status = "success",
                    zipFile = azureBlobName,
                    totalEntries = entries.Count,
                    entries = entries.OrderBy(e => (e as dynamic).fullName),
                    potentialRootFiles = potentialRootFiles,
                    suggestedRootFile = potentialRootFiles.FirstOrDefault()?.fullName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing ZIP contents for {azureBlobName}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Failed to list ZIP contents",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test with RF Joist.zip using SAS URL approach
        /// </summary>
        [HttpPost("test-rf-joist-sas")]
        public async Task<IActionResult> TestRFJoistWithSAS()
        {
            try
            {
                var blobName = "RF Joist.zip";
                _logger.LogInformation($"Testing with manually uploaded {blobName}");

                // Step 1: Verify file exists
                var exists = await _azureBlobService.BlobExistsAsync(blobName);
                if (!exists)
                {
                    return NotFound(new { error = $"Blob '{blobName}' not found in Azure storage" });
                }

                // Step 2: Generate SAS URL for direct access
                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(blobName, 
                    Azure.Storage.Sas.BlobSasPermissions.Read, 
                    expirationMinutes: 60);
                
                _logger.LogInformation($"Generated SAS URL: {sasUrl.Substring(0, 100)}...");

                // Step 3: Create bucket for Forge
                var bucketKey = $"rf-joist-test-{Guid.NewGuid().ToString("N").Substring(0, 8)}".ToLowerInvariant();
                await _modelDerivativeService.CreateBucketAsync(bucketKey);
                _logger.LogInformation($"Created bucket: {bucketKey}");

                // Step 4: Download directly from SAS URL and upload to Forge
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                
                var downloadResponse = await httpClient.GetAsync(sasUrl);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    return StatusCode(500, new { error = "Failed to download from SAS URL", status = downloadResponse.StatusCode });
                }

                using var stream = await downloadResponse.Content.ReadAsStreamAsync();
                var fileSize = downloadResponse.Content.Headers.ContentLength ?? 0;
                _logger.LogInformation($"Downloaded file size: {fileSize} bytes");

                // Try smaller chunks for upload
                var objectName = $"rf_joist_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
                
                // Copy to memory stream for better control
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                _logger.LogInformation($"Attempting upload of {objectName} ({memoryStream.Length} bytes)");
                
                try
                {
                    var uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, memoryStream);
                    
                    // Step 5: Start translation
                    var urn = await _modelDerivativeService.TranslateFileAsync(uploadResult.ObjectId, null);
                    
                    return Ok(new
                    {
                        status = "success",
                        message = "RF Joist test completed successfully",
                        sasUrl = sasUrl.Substring(0, 100) + "...",
                        bucketKey,
                        objectName,
                        fileSize,
                        objectId = uploadResult.ObjectId,
                        translationUrn = urn,
                        nextStep = $"Check translation at: /api/test/azure/check-translation?urn={urn}"
                    });
                }
                catch (Exception uploadEx)
                {
                    _logger.LogError(uploadEx, "Upload failed, trying alternative approach");
                    
                    // Alternative: Try using signed URL upload
                    return Ok(new
                    {
                        status = "partial",
                        message = "Direct upload failed, SAS URL generated for manual testing",
                        sasUrl,
                        bucketKey,
                        objectName,
                        fileSize,
                        uploadError = uploadEx.Message,
                        suggestion = "Try using Forge's signed URL upload or chunked upload API directly"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RF Joist SAS test");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simple endpoint to get SAS URL for any blob
        /// </summary>
        [HttpGet("get-sas-url/{blobName}")]
        public async Task<IActionResult> GetSasUrl(string blobName)
        {
            try
            {
                var exists = await _azureBlobService.BlobExistsAsync(blobName);
                if (!exists)
                {
                    return NotFound(new { error = $"Blob '{blobName}' not found" });
                }

                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(blobName, 
                    Azure.Storage.Sas.BlobSasPermissions.Read, 
                    expirationMinutes: 120); // 2 hours

                return Ok(new
                {
                    blobName,
                    sasUrl,
                    expiresIn = "2 hours",
                    usage = "Use this URL to download the file directly or pass to Forge APIs"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Upload a file and translate it to SVF
        /// </summary>
        [HttpPost("upload-and-translate")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAndTranslate(IFormFile file, [FromForm] string rootFilename = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            try
            {
                _logger.LogInformation($"Received file: {file.FileName}, Size: {file.Length} bytes");

                // Step 1: Upload to Azure Blob Storage first
                var azureBlobName = $"uploads/{DateTime.UtcNow:yyyyMMdd}/{file.FileName}";
                using (var stream = file.OpenReadStream())
                {
                    await _azureBlobService.UploadStreamAsync(stream, azureBlobName);
                }
                _logger.LogInformation($"Uploaded to Azure: {azureBlobName}");

                // Step 2: Create a unique bucket for this translation
                var bucketKey = $"upload-{Guid.NewGuid().ToString("N").Substring(0, 10)}".ToLowerInvariant();
                await _modelDerivativeService.CreateBucketAsync(bucketKey);
                _logger.LogInformation($"Created bucket: {bucketKey}");

                // Step 3: Try direct upload approach first
                var objectName = $"upload_{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
                
                ObjectDetails uploadResult = null;
                string uploadMethod = "direct";
                
                try
                {
                    // Reset stream position
                    using var uploadStream = file.OpenReadStream();
                    uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, uploadStream);
                    _logger.LogInformation($"Direct upload successful: {uploadResult.ObjectId}");
                }
                catch (Exception directEx)
                {
                    _logger.LogWarning($"Direct upload failed: {directEx.Message}. Trying alternative method...");
                    
                    // Alternative: Generate SAS URL and let user know
                    uploadMethod = "manual";
                    var sasUrl = await _azureBlobService.GenerateSasUrlAsync(azureBlobName, 
                        Azure.Storage.Sas.BlobSasPermissions.Read, 
                        expirationMinutes: 120);
                    
                    return Ok(new
                    {
                        status = "partial",
                        message = "File uploaded to Azure, but Forge upload failed",
                        azureBlobName,
                        sasUrl,
                        bucketKey,
                        objectName,
                        fileSize = file.Length,
                        suggestion = "Use the SAS URL with Forge API directly or try again later",
                        error = directEx.Message
                    });
                }

                // Step 4: Start translation
                var urn = await _modelDerivativeService.TranslateFileAsync(uploadResult.ObjectId, rootFilename);
                
                // Step 5: Store translation info in Azure (optional)
                var translationInfo = new
                {
                    originalFile = file.FileName,
                    azureBlobName,
                    bucketKey,
                    objectName,
                    objectId = uploadResult.ObjectId,
                    urn,
                    translationStarted = DateTime.UtcNow,
                    fileSize = file.Length,
                    rootFilename
                };
                
                var infoJson = System.Text.Json.JsonSerializer.Serialize(translationInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var infoBytes = System.Text.Encoding.UTF8.GetBytes(infoJson);
                using var infoStream = new MemoryStream(infoBytes);
                await _azureBlobService.UploadStreamAsync(infoStream, $"translations/{urn}.json");

                return Ok(new
                {
                    status = "success",
                    message = "File uploaded and translation started",
                    originalFile = file.FileName,
                    azureBlobName,
                    uploadMethod,
                    translation = translationInfo,
                    checkProgress = $"/api/test/azure/check-translation?urn={urn}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in upload and translate");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Check translation and download SVF when ready
        /// </summary>
        [HttpGet("download-svf/{urn}")]
        public async Task<IActionResult> DownloadSvf(string urn)
        {
            try
            {
                // Check translation status
                var manifest = await _modelDerivativeService.GetManifestAsync(urn);
                
                if (manifest.Status != "success" && manifest.Status != "complete")
                {
                    return Ok(new
                    {
                        status = "processing",
                        message = "Translation not complete",
                        translationStatus = manifest.Status,
                        progress = manifest.Progress
                    });
                }

                // Find SVF derivative
                var svfDerivative = manifest.Derivatives
                    ?.FirstOrDefault(d => d.OutputType == ManifestDerivative.OutputTypeEnum.Svf);
                
                if (svfDerivative == null)
                {
                    return NotFound(new { error = "No SVF derivative found" });
                }

                // Get download info
                var viewerUrl = await _modelDerivativeService.GetViewerUrlAsync(urn);
                
                return Ok(new
                {
                    status = "success",
                    urn,
                    translationStatus = manifest.Status,
                    svfAvailable = true,
                    viewerUrl,
                    derivatives = manifest.Derivatives?.Select(d => new
                    {
                        name = d.Name,
                        outputType = d.OutputType,
                        status = d.Status
                    }),
                    message = "SVF is ready. Use the viewer URL to access it."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Simple upload to Azure only (no Forge translation)
        /// </summary>
        [HttpPost("upload-to-azure-only")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadToAzureOnly(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            try
            {
                _logger.LogInformation($"Azure-only upload: {file.FileName}, Size: {file.Length} bytes");

                // Upload to Azure Blob Storage
                var azureBlobName = $"uploads/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid().ToString("N").Substring(0, 8)}_{file.FileName}";
                using (var stream = file.OpenReadStream())
                {
                    await _azureBlobService.UploadStreamAsync(stream, azureBlobName);
                }

                // Generate SAS URL for access
                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(azureBlobName, 
                    Azure.Storage.Sas.BlobSasPermissions.Read, 
                    expirationMinutes: 1440); // 24 hours

                return Ok(new
                {
                    status = "success",
                    message = "File uploaded to Azure successfully",
                    azureBlobName,
                    fileName = file.FileName,
                    fileSize = file.Length,
                    sasUrl,
                    downloadUrl = sasUrl,
                    suggestion = "Use the SAS URL with Forge API directly or download the file"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to Azure");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Translate a file from Azure URL (two-step process)
        /// </summary>
        [HttpPost("translate-from-azure-url")]
        public async Task<IActionResult> TranslateFromAzureUrl([FromBody] TranslateFromUrlRequest request)
        {
            if (string.IsNullOrEmpty(request?.AzureUrl))
            {
                return BadRequest(new { error = "Azure URL is required" });
            }

            try
            {
                _logger.LogInformation($"Starting translation from Azure URL");

                // Step 1: Create bucket
                var bucketKey = $"azure-translate-{Guid.NewGuid().ToString("N").Substring(0, 8)}".ToLowerInvariant();
                await _modelDerivativeService.CreateBucketAsync(bucketKey);
                
                // Step 2: Download file from Azure URL
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                
                var response = await httpClient.GetAsync(request.AzureUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new { error = "Failed to download from Azure URL" });
                }

                // Extract filename from URL or use provided one
                var fileName = request.FileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    var uri = new Uri(request.AzureUrl);
                    fileName = Path.GetFileName(uri.LocalPath);
                    if (fileName.Contains('?'))
                    {
                        fileName = fileName.Substring(0, fileName.IndexOf('?'));
                    }
                }

                var objectName = $"azure_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
                
                // Step 3: Upload to Forge
                using var stream = await response.Content.ReadAsStreamAsync();
                var uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, stream);
                
                // Step 4: Start translation
                var urn = await _modelDerivativeService.TranslateFileAsync(uploadResult.ObjectId, request.RootFilename);
                
                return Ok(new
                {
                    status = "success",
                    message = "Translation started from Azure URL",
                    bucketKey,
                    objectName,
                    objectId = uploadResult.ObjectId,
                    urn,
                    checkProgress = $"/api/test/azure/check-translation?urn={urn}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating from Azure URL");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class TranslateFromUrlRequest
        {
            public string AzureUrl { get; set; }
            public string FileName { get; set; }
            public string RootFilename { get; set; }
        }

        /// <summary>
        /// Test Forge connectivity and authentication
        /// </summary>
        [HttpGet("test-forge-connection")]
        public async Task<IActionResult> TestForgeConnection()
        {
            try
            {
                _logger.LogInformation("Testing Forge connection and authentication");
                
                var results = new List<object>();
                
                // Step 1: Test basic authentication
                try
                {
                    // Create a simple test bucket to verify auth works
                    var testBucketKey = $"test-auth-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    await _modelDerivativeService.CreateBucketAsync(testBucketKey);
                    
                    results.Add(new { 
                        step = "Authentication", 
                        status = "success", 
                        message = "Successfully authenticated with Forge",
                        bucket = testBucketKey
                    });
                }
                catch (Exception authEx)
                {
                    results.Add(new { 
                        step = "Authentication", 
                        status = "failed", 
                        message = authEx.Message,
                        type = authEx.GetType().Name
                    });
                }

                // Step 2: Test small file upload
                try
                {
                    var testBucketKey = $"test-upload-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    await _modelDerivativeService.CreateBucketAsync(testBucketKey);
                    
                    var testData = System.Text.Encoding.UTF8.GetBytes("This is a test file for Forge connectivity.");
                    using var testStream = new MemoryStream(testData);
                    var uploadResult = await _modelDerivativeService.UploadFileAsync(testBucketKey, "test.txt", testStream);
                    
                    results.Add(new { 
                        step = "SmallFileUpload", 
                        status = "success",
                        message = "Successfully uploaded small test file",
                        objectId = uploadResult?.ObjectId
                    });
                }
                catch (Exception uploadEx)
                {
                    results.Add(new { 
                        step = "SmallFileUpload", 
                        status = "failed", 
                        message = uploadEx.Message,
                        innerMessage = uploadEx.InnerException?.Message,
                        type = uploadEx.GetType().Name
                    });
                }

                // Step 3: Test network connectivity to Forge endpoints
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    
                    var endpoints = new[] {
                        "https://developer.api.autodesk.com",
                        "https://developer.api.autodesk.com/oss/v2/buckets",
                        "https://developer.api.autodesk.com/modelderivative/v2/designdata"
                    };
                    
                    foreach (var endpoint in endpoints)
                    {
                        try
                        {
                            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, endpoint));
                            results.Add(new { 
                                step = "NetworkTest", 
                                status = "success",
                                endpoint = endpoint,
                                statusCode = (int)response.StatusCode
                            });
                        }
                        catch (Exception netEx)
                        {
                            results.Add(new { 
                                step = "NetworkTest", 
                                status = "failed",
                                endpoint = endpoint,
                                message = netEx.Message
                            });
                        }
                    }
                }
                catch (Exception netEx)
                {
                    results.Add(new { 
                        step = "NetworkTest", 
                        status = "failed", 
                        message = netEx.Message
                    });
                }

                return Ok(new
                {
                    status = "completed",
                    timestamp = DateTime.UtcNow,
                    results = results,
                    configuration = new
                    {
                        hasForgeCredentials = !string.IsNullOrEmpty(_configuration["Forge:clientId"]),
                        environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Forge connection");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}