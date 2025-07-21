using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

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

        public TestAzureIntegrationController(
            ILogger<TestAzureIntegrationController> logger,
            IAzureBlobService azureBlobService,
            IModelDerivativeService modelDerivativeService)
        {
            _logger = logger;
            _azureBlobService = azureBlobService;
            _modelDerivativeService = modelDerivativeService;
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
                var bucketKey = $"test-workflow-{DateTime.UtcNow:yyyyMMddHHmmss}";
                await _modelDerivativeService.CreateBucketAsync(bucketKey);
                _logger.LogInformation($"Bucket created: {bucketKey}");

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
                var uploadResult = await _modelDerivativeService.UploadFileAsync(bucketKey, objectName, memoryStream);
                
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
    }
}