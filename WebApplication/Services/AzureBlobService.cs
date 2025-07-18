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
        Task<Stream> DownloadModelAsync(string modelName);
        Task<string> GetModelUrlAsync(string modelName);
        Task<bool> ModelExistsAsync(string modelName);
        Task<bool> IsConfiguredAsync();
        
        // New methods for file management
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
            
            // Try to get connection string first
            _connectionString = configuration.GetConnectionString("AzureStorage") ?? 
                              configuration["AzureBlobStorage:ConnectionString"];
            
            _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "models";
            
            // If no connection string, use the direct URL approach
            if (string.IsNullOrEmpty(_connectionString))
            {
                _baseUrl = configuration["AzureBlobStorage:BaseUrl"] ?? "https://conform3d.blob.core.windows.net/models";
                _sasToken = configuration["AzureBlobStorage:SasToken"] ?? 
                           "sp=r&st=2025-07-18T01:15:44Z&se=2025-07-18T09:30:44Z&spr=https&sv=2024-11-04&sr=c&sig=vHQUX6uWbsKV5IVkB4d297nP3No1wBkDpUTzdTaxre8%3D";
                _isConfigured = !string.IsNullOrEmpty(_sasToken);
                
                // Create BlobServiceClient with SAS token
                if (_isConfigured)
                {
                    var storageAccountName = ExtractStorageAccountName(_baseUrl);
                    var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/?{_sasToken}");
                    _blobServiceClient = new BlobServiceClient(blobServiceUri);
                }
            }
            else
            {
                // Parse connection string for direct access
                _baseUrl = "https://conform3d.blob.core.windows.net/models";
                _isConfigured = true;
                _blobServiceClient = new BlobServiceClient(_connectionString);
            }

            _logger.LogInformation($"Azure Blob Service configured: {_isConfigured}");
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
                        Name = "MRConfigurator.zip",
                        DisplayName = "MR Configurator",
                        Url = $"{_baseUrl}/MRConfigurator.zip?{_sasToken}",
                        ContentType = "application/zip",
                        Size = 0, // We'll get this from HEAD request if needed
                        LastModified = DateTime.UtcNow.AddDays(-1)
                    }
                };

                // Verify the model exists
                foreach (var model in models.ToList())
                {
                    var exists = await ModelExistsAsync(model.Name);
                    if (!exists)
                    {
                        _logger.LogWarning($"Model {model.Name} does not exist in Azure Storage");
                        models.Remove(model);
                    }
                }

                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing models from Azure Blob Storage");
                return new List<ModelInfo>();
            }
        }

        public async Task<Stream> DownloadModelAsync(string modelName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            try
            {
                var url = $"{_baseUrl}/{modelName}?{_sasToken}";
                using var httpClient = new HttpClient();
                
                // Set timeout for large files
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                _logger.LogInformation($"Successfully downloaded {modelName} from Azure Blob Storage");
                return await response.Content.ReadAsStreamAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading model {modelName} from Azure Blob Storage");
                throw;
            }
        }

        public async Task<string> GetModelUrlAsync(string modelName)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Azure Blob Service is not configured");
            }

            return await Task.FromResult($"{_baseUrl}/{modelName}?{_sasToken}");
        }

        public async Task<bool> ModelExistsAsync(string modelName)
        {
            if (!_isConfigured)
            {
                return false;
            }

            try
            {
                var url = $"{_baseUrl}/{modelName}?{_sasToken}";
                using var httpClient = new HttpClient();
                
                // Use HEAD request to check existence without downloading
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await httpClient.SendAsync(request);
                
                var exists = response.IsSuccessStatusCode;
                _logger.LogInformation($"Model {modelName} exists: {exists}");
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if model {modelName} exists");
                return false;
            }
        }

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