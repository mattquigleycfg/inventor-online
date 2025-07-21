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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private string _accessToken;
        private DateTime _tokenExpiry;

        public ModelDerivativeService(
            IConfiguration configuration, 
            ILogger<ModelDerivativeService> logger,
            IForgeOSS forgeOSS,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _forgeOSS = forgeOSS;
            _httpClientFactory = httpClientFactory;
            _clientId = configuration["Forge:clientId"];
            _clientSecret = configuration["Forge:clientSecret"];
        }

        public async Task<string> CreateBucketAsync(string bucketKey)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                
                var api = new BucketsApi();
                api.Configuration.AccessToken = accessToken;

                var bucketPayload = new PostBucketsPayload(
                    bucketKey,
                    null,
                    PostBucketsPayload.PolicyKeyEnum.Transient
                );

                dynamic result = await api.CreateBucketAsync(bucketPayload);
                string createdBucketKey = result.bucketKey;
                _logger.LogInformation($"Bucket created: {createdBucketKey}");
                return createdBucketKey;
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
                // Use the existing ForgeOSS service to upload the file
                await _forgeOSS.UploadObjectAsync(bucketKey, objectName, fileStream);
                
                // Get the uploaded object details
                var objects = await _forgeOSS.GetBucketObjectsAsync(bucketKey, objectName);
                var uploadedObject = objects.Find(o => o.ObjectKey == objectName);
                
                if (uploadedObject != null)
                {
                    _logger.LogInformation($"File uploaded: {uploadedObject.ObjectId}");
                    return uploadedObject;
                }
                else
                {
                    // If we can't find the object in the list, create a basic ObjectDetails
                    var objectDetails = new ObjectDetails
                    {
                        BucketKey = bucketKey,
                        ObjectKey = objectName,
                        ObjectId = $"urn:adsk.objects:os.object:{bucketKey}/{objectName}"
                    };
                    _logger.LogInformation($"File uploaded: {objectDetails.ObjectId}");
                    return objectDetails;
                }
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
                var accessToken = await GetAccessTokenAsync();

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

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.PostAsync(
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
                var accessToken = await GetAccessTokenAsync();

                var api = new DerivativesApi();
                api.Configuration.AccessToken = accessToken;

                dynamic manifest = await api.GetManifestAsync(urn);
                
                var progress = new JobProgress
                {
                    Status = manifest.status,
                    Progress = manifest.progress,
                    HasDerivatives = manifest.derivatives != null && manifest.derivatives.Count > 0
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
                var accessToken = await GetAccessTokenAsync();

                var api = new DerivativesApi();
                api.Configuration.AccessToken = accessToken;

                dynamic result = await api.GetManifestAsync(urn);
                
                // Create Manifest object from dynamic result
                var manifest = new Manifest();
                manifest.Urn = result.urn;
                manifest.Status = result.status;
                manifest.Progress = result.progress;
                manifest.Type = result.type;
                manifest.Region = result.region;
                
                return manifest;
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
                var accessToken = await GetAccessTokenAsync();

                var api = new ObjectsApi();
                api.Configuration.AccessToken = accessToken;

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


        private string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // Check if we have a valid token
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
            {
                return _accessToken;
            }

            // Get new token
            _logger.LogInformation("Refreshing Forge access token for Model Derivative");
            
            using var httpClient = _httpClientFactory.CreateClient();
            
            // Encode client credentials for Basic authentication (v2 requirement)
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")
            );
            
            // Set up headers for v2 endpoint
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            
            // Prepare form data (client credentials NOT in body for v2)
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "data:read data:write data:create bucket:create bucket:read bucket:delete")
            });

            var response = await httpClient.PostAsync(
                "https://developer.api.autodesk.com/authentication/v2/token",
                requestBody
            );

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                dynamic result = JObject.Parse(content);
                
                _accessToken = result.access_token;
                int expiresIn = result.expires_in;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                
                _logger.LogInformation("Successfully obtained Forge access token");
                return _accessToken;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to get access token: {error}");
                throw new Exception($"Failed to get access token: {response.StatusCode}");
            }
        }
    }

    public class JobProgress
    {
        public string Status { get; set; }
        public string Progress { get; set; }
        public bool HasDerivatives { get; set; }
    }
}