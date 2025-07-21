using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelDerivativeController : ControllerBase
    {
        private readonly ILogger<ModelDerivativeController> _logger;
        private readonly IModelDerivativeService _modelDerivativeService;
        private readonly IAzureBlobService _azureBlobService;
        private const string TRANSLATION_BUCKET = "inventor-online-translations";

        public ModelDerivativeController(
            ILogger<ModelDerivativeController> logger,
            IModelDerivativeService modelDerivativeService,
            IAzureBlobService azureBlobService)
        {
            _logger = logger;
            _modelDerivativeService = modelDerivativeService;
            _azureBlobService = azureBlobService;
        }

        [HttpPost("translate/azure/{blobName}")]
        public async Task<IActionResult> TranslateAzureFile(string blobName)
        {
            try
            {
                // Check if blob exists in Azure
                if (!await _azureBlobService.BlobExistsAsync(blobName))
                {
                    return NotFound($"Blob {blobName} not found in Azure storage");
                }

                // Create bucket if it doesn't exist
                await _modelDerivativeService.CreateBucketAsync(TRANSLATION_BUCKET);

                // Download file from Azure
                using var azureStream = await _azureBlobService.DownloadBlobAsync(blobName);
                
                // Generate unique object name for Forge
                var objectName = $"{Guid.NewGuid()}_{blobName}";
                
                // Upload to Forge OSS
                var uploadResult = await _modelDerivativeService.UploadFileAsync(
                    TRANSLATION_BUCKET, 
                    objectName, 
                    azureStream
                );

                // Start translation
                var urn = await _modelDerivativeService.TranslateFileAsync(uploadResult.ObjectId);

                return Ok(new
                {
                    urn = urn,
                    objectId = uploadResult.ObjectId,
                    bucketKey = TRANSLATION_BUCKET,
                    message = "Translation started successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error translating Azure file {blobName}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("translate/upload")]
        public async Task<IActionResult> TranslateUploadedFile([FromForm] FileUploadModel model)
        {
            try
            {
                if (model.File == null || model.File.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }

                // Create bucket if it doesn't exist
                await _modelDerivativeService.CreateBucketAsync(TRANSLATION_BUCKET);

                // Generate unique object name
                var objectName = $"{Guid.NewGuid()}_{model.File.FileName}";

                // Upload to Forge OSS
                using var stream = model.File.OpenReadStream();
                var uploadResult = await _modelDerivativeService.UploadFileAsync(
                    TRANSLATION_BUCKET,
                    objectName,
                    stream
                );

                // Start translation
                var urn = await _modelDerivativeService.TranslateFileAsync(
                    uploadResult.ObjectId,
                    model.RootFilename
                );

                return Ok(new
                {
                    urn = urn,
                    objectId = uploadResult.ObjectId,
                    bucketKey = TRANSLATION_BUCKET,
                    message = "Translation started successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating uploaded file");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("translate/progress/{urn}")]
        public async Task<IActionResult> GetTranslationProgress(string urn)
        {
            try
            {
                var progress = await _modelDerivativeService.GetTranslationProgressAsync(urn);
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting translation progress for {urn}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("manifest/{urn}")]
        public async Task<IActionResult> GetManifest(string urn)
        {
            try
            {
                var manifest = await _modelDerivativeService.GetManifestAsync(urn);
                return Ok(manifest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting manifest for {urn}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("object/{bucketKey}/{objectId}")]
        public async Task<IActionResult> DeleteObject(string bucketKey, string objectId)
        {
            try
            {
                var result = await _modelDerivativeService.DeleteObjectAsync(bucketKey, objectId);
                if (result)
                {
                    return Ok(new { message = "Object deleted successfully" });
                }
                return StatusCode(500, new { error = "Failed to delete object" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting object {objectId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class FileUploadModel
    {
        public IFormFile File { get; set; }
        public string RootFilename { get; set; }
    }
}