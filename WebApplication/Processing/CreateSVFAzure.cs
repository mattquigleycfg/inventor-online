using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Forge.DesignAutomation.Model;
using Microsoft.Extensions.Logging;
using WebApplication.Definitions;
using WebApplication.Services;
using WebApplication.Utilities;

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

        protected override string OutputUrl(ProcessingArgs projectData) => projectData.SvfUrl;
        protected override string OutputName => "SvfOutput";
        protected override bool IsOutputZip => true;

        public CreateSVFAzure(Publisher publisher, IAzureBlobService azureBlobService) : base(publisher) 
        {
            _azureBlobService = azureBlobService;
        }

        protected override async Task<List<Autodesk.Forge.DesignAutomation.Model.Argument>> BuildArgumentsAsync(ProcessingArgs data)
        {
            var arguments = await base.BuildArgumentsAsync(data);
            
            // Add onDemand argument for direct Azure upload
            if (await _azureBlobService.IsConfiguredAsync())
            {
                var azureOutputArg = await CreateAzureOutputArgumentAsync(data);
                arguments.Add(azureOutputArg);
            }
            
            return arguments;
        }

        private async Task<Autodesk.Forge.DesignAutomation.Model.Argument> CreateAzureOutputArgumentAsync(ProcessingArgs data)
        {
            // Generate Azure blob path
            var blobPath = $"svf/{data.ProjectId}/{data.ParametersHash}/SvfOutput.zip";
            
            // Generate SAS URL with write permissions
            var sasUrl = await _azureBlobService.GenerateSasUrlAsync(
                blobPath, 
                Azure.Storage.Sas.BlobSasPermissions.Write | Azure.Storage.Sas.BlobSasPermissions.Create,
                expirationMinutes: 60
            );

            // Create onDemand output argument
            return new Autodesk.Forge.DesignAutomation.Model.Argument
            {
                Url = sasUrl,
                Verb = Autodesk.Forge.DesignAutomation.Model.Argument.VerbEnum.Put,
                LocalName = "SvfOutputAzure",
                Required = false,
                Headers = new Dictionary<string, string>
                {
                    { "x-ms-blob-type", "BlockBlob" }
                }
            };
        }

        protected override async Task<WorkItemStatus> CreateWorkItemAsync(ProcessingArgs projectData)
        {
            var workItem = await base.CreateWorkItemAsync(projectData);
            
            // Store Azure blob path in project data for later retrieval
            if (await _azureBlobService.IsConfiguredAsync())
            {
                var blobPath = $"svf/{projectData.ProjectId}/{projectData.ParametersHash}/SvfOutput.zip";
                projectData.SetAzureSvfBlobPath(blobPath);
            }
            
            return workItem;
        }

        protected override async Task OnSuccessAsync(ProcessingArgs data, WorkItemStatus workItemStatus)
        {
            await base.OnSuccessAsync(data, workItemStatus);
            
            // If Azure upload was successful, update the SVF URL to point to Azure
            if (!string.IsNullOrEmpty(data.GetAzureSvfBlobPath()))
            {
                var blobPath = data.GetAzureSvfBlobPath();
                var azureUrl = await _azureBlobService.GetModelUrlAsync(blobPath);
                data.SvfUrl = azureUrl;
                
                Logger.LogInformation($"SVF successfully uploaded to Azure: {blobPath}");
            }
        }
    }

    public static class ProcessingArgsExtensions
    {
        private const string AzureSvfBlobPathKey = "AzureSvfBlobPath";
        
        public static string GetAzureSvfBlobPath(this ProcessingArgs args)
        {
            return args.TryGetValue(AzureSvfBlobPathKey, out var value) ? value as string : null;
        }
        
        public static void SetAzureSvfBlobPath(this ProcessingArgs args, string value)
        {
            args[AzureSvfBlobPathKey] = value;
        }
    }
}