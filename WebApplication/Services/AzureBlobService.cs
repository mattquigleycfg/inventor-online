using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace WebApplication.Services
{
    public interface IAzureBlobService
    {
        Task<List<ModelInfo>> ListModelsAsync();
        Task<Stream> DownloadModelAsync(string modelName);
        Task<string> GetModelUrlAsync(string modelName);
        Task<bool> ModelExistsAsync(string modelName);
    }

    public class ModelInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Url { get; set; }
        public string ContentType { get; set; }
    }

    public class AzureBlobService : IAzureBlobService
    {
        private readonly ILogger<AzureBlobService> _logger;
        private readonly string _baseUrl;
        private readonly string _sasToken;

        public AzureBlobService(IConfiguration configuration, ILogger<AzureBlobService> logger)
        {
            _logger = logger;
            
            // Extract base URL and SAS token from your provided information
            _baseUrl = "https://conform3d.blob.core.windows.net/models";
            _sasToken = "sp=racw&st=2025-07-16T23:35:30Z&se=2029-07-17T07:50:30Z&spr=https&sv=2024-11-04&sr=c&sig=uUgzyjVHttrn4ZsYLWzAZbWTPctWbjwEpZdLpowqZgk%3D";
        }

        public async Task<List<ModelInfo>> ListModelsAsync()
        {
            // For now, return the known model from your upload
            var models = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Name = "MRConfigurator.zip",
                    DisplayName = "MR Configurator",
                    Url = $"{_baseUrl}/MRConfigurator.zip?{_sasToken}",
                    ContentType = "application/zip"
                }
            };

            return await Task.FromResult(models);
        }

        public async Task<Stream> DownloadModelAsync(string modelName)
        {
            try
            {
                var url = $"{_baseUrl}/{modelName}?{_sasToken}";
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
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
            return await Task.FromResult($"{_baseUrl}/{modelName}?{_sasToken}");
        }

        public async Task<bool> ModelExistsAsync(string modelName)
        {
            try
            {
                var url = $"{_baseUrl}/{modelName}?{_sasToken}";
                using var httpClient = new HttpClient();
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if model {modelName} exists");
                return false;
            }
        }
    }
} 