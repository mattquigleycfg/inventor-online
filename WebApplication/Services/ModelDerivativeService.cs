using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebApplication.Services;

namespace WebApplication.Services
{
    public interface IModelDerivativeService
    {
        Task<string> TranslateFileAsync(string objectId, string rootFilename = null);
        Task<JobProgress> GetTranslationProgressAsync(string urn);
        Task<Manifest> GetManifestAsync(string urn);
        Task<string> CreateBucketAsync(string bucketKey);
        Task<ObjectDetails> UploadFileAsync(string bucketKey, string objectName, Stream fileStream);
        Task<string> GetViewerUrlAsync(string urn);
        Task<bool> DeleteObjectAsync(string bucketKey, string objectId);
    }

    public class ModelDerivativeService : IModelDerivativeService
    {
        private readonly ILogger<ModelDerivativeService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IForgeOSS _forgeOSS;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private HttpClient _httpClient;

        public ModelDerivativeService(
            IConfiguration configuration, 
            ILogger<ModelDerivativeService> logger,
            IForgeOSS forgeOSS)
        {
            _configuration = configuration;
            _logger = logger;
            _forgeOSS = forgeOSS;
            _clientId = configuration["Forge:clientId"];
            _clientSecret = configuration["Forge:clientSecret"];
            _httpClient = new HttpClient();
        }

        public async Task<string> CreateBucketAsync(string bucketKey)
        {
            try
            {
                var scope = new Scope[] { Scope.BucketCreate, Scope.BucketRead, Scope.DataRead, Scope.DataWrite };
                var token = await GetAccessTokenAsync(scope);
                
                var api = new BucketsApi();
                api.Configuration.AccessToken = token.AccessToken;

                var bucketPayload = new PostBucketsPayload(
                    bucketKey,
                    null,
                    PostBucketsPayload.PolicyKeyEnum.Transient
                );

                var result = await api.CreateBucketAsync(bucketPayload);
                _logger.LogInformation($"Bucket created: {result.BucketKey}");
                return result.BucketKey;
            }
            catch (Autodesk.Forge.Client.ApiException ex)
            {
                if (ex.ErrorCode == 409) // Bucket already exists
                {
                    _logger.LogInformation($"Bucket {bucketKey} already exists");
                    return bucketKey;
                }
                _logger.LogError(ex, $"Error creating bucket {bucketKey}");
                throw;
            }
        }

        public async Task<ObjectDetails> UploadFileAsync(string bucketKey, string objectName, Stream fileStream)
        {
            try
            {
                var scope = new Scope[] { Scope.DataWrite, Scope.DataRead };
                var token = await GetAccessTokenAsync(scope);

                var api = new ObjectsApi();
                api.Configuration.AccessToken = token.AccessToken;

                var result = await api.UploadObjectAsync(
                    bucketKey,
                    objectName,
                    (int)fileStream.Length,
                    fileStream,
                    "application/octet-stream"
                );

                _logger.LogInformation($"File uploaded: {result.ObjectId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading file {objectName} to bucket {bucketKey}");
                throw;
            }
        }

        public async Task<string> TranslateFileAsync(string objectId, string rootFilename = null)
        {
            try
            {
                var scope = new Scope[] { Scope.DataRead, Scope.DataWrite, Scope.DataCreate };
                var token = await GetAccessTokenAsync(scope);

                var urn = Base64Encode(objectId);
                
                var translateRequest = new
                {
                    input = new
                    {
                        urn = urn,
                        rootFilename = rootFilename,
                        compressedUrn = false
                    },
                    output = new
                    {
                        formats = new[]
                        {
                            new
                            {
                                type = "svf",
                                views = new[] { "2d", "3d" }
                            }
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(translateRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token.AccessToken);

                var response = await _httpClient.PostAsync(
                    "https://developer.api.autodesk.com/modelderivative/v2/designdata/job",
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JObject.Parse(responseContent);
                    _logger.LogInformation($"Translation job started for URN: {urn}");
                    return urn;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Translation failed: {error}");
                    throw new Exception($"Translation failed: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error translating file {objectId}");
                throw;
            }
        }

        public async Task<JobProgress> GetTranslationProgressAsync(string urn)
        {
            try
            {
                var scope = new Scope[] { Scope.DataRead };
                var token = await GetAccessTokenAsync(scope);

                var api = new DerivativesApi();
                api.Configuration.AccessToken = token.AccessToken;

                var manifest = await api.GetManifestAsync(urn);
                
                var progress = new JobProgress
                {
                    Status = manifest.Status,
                    Progress = manifest.Progress,
                    HasDerivatives = manifest.Derivatives?.Count > 0
                };

                _logger.LogInformation($"Translation progress for {urn}: {progress.Status} - {progress.Progress}");
                return progress;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting translation progress for {urn}");
                throw;
            }
        }

        public async Task<Manifest> GetManifestAsync(string urn)
        {
            try
            {
                var scope = new Scope[] { Scope.DataRead };
                var token = await GetAccessTokenAsync(scope);

                var api = new DerivativesApi();
                api.Configuration.AccessToken = token.AccessToken;

                return await api.GetManifestAsync(urn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting manifest for {urn}");
                throw;
            }
        }

        public async Task<string> GetViewerUrlAsync(string urn)
        {
            // For viewing, we just need the URN - the viewer will handle the rest
            return await Task.FromResult(urn);
        }

        public async Task<bool> DeleteObjectAsync(string bucketKey, string objectId)
        {
            try
            {
                var scope = new Scope[] { Scope.DataWrite };
                var token = await GetAccessTokenAsync(scope);

                var api = new ObjectsApi();
                api.Configuration.AccessToken = token.AccessToken;

                await api.DeleteObjectAsync(bucketKey, objectId);
                _logger.LogInformation($"Object deleted: {objectId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting object {objectId}");
                return false;
            }
        }

        private async Task<Bearer> GetAccessTokenAsync(Scope[] scopes)
        {
            var auth = new TwoLeggedApi();
            return await auth.AuthenticateAsync(_clientId, _clientSecret, "client_credentials", scopes);
        }

        private string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    public class JobProgress
    {
        public string Status { get; set; }
        public string Progress { get; set; }
        public bool HasDerivatives { get; set; }
    }
}