using System;
using System.Collections.Generic;
using Autodesk.Forge.DesignAutomation.Model;
using WebApplication.Definitions;
using WebApplication.Services;

namespace WebApplication.Processing
{
    /// <summary>
    /// SVF generator with direct Azure Blob Storage upload.
    /// Implements the optimized workflow from the APS guide.
    /// </summary>
    public class CreateSVFAzure : ForgeAppBase
    {
        private readonly IAzureBlobService _azureBlobService;
        
        public override string Id => nameof(CreateSVFAzure);
        public override string Description => "Generate SVF and upload directly to Azure Blob Storage";

        protected override string OutputName => "SvfOutput";
        protected override bool IsOutputZip => true;

        public CreateSVFAzure(Publisher publisher, IAzureBlobService azureBlobService) : base(publisher) 
        {
            _azureBlobService = azureBlobService;
        }

        public override Dictionary<string, IArgument> ToWorkItemArgs(ProcessingArgs data)
        {
            // Get base arguments
            var args = base.ToWorkItemArgs(data);
            
            // Add Azure output argument if Azure is configured
            if (_azureBlobService.IsConfiguredAsync().GetAwaiter().GetResult())
            {
                // Generate a unique blob path based on timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var blobPath = $"svf/output_{timestamp}/SvfOutput.zip";
                
                // Generate SAS URL with write permissions
                var sasUrl = _azureBlobService.GenerateSasUrlAsync(
                    blobPath, 
                    Azure.Storage.Sas.BlobSasPermissions.Write | Azure.Storage.Sas.BlobSasPermissions.Create,
                    expirationMinutes: 60
                ).GetAwaiter().GetResult();

                // Add onDemand output argument for direct Azure upload
                args["AzureOutput"] = new XrefTreeArgument
                {
                    Url = sasUrl,
                    Verb = Verb.Put,
                    LocalName = "SvfOutputAzure",
                    Optional = true,
                    Headers = new Dictionary<string, string>
                    {
                        { "x-ms-blob-type", "BlockBlob" }
                    }
                };
                
                // Store the Azure URL in the output URL for later retrieval
                data.SvfUrl = sasUrl;
            }
            
            return args;
        }

        protected override string OutputUrl(ProcessingArgs data)
        {
            // Return the stored SVF URL (which might be Azure URL if configured)
            return data.SvfUrl ?? base.OutputUrl(data);
        }
    }
}