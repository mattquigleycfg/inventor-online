using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication.Services
{
    public interface IAzureBlobService
    {
        Task<List<ModelInfo>> ListModelsAsync();
        Task<Stream> DownloadModelAsync(string modelName);
        Task<string> GetModelUrlAsync(string modelName);
        Task<bool> ModelExistsAsync(string modelName);
        Task<bool> IsConfiguredAsync();
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

        public AzureBlobService(IConfiguration configuration, ILogger<AzureBlobService> logger)
        {
            _logger = logger;
            
            // Try to get connection string first
            _connectionString = configuration.GetConnectionString("AzureStorage") ?? 
                              configuration["AzureBlobStorage:ConnectionString"];
            
            // If no connection string, use the direct URL approach
            if (string.IsNullOrEmpty(_connectionString))
            {
                _baseUrl = configuration["AzureBlobStorage:BaseUrl"] ?? "https://conform3d.blob.core.windows.net/models";
                _sasToken = configuration["AzureBlobStorage:SasToken"] ?? 
                           "sp=racw&st=2025-07-16T23:35:30Z&se=2029-07-17T07:50:30Z&spr=https&sv=2024-11-04&sr=c&sig=uUgzyjVHttrn4ZsYLWzAZbWTPctWbjwEpZdLpowqZgk%3D";
                _isConfigured = !string.IsNullOrEmpty(_sasToken);
            }
            else
            {
                // Parse connection string for direct access
                _baseUrl = "https://conform3d.blob.core.windows.net/models";
                _isConfigured = true;
            }

            _logger.LogInformation($"Azure Blob Service configured: {_isConfigured}");
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
    }
} 