using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("api/azure-standalone")]
    public class AzureStandaloneController : ControllerBase
    {
        private readonly IAzureBlobService _azureBlobService;
        private readonly ILogger<AzureStandaloneController> _logger;

        public AzureStandaloneController(IAzureBlobService azureBlobService, ILogger<AzureStandaloneController> logger)
        {
            _azureBlobService = azureBlobService;
            _logger = logger;
        }

        /// <summary>
        /// Test Azure Blob Storage connectivity (standalone - no Forge required)
        /// </summary>
        [HttpGet("test")]
        public async Task<ActionResult> TestAzureConnection()
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                if (!isConfigured)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Azure Blob Storage is not configured",
                        ConfigurationRequired = new
                        {
                            BaseUrl = "https://conform3d.blob.core.windows.net/models",
                            SasToken = "Your SAS token",
                            ContainerName = "models"
                        }
                    });
                }

                var exists = await _azureBlobService.ModelExistsAsync("MRConfigurator.zip");
                return Ok(new
                {
                    Success = true,
                    Message = "Azure Blob Storage connection successful",
                    IsConfigured = isConfigured,
                    MRConfiguratorExists = exists,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Azure Blob Storage connection");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    Message = "Azure Blob Storage connection failed"
                });
            }
        }

        /// <summary>
        /// List all models in Azure Blob Storage (standalone - no Forge required)
        /// </summary>
        [HttpGet("models")]
        public async Task<ActionResult> ListAzureModels()
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                if (!isConfigured)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Azure Blob Storage is not configured"
                    });
                }

                var models = await _azureBlobService.ListModelsAsync();
                return Ok(new
                {
                    Success = true,
                    Count = models.Count,
                    Models = models,
                    Source = "Azure Blob Storage",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing models from Azure Blob Storage");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get URL for a specific model (standalone - no Forge required)
        /// </summary>
        [HttpGet("models/{modelName}/url")]
        public async Task<ActionResult> GetAzureModelUrl(string modelName)
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                if (!isConfigured)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Azure Blob Storage is not configured"
                    });
                }

                var url = await _azureBlobService.GetModelUrlAsync(modelName);
                var exists = await _azureBlobService.ModelExistsAsync(modelName);
                
                return Ok(new
                {
                    Success = true,
                    ModelName = modelName,
                    Url = url,
                    Exists = exists,
                    Source = "Azure Blob Storage"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting URL for model {modelName}");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Check if a model exists in Azure Storage (standalone - no Forge required)
        /// </summary>
        [HttpGet("models/{modelName}/exists")]
        public async Task<ActionResult> CheckAzureModelExists(string modelName)
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                if (!isConfigured)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Azure Blob Storage is not configured"
                    });
                }

                var exists = await _azureBlobService.ModelExistsAsync(modelName);
                return Ok(new
                {
                    Success = true,
                    ModelName = modelName,
                    Exists = exists,
                    Source = "Azure Blob Storage",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if model {modelName} exists");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get configuration status
        /// </summary>
        [HttpGet("config")]
        public async Task<ActionResult> GetConfigurationStatus()
        {
            try
            {
                var isConfigured = await _azureBlobService.IsConfiguredAsync();
                return Ok(new
                {
                    Success = true,
                    IsConfigured = isConfigured,
                    Message = isConfigured ? "Azure Blob Storage is properly configured" : "Azure Blob Storage configuration is missing",
                    RequiredSettings = new
                    {
                        BaseUrl = "AzureBlobStorage:BaseUrl",
                        SasToken = "AzureBlobStorage:SasToken",
                        ContainerName = "AzureBlobStorage:ContainerName"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration status");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
} 