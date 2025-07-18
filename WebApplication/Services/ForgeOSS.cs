/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Autodesk Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Client;
using Autodesk.Forge.Core;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Polly;
using WebApplication.Utilities;

namespace WebApplication.Services
{
    /// <summary>
    /// Class to work with Forge APIs.
    /// </summary>
    public class ForgeOSS : IForgeOSS
    {
        /// <summary>
        /// Page size for "Get Bucket Objects" operation.
        /// </summary>
        private const int PageSize = 50;

        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<ForgeOSS> _logger;
        private static readonly Scope[] _scope = { Scope.DataRead, Scope.DataWrite, Scope.BucketCreate, Scope.BucketDelete, Scope.BucketRead, Scope.CodeAll };

        private readonly IAsyncPolicy _ossResiliencyPolicy;

        public Task<string> TwoLeggedAccessToken => _twoLeggedAccessToken.Value;
        private Lazy<Task<string>> _twoLeggedAccessToken;

        /// <summary>
        /// Forge configuration.
        /// </summary>
        public ForgeConfiguration Configuration { get; }

        private string OssV2Base => $"{Configuration.AuthenticationAddress.GetLeftPart(UriPartial.Authority)}/oss/v2";

        /// <summary>
        /// Constructor.
        /// </summary>
        public ForgeOSS(IHttpClientFactory clientFactory, IOptions<ForgeConfiguration> optionsAccessor, ILogger<ForgeOSS> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            Configuration = optionsAccessor.Value.Validate();

            string apiBaseUrl = Configuration.AuthenticationAddress.GetLeftPart(System.UriPartial.Authority);
            Autodesk.Forge.Client.Configuration.Default.setApiClientUsingDefault(new ApiClient(apiBaseUrl));

            RefreshApiToken();

            // create policy to refresh API token on expiration (401 error code)
            var refreshTokenPolicy = Policy
                                    .Handle<ApiException>(e => e.ErrorCode == StatusCodes.Status401Unauthorized)
                                    .RetryAsync(5, (_, __) => RefreshApiToken());

            var bulkHeadPolicy = Policy.BulkheadAsync(10, int.MaxValue);
            var rateLimitRetryPolicy = Policy
                .Handle<ApiException>(e => e.ErrorCode == StatusCodes.Status429TooManyRequests)
                .WaitAndRetryAsync(new[] {
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(20),
                    TimeSpan.FromSeconds(40)
                });
            _ossResiliencyPolicy = refreshTokenPolicy.WrapAsync(rateLimitRetryPolicy).WrapAsync(bulkHeadPolicy);
        }

        public static bool PropertyExists(dynamic obj, string name)
        {
            if (obj == null) return false;
            if (obj is IDictionary<string, object> dict)
            {
                return dict.ContainsKey(name);
            }

            var property = obj.GetType().GetProperty(name);
            return property != null;
        }

        public async Task<List<ObjectDetails>> GetBucketObjectsAsync(string bucketKey, string beginsWith = null)
        {
            var objects = new List<ObjectDetails>();
            string startAt = null; // next page pointer

            do
            {
                DynamicJsonResponse response = await WithObjectsApiAsync(async api =>
                {
                    return await api.GetObjectsAsync(bucketKey, PageSize, beginsWith, startAt);
                });
                

                foreach (KeyValuePair<string, dynamic> objInfo in new DynamicDictionaryItems((response as dynamic).items))
                {
                    dynamic item = objInfo.Value;

                    var details = new ObjectDetails
                    {
                        BucketKey = item.bucketKey,
                        ObjectId = item.objectId,
                        ObjectKey = item.objectKey,
                        Sha1 = Encoding.ASCII.GetBytes(item.sha1),
                        Size = (int?)item.size,
                        Location = item.location
                    };
                    objects.Add(details);
                }

                startAt = GetNextStartAt(response.Dictionary);

            } while (startAt != null);

            return objects;
        }

        /// <summary>
        /// List all buckets
        /// </summary>
        /// <returns>List of all buckets for the account</returns>
        public async Task<List<string>> GetBucketsAsync()
        {
            var buckets = new List<string>();
            string startAt = null;

            do
            {
                dynamic bucketList = await WithBucketApiAsync(async api =>
                {
                    return await api.GetBucketsAsync(/* use default (US region) */ null, PageSize, startAt);
                });

                foreach (KeyValuePair<string, dynamic> bucketInfo in new DynamicDictionaryItems(bucketList.items))
                {
                    buckets.Add(bucketInfo.Value.bucketKey);
                }

                startAt = GetNextStartAt(bucketList.Dictionary);

            } while (startAt != null);

            return buckets;
        }

        private string GetNextStartAt(Dictionary<string, object> dict)
        {
            string startAt = null;
            // check if there is a next page with projects
            if (dict.TryGetValue("next", out var nextPage))
            {
                string nextPageUrl = (string)nextPage;
                if (!string.IsNullOrEmpty(nextPageUrl))
                {
                    Uri nextUri = new Uri(nextPageUrl, UriKind.Absolute);
                    Dictionary<string, StringValues> query = QueryHelpers.ParseNullableQuery(nextUri.Query);
                    startAt = query["startAt"];
                }
            }

            return startAt;
        }

      /// <summary>
      /// Create bucket with given name
      /// </summary>
      /// <param name="bucketKey">The bucket name.</param>
      public async Task CreateBucketAsync(string bucketKey)
        {
            await WithBucketApiAsync(async api =>
            {
                var payload = new PostBucketsPayload(bucketKey, /*allow*/null, PostBucketsPayload.PolicyKeyEnum.Persistent);
                await api.CreateBucketAsync(payload, /* use default (US region) */ null);
            });
        }

        public async Task DeleteBucketAsync(string bucketKey)
        {
            await WithBucketApiAsync(async api => await api.DeleteBucketAsync(bucketKey));
        }

        /// <summary>
        /// Generate a signed URL to OSS object.
        /// NOTE: An empty object created if not exists.
        /// </summary>
        /// <param name="bucketKey">Bucket key.</param>
        /// <param name="objectName">Object name.</param>
        /// <param name="access">Requested access to the object.</param>
        /// <param name="minutesExpiration">Minutes while the URL is valid. Default is 30 minutes.</param>
        /// <returns>Signed URL</returns>
        public async Task<string> CreateSignedUrlAsync(string bucketKey, string objectName, ObjectAccess access = ObjectAccess.Read, int minutesExpiration = 30)
        {
            return await WithObjectsApiAsync(async api => await GetSignedUrl(api, bucketKey, objectName, access, minutesExpiration));
        }

        public async Task UploadObjectAsync(string bucketKey, string objectName, Stream stream)
        {
            await WithObjectsApiAsync(async api => await api.UploadObjectAsync(bucketKey, objectName, 0, stream));
        }

        public async Task UploadChunkAsync(string bucketKey, string objectName, string contentRange, string sessionId, Stream stream)
        {
            await WithObjectsApiAsync(async api => await api.UploadChunkAsync(bucketKey, objectName, 0, contentRange, sessionId, stream));
        }

        /// <summary>
        /// Rename object.
        /// </summary>
        /// <param name="bucketKey">Bucket key.</param>
        /// <param name="oldName">Old object name.</param>
        /// <param name="newName">New object name.</param>
        public async Task RenameObjectAsync(string bucketKey, string oldName, string newName)
        {
            // OSS does not support renaming, so emulate it with more ineffective operations
            await WithObjectsApiAsync(async api => await api.CopyToAsync(bucketKey, oldName, newName));
            await WithObjectsApiAsync(async api => await api.DeleteObjectAsync(bucketKey, oldName));   
        }

        public async Task<Autodesk.Forge.Client.ApiResponse<dynamic>> GetObjectAsync(string bucketKey, string objectName)
        {
            return await WithObjectsApiAsync(async api => await api.GetObjectAsyncWithHttpInfo(bucketKey, objectName));
        }

        /// <summary>
        /// Copy OSS object.
        /// </summary>
        public async Task CopyAsync(string bucketKey, string fromName, string toName)
        {
            await WithObjectsApiAsync(async api => await api.CopyToAsync(bucketKey, fromName, toName));
        }

        /// <summary>
        /// Delete OSS object.
        /// </summary>
        public async Task DeleteAsync(string bucketKey, string objectName)
        {
            await WithObjectsApiAsync(async api => await api.DeleteObjectAsync(bucketKey, objectName));
        }

        /// <summary>
        /// Download OSS file.
        /// </summary>
        public async Task DownloadFileAsync(string bucketKey, string objectName, string localFullName)
        {
            var url = await CreateSignedUrlAsync(bucketKey, objectName);

            var client = _clientFactory.CreateClient();
            await client.DownloadAsync(url, localFullName);
        }

        /// <summary>
        /// Get profile for the user with the access token.
        /// </summary>
        /// <param name="token">Oxygen access token.</param>
        /// <returns>Dynamic object with User Profile</returns>
        /// <remarks>
        /// User Profile fields: https://forge.autodesk.com/en/docs/oauth/v2/reference/http/users-@me-GET/#body-structure-200
        /// </remarks>
        public async Task<dynamic> GetProfileAsync(string token)
        {
            var api = new UserProfileApi(new Configuration { AccessToken = token }); // TODO: use Polly cache policy!
            return await api.GetUserProfileAsync();
        }

        /// <summary>
        /// Run action against Buckets OSS API.
        /// </summary>
        /// <remarks>The action runs with retry policy to handle API token expiration.</remarks>
        private async Task WithBucketApiAsync(Func<BucketsApi, Task> action)
        {
            await _ossResiliencyPolicy.ExecuteAsync(async () =>
                    {
                        var api = new BucketsApi(OssV2Base);
                        string token = await TwoLeggedAccessToken;
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            throw new InvalidOperationException("Forge access token could not be obtained.");
                        }
                        api.Configuration.AddDefaultHeader("Authorization", $"Bearer {token}");
                        await action(api);
                    });
        }

        /// <summary>
        /// Run action against Buckets OSS API.
        /// </summary>
        /// <remarks>The action runs with retry policy to handle API token expiration.</remarks>
        private async Task<T> WithBucketApiAsync<T>(Func<BucketsApi, Task<T>> action)
        {
            return await _ossResiliencyPolicy.ExecuteAsync(async () =>
            {
                var api = new BucketsApi(OssV2Base);
                string token = await TwoLeggedAccessToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("Forge access token could not be obtained.");
                }
                api.Configuration.AddDefaultHeader("Authorization", $"Bearer {token}");
                return await action(api);
            });
        }

        /// <summary>
        /// Run action against Objects OSS API.
        /// </summary>
        /// <remarks>The action runs with retry policy to handle API token expiration.</remarks>
        private async Task WithObjectsApiAsync(Func<ObjectsApi, Task> action)
        {
            await _ossResiliencyPolicy.ExecuteAsync(async () =>
                    {
                        var api = new ObjectsApi(OssV2Base);
                        string token = await TwoLeggedAccessToken;
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            throw new InvalidOperationException("Forge access token could not be obtained.");
                        }
                        api.Configuration.AddDefaultHeader("Authorization", $"Bearer {token}");
                        await action(api);
                    });
        }

        /// <summary>
        /// Run action against Objects OSS API.
        /// </summary>
        /// <remarks>The action runs with retry policy to handle API token expiration.</remarks>
        private async Task<T> WithObjectsApiAsync<T>(Func<ObjectsApi, Task<T>> action)
        {
            return await _ossResiliencyPolicy.ExecuteAsync(async () =>
            {
                var api = new ObjectsApi(OssV2Base);
                string token = await TwoLeggedAccessToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("Forge access token could not be obtained.");
                }
                api.Configuration.AddDefaultHeader("Authorization", $"Bearer {token}");
                return await action(api);
            });
        }

        private static string AsString(ObjectAccess access)
        {
            return access.ToString().ToLowerInvariant();
        }

        private void RefreshApiToken()
        {
            _twoLeggedAccessToken = new Lazy<Task<string>>(async () => await _2leggedAsync());
        }

        private async Task<string> _2leggedAsync()
        {
            _logger.LogInformation("Refreshing Forge token using v2 endpoint");

            using var httpClient = _clientFactory.CreateClient();
            
            // Encode client credentials for Basic authentication (v2 requirement)
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{Configuration.ClientId}:{Configuration.ClientSecret}")
            );
            
            // Set up headers for v2 endpoint
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            
            // Prepare form data (client credentials NOT in body for v2)
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "data:read data:write bucket:create bucket:delete bucket:read code:all")
            };
            
            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            
            // Call v2 token endpoint
            string tokenEndpoint = $"{Configuration.AuthenticationAddress.GetLeftPart(System.UriPartial.Authority)}/authentication/v2/token";
            
            try
            {
                var response = await httpClient.PostAsync(tokenEndpoint, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get v2 token. Status: {response.StatusCode}, Content: {errorContent}");
                    throw new Exception($"Request failed with HTTP response {response.StatusCode}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (!tokenResponse.TryGetProperty("access_token", out var accessTokenElement))
                {
                    _logger.LogError($"No access_token in response: {responseContent}");
                    throw new Exception("No access_token in response");
                }
                
                string accessToken = accessTokenElement.GetString();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError($"Received empty access_token in response: {responseContent}");
                    throw new Exception("Received empty access_token");
                }
                _logger.LogInformation("Successfully obtained v2 access token");
                
                return accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining v2 access token");
                throw;
            }
        }

        /// <summary>
        /// Generate signed URL for the OSS object.
        /// </summary>
        private static async Task<string> GetSignedUrl(IObjectsApi api, string bucketKey, string objectName,
                                                            ObjectAccess access = ObjectAccess.Read, int minutesExpiration = 30)
        {
            var signature = new PostBucketsSigned(minutesExpiration);
            dynamic result = await api.CreateSignedResourceAsync(bucketKey, objectName, signature, AsString(access));
            return result.signedUrl;
        }
    }
}
