using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

namespace WebApplication.Controllers
{
    /// <summary>
    /// Controller for Azure Blob Storage operations and debugging
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AzureController : ControllerBase
    {
        private readonly ILogger<AzureController> _logger;
        private readonly IAzureBlobService _azureBlobService;

        public AzureController(
            ILogger<AzureController> logger,
            IAzureBlobService azureBlobService)
        {
            _logger = logger;
            _azureBlobService = azureBlobService;
        }

        /// <summary>
        /// Get Azure configuration status
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                
                return Ok(new
                {
                    configured = isConfigured,
                    timestamp = DateTime.UtcNow,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Azure status");
                return StatusCode(500, new { error = "Failed to get Azure status" });
            }
        }

        /// <summary>
        /// List models/files in Azure storage
        /// </summary>
        [HttpGet("models")]
        public async Task<IActionResult> ListModels()
        {
            try
            {
                // First try the ListModelsAsync method
                var models = await _azureBlobService.ListModelsAsync();
                
                // If no models from ListModelsAsync, try listing all blobs
                if (!models.Any())
                {
                    var blobs = await _azureBlobService.ListBlobsAsync();
                    models = blobs.Select(name => new ModelInfo
                    {
                        Name = name,
                        DisplayName = System.IO.Path.GetFileNameWithoutExtension(name),
                        Size = 0, // Size would need separate call
                        LastModified = DateTime.UtcNow
                    }).ToList();
                }
                
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing models");
                return Ok(new[] 
                {
                    new { 
                        name = "Error accessing storage", 
                        error = ex.Message,
                        size = 0 
                    }
                });
            }
        }

        /// <summary>
        /// Generate SAS URL for a specific blob
        /// </summary>
        [HttpGet("sas-url")]
        public async Task<IActionResult> GetSasUrl([FromQuery] string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
            {
                return BadRequest(new { error = "Blob name is required" });
            }

            try
            {
                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(blobName);
                return Ok(new { url = sasUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating SAS URL for {blobName}");
                return StatusCode(500, new { error = "Failed to generate SAS URL" });
            }
        }

        /// <summary>
        /// Check if a specific blob exists
        /// </summary>
        [HttpHead("blob/{*blobName}")]
        public async Task<IActionResult> CheckBlobExists(string blobName)
        {
            try
            {
                var exists = await _azureBlobService.BlobExistsAsync(blobName);
                if (exists)
                {
                    return Ok();
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking blob existence: {blobName}");
                return StatusCode(500);
            }
        }
    }
}