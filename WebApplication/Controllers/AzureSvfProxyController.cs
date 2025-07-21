using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApplication.Services;

namespace WebApplication.Controllers
{
    /// <summary>
    /// Proxy controller to serve SVF files from Azure Blob Storage.
    /// This helps the Autodesk Viewer access Azure-hosted SVF files with proper authentication.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AzureSvfProxyController : ControllerBase
    {
        private readonly ILogger<AzureSvfProxyController> _logger;
        private readonly IAzureBlobService _azureBlobService;
        private readonly HttpClient _httpClient;

        public AzureSvfProxyController(
            ILogger<AzureSvfProxyController> logger,
            IAzureBlobService azureBlobService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _azureBlobService = azureBlobService;
            _httpClient = httpClientFactory.CreateClient();
        }

        /// <summary>
        /// Proxy endpoint to fetch SVF files from Azure Blob Storage.
        /// Handles URL encoding and authentication.
        /// </summary>
        [HttpGet("svf/{*path}")]
        public async Task<IActionResult> GetSvfFile(string path)
        {
            try
            {
                // Decode the path (handles forward slashes encoded as %2F)
                var decodedPath = WebUtility.UrlDecode(path);
                
                // Check if blob exists
                if (!await _azureBlobService.BlobExistsAsync(decodedPath))
                {
                    _logger.LogWarning($"SVF file not found: {decodedPath}");
                    return NotFound();
                }

                // Get SAS URL for the blob
                var sasUrl = await _azureBlobService.GenerateSasUrlAsync(
                    decodedPath, 
                    Azure.Storage.Sas.BlobSasPermissions.Read,
                    expirationMinutes: 30
                );

                // Fetch the file from Azure
                var response = await _httpClient.GetAsync(sasUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch SVF file from Azure: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode);
                }

                // Get content type
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                
                // Special handling for bubble.json
                if (Path.GetFileName(decodedPath) == "bubble.json")
                {
                    contentType = "application/json";
                }

                // Stream the content back to the client
                var stream = await response.Content.ReadAsStreamAsync();
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error proxying SVF file: {path}");
                return StatusCode(500, new { error = "Failed to fetch SVF file" });
            }
        }

        /// <summary>
        /// Extract and serve SVF files from a zipped SVF output.
        /// This is used when the SVF is stored as a ZIP file in Azure.
        /// </summary>
        [HttpGet("extract/{projectId}/{hash}/{*filePath}")]
        public async Task<IActionResult> ExtractFromZip(string projectId, string hash, string filePath)
        {
            try
            {
                var zipBlobPath = $"svf/{projectId}/{hash}/SvfOutput.zip";
                
                // Check if the ZIP exists
                if (!await _azureBlobService.BlobExistsAsync(zipBlobPath))
                {
                    _logger.LogWarning($"SVF ZIP not found: {zipBlobPath}");
                    return NotFound();
                }

                // Download the ZIP file
                using var zipStream = await _azureBlobService.DownloadBlobAsync(zipBlobPath);
                using var memoryStream = new MemoryStream();
                await zipStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Extract the requested file from ZIP
                using var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Read);
                
                // Normalize the file path
                var normalizedPath = filePath.Replace('\\', '/').TrimStart('/');
                
                // Find the entry
                var entry = archive.Entries.FirstOrDefault(e => 
                    e.FullName.Replace('\\', '/').Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                
                if (entry == null)
                {
                    _logger.LogWarning($"File not found in ZIP: {filePath}");
                    return NotFound();
                }

                // Determine content type
                var contentType = GetContentType(entry.Name);

                // Return the file
                using var entryStream = entry.Open();
                var fileBytes = new byte[entry.Length];
                await entryStream.ReadAsync(fileBytes, 0, fileBytes.Length);
                
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting file from SVF ZIP: {projectId}/{hash}/{filePath}");
                return StatusCode(500, new { error = "Failed to extract file from SVF archive" });
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".xml" => "application/xml",
                ".svf" => "application/octet-stream",
                ".pack" => "application/octet-stream",
                ".gz" => "application/gzip",
                _ => "application/octet-stream"
            };
        }
    }
}