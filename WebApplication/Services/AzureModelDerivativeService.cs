using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace WebApplication.Services
{
    /// <summary>
    /// Service that bridges Azure Blob Storage with Autodesk Model Derivative API
    /// Handles the workflow: Azure Blob -> Forge OSS -> Model Derivative -> Viewer
    /// </summary>
    public interface IAzureModelDerivativeService
    {
        Task<TranslationResult> ProcessAzureBlobAsync(string azureBlobName, string bucketKey = null);
        Task<JobProgress> GetJobStatusAsync(string urn);
        Task<string> GetViewableUrnAsync(string azureBlobName);
        Task<List<ProcessedModel>> GetProcessedModelsAsync();
    }

    public class AzureModelDerivativeService : IAzureModelDerivativeService
    {
        private readonly ILogger<AzureModelDerivativeService> _logger;
        private readonly IAzureBlobService _azureBlobService;
        private readonly IModelDerivativeService _modelDerivativeService;
        private readonly IForgeOSS _forgeOSS;
        private readonly IConfiguration _configuration;
        private readonly string _defaultBucketPrefix;

        // In-memory cache for processed models (in production, use Redis or database)
        private static readonly Dictionary<string, ProcessedModel> _processedModels = new();

        public AzureModelDerivativeService(
            ILogger<AzureModelDerivativeService> logger,
            IAzureBlobService azureBlobService,
            IModelDerivativeService modelDerivativeService,
            IForgeOSS forgeOSS,
            IConfiguration configuration)
        {
            _logger = logger;
            _azureBlobService = azureBlobService;
            _modelDerivativeService = modelDerivativeService;
            _forgeOSS = forgeOSS;
            _configuration = configuration;
            _defaultBucketPrefix = configuration["Forge:BucketPrefix"] ?? "azure-models";
        }

        /// <summary>
        /// Process a file from Azure Blob Storage through Model Derivative API
        /// </summary>
        public async Task<TranslationResult> ProcessAzureBlobAsync(string azureBlobName, string bucketKey = null)
        {
            try
            {
                _logger.LogInformation($"Starting processing of Azure blob: {azureBlobName}");

                // Check if already processed
                if (_processedModels.ContainsKey(azureBlobName))
                {
                    var existing = _processedModels[azureBlobName];
                    if (existing.Status == "complete" || existing.Status == "inprogress")
                    {
                        _logger.LogInformation($"Model {azureBlobName} already processed or in progress");
                        return new TranslationResult
                        {
                            Success = true,
                            Urn = existing.Urn,
                            Status = existing.Status,
                            Message = "Model already processed"
                        };
                    }
                }

                // Step 1: Download from Azure
                _logger.LogInformation("Step 1: Downloading from Azure Blob Storage");
                using var azureStream = await _azureBlobService.DownloadBlobAsync(azureBlobName);
                
                // Create a memory stream to hold the data
                using var memoryStream = new MemoryStream();
                await azureStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Step 2: Create bucket if needed
                bucketKey = bucketKey ?? GenerateBucketKey(azureBlobName);
                _logger.LogInformation($"Step 2: Using bucket: {bucketKey}");
                await _modelDerivativeService.CreateBucketAsync(bucketKey);

                // Step 3: Upload to Forge OSS
                var objectName = SanitizeObjectName(azureBlobName);
                _logger.LogInformation($"Step 3: Uploading to Forge OSS as: {objectName}");
                
                memoryStream.Position = 0; // Reset position
                var objectDetails = await _modelDerivativeService.UploadFileAsync(
                    bucketKey, 
                    objectName, 
                    memoryStream
                );

                // Step 4: Start translation
                _logger.LogInformation($"Step 4: Starting translation for: {objectDetails.ObjectId}");
                
                // Determine root filename for composite files (like ZIP)
                string rootFilename = null;
                if (azureBlobName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // For Inventor assemblies, we need to specify the root file
                    rootFilename = await DetermineRootFilename(azureBlobName);
                }

                var urn = await _modelDerivativeService.TranslateFileAsync(
                    objectDetails.ObjectId, 
                    rootFilename
                );

                // Store the processed model info
                var processedModel = new ProcessedModel
                {
                    AzureBlobName = azureBlobName,
                    BucketKey = bucketKey,
                    ObjectId = objectDetails.ObjectId,
                    Urn = urn,
                    Status = "inprogress",
                    ProcessedAt = DateTime.UtcNow
                };
                _processedModels[azureBlobName] = processedModel;

                _logger.LogInformation($"Translation started successfully. URN: {urn}");

                return new TranslationResult
                {
                    Success = true,
                    Urn = urn,
                    Status = "inprogress",
                    Message = "Translation job started successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Azure blob: {azureBlobName}");
                return new TranslationResult
                {
                    Success = false,
                    Error = ex.Message,
                    Status = "failed"
                };
            }
        }

        /// <summary>
        /// Get the status of a translation job
        /// </summary>
        public async Task<JobProgress> GetJobStatusAsync(string urn)
        {
            try
            {
                var progress = await _modelDerivativeService.GetTranslationProgressAsync(urn);
                
                // Update cached status
                var cached = _processedModels.Values.FirstOrDefault(m => m.Urn == urn);
                if (cached != null)
                {
                    cached.Status = progress.Status;
                }

                return progress;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting job status for URN: {urn}");
                throw;
            }
        }

        /// <summary>
        /// Get the URN for viewing a processed model
        /// </summary>
        public async Task<string> GetViewableUrnAsync(string azureBlobName)
        {
            if (_processedModels.TryGetValue(azureBlobName, out var model))
            {
                // Check if translation is complete
                if (model.Status != "complete")
                {
                    var progress = await GetJobStatusAsync(model.Urn);
                    model.Status = progress.Status;
                }

                if (model.Status == "complete")
                {
                    return model.Urn;
                }
                else
                {
                    throw new InvalidOperationException($"Model translation not complete. Status: {model.Status}");
                }
            }

            throw new KeyNotFoundException($"Model {azureBlobName} has not been processed");
        }

        /// <summary>
        /// Get list of all processed models
        /// </summary>
        public async Task<List<ProcessedModel>> GetProcessedModelsAsync()
        {
            // Update statuses for in-progress models
            var inProgressModels = _processedModels.Values
                .Where(m => m.Status == "inprogress")
                .ToList();

            foreach (var model in inProgressModels)
            {
                try
                {
                    var progress = await _modelDerivativeService.GetTranslationProgressAsync(model.Urn);
                    model.Status = progress.Status;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating status for {model.AzureBlobName}");
                }
            }

            return _processedModels.Values.OrderByDescending(m => m.ProcessedAt).ToList();
        }

        #region Private Helper Methods

        private string GenerateBucketKey(string azureBlobName)
        {
            // Generate a unique bucket key based on the blob name
            var sanitized = azureBlobName
                .Replace("/", "-")
                .Replace(".", "-")
                .Replace(" ", "-")
                .ToLowerInvariant();

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
            return $"{_defaultBucketPrefix}-{sanitized}-{timestamp}".Substring(0, Math.Min(128, sanitized.Length));
        }

        private string SanitizeObjectName(string blobName)
        {
            // Forge OSS has restrictions on object names
            return Path.GetFileName(blobName)
                .Replace(" ", "_")
                .Replace("#", "_");
        }

        private async Task<string> DetermineRootFilename(string zipBlobName)
        {
            // For Inventor files, we need to determine the root assembly file
            // This is a simplified version - in production, you might want to:
            // 1. Download and inspect the ZIP file
            // 2. Look for .iam or .ipt files
            // 3. Use metadata to determine the root file

            // Common patterns for Inventor assemblies
            if (zipBlobName.Contains("Wrench", StringComparison.OrdinalIgnoreCase))
                return "Wrench.iam";
            if (zipBlobName.Contains("Wheel", StringComparison.OrdinalIgnoreCase))
                return "WheelAssembly.iam";
            if (zipBlobName.Contains("MRConfigurator", StringComparison.OrdinalIgnoreCase))
                return "MRConfigurator.iam";

            // Default: let Model Derivative try to figure it out
            return null;
        }

        #endregion
    }

    #region Data Models

    public class TranslationResult
    {
        public bool Success { get; set; }
        public string Urn { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class ProcessedModel
    {
        public string AzureBlobName { get; set; }
        public string BucketKey { get; set; }
        public string ObjectId { get; set; }
        public string Urn { get; set; }
        public string Status { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    #endregion
}