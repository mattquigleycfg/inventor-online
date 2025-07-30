using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;
using System.Linq;

namespace WebApplication.Controllers
{
    /// <summary>
    /// Controller for processing Azure Blob Storage files through Model Derivative API
    /// </summary>
    [ApiController]
    [Route("api/azure/model-derivative")]
    public class AzureModelDerivativeController : ControllerBase
    {
        private readonly ILogger<AzureModelDerivativeController> _logger;
        private readonly IAzureModelDerivativeService _azureModelDerivativeService;
        private readonly IAzureBlobService _azureBlobService;

        public AzureModelDerivativeController(
            ILogger<AzureModelDerivativeController> logger,
            IAzureModelDerivativeService azureModelDerivativeService,
            IAzureBlobService azureBlobService)
        {
            _logger = logger;
            _azureModelDerivativeService = azureModelDerivativeService;
            _azureBlobService = azureBlobService;
        }

        /// <summary>
        /// Process a file from Azure Blob Storage
        /// </summary>
        [HttpPost("process")]
        public async Task<IActionResult> ProcessBlob([FromBody] ProcessBlobRequest request)
        {
            if (string.IsNullOrEmpty(request?.BlobName))
            {
                return BadRequest(new { error = "Blob name is required" });
            }

            try
            {
                _logger.LogInformation($"Processing blob: {request.BlobName}");

                // Check if blob exists
                if (!await _azureBlobService.BlobExistsAsync(request.BlobName))
                {
                    return NotFound(new { error = $"Blob '{request.BlobName}' not found in Azure storage" });
                }

                // Process the blob
                var result = await _azureModelDerivativeService.ProcessAzureBlobAsync(
                    request.BlobName, 
                    request.BucketKey
                );

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        urn = result.Urn,
                        status = result.Status,
                        message = result.Message
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = result.Error,
                        status = result.Status
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing blob: {request.BlobName}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Get the status of a translation job
        /// </summary>
        [HttpGet("status/{urn}")]
        public async Task<IActionResult> GetJobStatus(string urn)
        {
            try
            {
                var progress = await _azureModelDerivativeService.GetJobStatusAsync(urn);
                
                return Ok(new
                {
                    status = progress.Status,
                    progress = progress.Progress,
                    hasDerivatives = progress.HasDerivatives,
                    complete = progress.Status == "complete",
                    failed = progress.Status == "failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting job status for URN: {urn}");
                return StatusCode(500, new { error = "Failed to get job status" });
            }
        }

        /// <summary>
        /// Get the viewable URN for a processed model
        /// </summary>
        [HttpGet("viewable/{blobName}")]
        public async Task<IActionResult> GetViewableUrn(string blobName)
        {
            try
            {
                var urn = await _azureModelDerivativeService.GetViewableUrnAsync(blobName);
                
                return Ok(new
                {
                    urn = urn,
                    blobName = blobName,
                    viewerReady = true
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = $"Model '{blobName}' has not been processed" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting viewable URN for: {blobName}");
                return StatusCode(500, new { error = "Failed to get viewable URN" });
            }
        }

        /// <summary>
        /// Get list of all processed models
        /// </summary>
        [HttpGet("processed")]
        public async Task<IActionResult> GetProcessedModels()
        {
            try
            {
                var models = await _azureModelDerivativeService.GetProcessedModelsAsync();
                
                return Ok(models.Select(m => new
                {
                    blobName = m.AzureBlobName,
                    urn = m.Urn,
                    status = m.Status,
                    processedAt = m.ProcessedAt,
                    viewerReady = m.Status == "complete"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting processed models");
                return StatusCode(500, new { error = "Failed to get processed models" });
            }
        }

        /// <summary>
        /// Process all ZIP files in Azure storage
        /// </summary>
        [HttpPost("process-all")]
        public async Task<IActionResult> ProcessAllZipFiles()
        {
            try
            {
                // List all blobs
                var blobs = await _azureBlobService.ListBlobsAsync();
                
                // Filter ZIP files
                var zipFiles = blobs.Where(b => b.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
                
                _logger.LogInformation($"Found {zipFiles.Count} ZIP files to process");

                var results = new System.Collections.Generic.List<object>();

                foreach (var zipFile in zipFiles)
                {
                    try
                    {
                        var result = await _azureModelDerivativeService.ProcessAzureBlobAsync(zipFile);
                        results.Add(new
                        {
                            blobName = zipFile,
                            success = result.Success,
                            urn = result.Urn,
                            status = result.Status,
                            error = result.Error
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing {zipFile}");
                        results.Add(new
                        {
                            blobName = zipFile,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    totalFiles = zipFiles.Count,
                    processed = results.Count(r => ((dynamic)r).success),
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing all ZIP files");
                return StatusCode(500, new { error = "Failed to process files" });
            }
        }
    }

    public class ProcessBlobRequest
    {
        public string BlobName { get; set; }
        public string BucketKey { get; set; }
    }
}