using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("api/azure-models")]
    public class AzureModelsController : ControllerBase
    {
        private readonly IAzureBlobService _azureBlobService;
        private readonly ILogger<AzureModelsController> _logger;

        public AzureModelsController(IAzureBlobService azureBlobService, ILogger<AzureModelsController> logger)
        {
            _azureBlobService = azureBlobService;
            _logger = logger;
        }

        /// <summary>
        /// List all models in Azure Blob Storage
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> ListModels()
        {
            try
            {
                var models = await _azureBlobService.ListModelsAsync();
                return Ok(new
                {
                    Success = true,
                    Count = models.Count,
                    Models = models
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
        /// Get URL for a specific model
        /// </summary>
        [HttpGet("{modelName}/url")]
        public async Task<ActionResult> GetModelUrl(string modelName)
        {
            try
            {
                var url = await _azureBlobService.GetModelUrlAsync(modelName);
                return Ok(new
                {
                    Success = true,
                    ModelName = modelName,
                    Url = url
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
        /// Check if a model exists
        /// </summary>
        [HttpGet("{modelName}/exists")]
        public async Task<ActionResult> ModelExists(string modelName)
        {
            try
            {
                var exists = await _azureBlobService.ModelExistsAsync(modelName);
                return Ok(new
                {
                    Success = true,
                    ModelName = modelName,
                    Exists = exists
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
        /// Test Azure Blob Storage connectivity
        /// </summary>
        [HttpGet("test")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                var exists = await _azureBlobService.ModelExistsAsync("MRConfigurator.zip");
                return Ok(new
                {
                    Success = true,
                    Message = "Azure Blob Storage connection successful",
                    MRConfiguratorExists = exists
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
    }
} 