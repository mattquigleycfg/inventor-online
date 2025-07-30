using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace WebApplication.Services
{
    public interface IAzureBlobService
    {
        Task<List<ModelInfo>> ListModelsAsync();
        Task<bool> IsConfiguredAsync();
        
        // File management methods
        Task<string> GenerateSasUrlAsync(string blobName, BlobSasPermissions permissions = BlobSasPermissions.Read, int expirationMinutes = 30);
        Task TransferFileAsync(string sourceUrl, string targetBlobName);
        Task UploadFileAsync(string localFilePath, string blobName);
        Task UploadStreamAsync(Stream stream, string blobName);
        Task<bool> BlobExistsAsync(string blobName);
        Task DeleteBlobAsync(string blobName);
        Task CopyBlobAsync(string sourceBlobName, string targetBlobName);
        Task<Stream> DownloadBlobAsync(string blobName);
        Task<List<string>> ListBlobsAsync(string prefix = null);
    }

    public class ModelInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Url { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class AzureBlobService : IAzureBlobService
    {
        private readonly ILogger<AzureBlobService> _logger;
        private readonly string _baseUrl;
        private readonly string _sasToken;
        private readonly string _connectionString;
        private readonly bool _isConfigured;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public AzureBlobService(IConfiguration configuration, ILogger<AzureBlobService> logger)
        {
            _logger = logger;
            
            // Get container name
            _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "models";
            
            // Priority: Environment variables > Local config > Default config
            _connectionString = configuration.GetConnectionString("AzureStorage") ?? 
                              configuration["AzureBlobStorage:ConnectionString"];
            
            if (!string.IsNullOrEmpty(_connectionString))
            {
                // Use connection string (preferred for production)
                _blobServiceClient = new BlobServiceClient(_connectionString);
                _isConfigured = true;
                _baseUrl = configuration["AzureBlobStorage:BaseUrl"] ?? "https://conform3d.blob.core.windows.net/models";
                _logger.LogInformation("Azure Blob Service configured with connection string");
            }
            else
            {
                // Fallback to SAS token approach
                _baseUrl = configuration["AzureBlobStorage:BaseUrl"] ?? Environment.GetEnvironmentVariable("AZURE_BLOB_BASE_URL");
                _sasToken = Environment.GetEnvironmentVariable("AZURE_BLOB_SAS_TOKEN") ?? configuration["AzureBlobStorage:SasToken"];
                
                if (!string.IsNullOrEmpty(_sasToken) && !string.IsNullOrEmpty(_baseUrl))
                {
                    var storageAccountName = ExtractStorageAccountName(_baseUrl);
                    var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/?{_sasToken}");
                    _blobServiceClient = new BlobServiceClient(blobServiceUri);
                    _isConfigured = true;
                    _logger.LogInformation("Azure Blob Service configured with SAS token");
                }
                else
                {
                    _isConfigured = false;
                    _logger.LogWarning("Azure Blob Service not configured - missing connection string or SAS token");
                }
            }
        }

        private string ExtractStorageAccountName(string baseUrl)
        {
            try
            {
                var uri = new Uri(baseUrl);
                var hostParts = uri.Host.Split('.');
                return hostParts[0];
            }
            catch
            {
                return "conform3d"; // fallback
            }
        }

        public async Task<bool> IsConfiguredAsync()
        {
            return await Task.FromResult(_isConfigured);
        }

        public async Task<List<ModelInfo>> ListModelsAsync()
        {
            if (!_isConfigured)
            {
                _logger.LogWarning("Azure Blob Service is not configured");
                return new List<ModelInfo>();
            }

            try
            {
                // For now, return the known models from your Azure storage
                var models = new List<ModelInfo>
                {
                    new ModelInfo
                    {
                        Name = "MRConfigurator",
                        DisplayName = "MR Configurator",
                        Url = $"{_baseUrl}/MRConfigurator/output/bubble.json?{_sasToken}",
                        ContentType = "application/json",
                        Size = 0,
                        LastModified = DateTime.UtcNow.AddDays(-1)
                    }
                };

                // Skip existence check here; viewer will handle 404 if path invalid.

                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing models from Azure Blob Storage");
                return new List<ModelInfo>();
            }
        }

        // Legacy methods - use BlobExistsAsync and DownloadBlobAsync instead
        public async Task<Stream> DownloadModelAsync(string modelName) => await DownloadBlobAsync(modelName);
        public async Task<bool> ModelExistsAsync(string modelName) => await BlobExistsAsync(modelName);
        public async Task<string> GetModelUrlAsync(string modelName) => await GenerateSasUrlAsync(modelName);

        public async Task<string> GenerateSasUrlAsync(string blobName, BlobSasPermissions permissions = BlobSasPermissions.Read, int expirationMinutes = 30)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                if (!string.IsNullOrEmpty(_connectionString))
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    if (blobClient.CanGenerateSasUri)
                    {
                        var sasBuilder = new BlobSasBuilder
                        {
                            BlobContainerName = _containerName,
                            BlobName = blobName,
                            Resource = "b",
                            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
                        };
                        sasBuilder.SetPermissions(permissions);

                        return blobClient.GenerateSasUri(sasBuilder).ToString();
                    }
                    else
                    {
                        throw new InvalidOperationException("Cannot generate SAS URI for blob");
                    }
                }
                else
                {
                    // Using SAS token from configuration
                    return $"{_baseUrl}/{blobName}?{_sasToken}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating SAS URL for blob {blobName}");
                throw;
            }
        }

        public async Task TransferFileAsync(string sourceUrl, string targetBlobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(30);

                using var response = await httpClient.GetAsync(sourceUrl);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                await UploadStreamAsync(stream, targetBlobName);

                _logger.LogInformation($"Successfully transferred file from {sourceUrl} to {targetBlobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring file from {sourceUrl} to {targetBlobName}");
                throw;
            }
        }

        public async Task UploadFileAsync(string localFilePath, string blobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                using var fileStream = File.OpenRead(localFilePath);
                await UploadStreamAsync(fileStream, blobName);
                _logger.LogInformation($"Successfully uploaded file {localFilePath} to {blobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading file {localFilePath} to {blobName}");
                throw;
            }
        }

        public async Task UploadStreamAsync(Stream stream, string blobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync();

                var blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(stream, overwrite: true);

                _logger.LogInformation($"Successfully uploaded stream to {blobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading stream to {blobName}");
                throw;
            }
        }

        public async Task<bool> BlobExistsAsync(string blobName)
        {
            if (!_isConfigured)
            {
                return false;
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if blob {blobName} exists");
                return false;
            }
        }

        public async Task DeleteBlobAsync(string blobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync();
                _logger.LogInformation($"Successfully deleted blob {blobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting blob {blobName}");
                throw;
            }
        }

        public async Task CopyBlobAsync(string sourceBlobName, string targetBlobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var sourceBlobClient = containerClient.GetBlobClient(sourceBlobName);
                var targetBlobClient = containerClient.GetBlobClient(targetBlobName);

                await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                _logger.LogInformation($"Successfully copied blob from {sourceBlobName} to {targetBlobName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error copying blob from {sourceBlobName} to {targetBlobName}");
                throw;
            }
        }

        public async Task<Stream> DownloadBlobAsync(string blobName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                var response = await blobClient.DownloadStreamingAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading blob {blobName}");
                throw;
            }
        }

        public async Task<List<string>> ListBlobsAsync(string prefix = null)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobs = new List<string>();

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    blobs.Add(blobItem.Name);
                }

                return blobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing blobs with prefix {prefix}");
                throw;
            }
        }
    }
} 