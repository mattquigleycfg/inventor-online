﻿/////////////////////////////////////////////////////////////////////
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
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WebApplication.Definitions;

namespace WebApplication.Processing
{
    public class FdaClient
    {
        private readonly TransferData _transferData;
        private readonly CreateRFA _rfaWork;
        private readonly ExportDrawing _exportDrawingWork;
        private readonly UpdateDrawings _updateDrawingsWork;
        private readonly AdoptProject _adoptWork;
        private readonly UpdateProject _updateProjectWork;
        private readonly AppBundleZipPaths _paths;
        private readonly Publisher _publisher;

        public FdaClient(Publisher publisher, IOptions<AppBundleZipPaths> appBundleZipPathsOptionsAccessor)
        {
            _transferData = new TransferData(publisher);
            _rfaWork = new CreateRFA(publisher);
            _exportDrawingWork = new ExportDrawing(publisher);
            _updateDrawingsWork = new UpdateDrawings(publisher);
            _adoptWork = new AdoptProject(publisher);
            _updateProjectWork = new UpdateProject(publisher);
            _paths = appBundleZipPathsOptionsAccessor.Value;
            _publisher = publisher;
        }

        public async Task InitializeAsync()
        {
            // create bundles and activities (gracefully handle missing ZIP files)
            await TryInitializeAsync(() => new DataChecker(_publisher).InitializeAsync(_paths.DataChecker), nameof(DataChecker));
            await TryInitializeAsync(() => new CreateSVF(_publisher).InitializeAsync(_paths.CreateSVF), nameof(CreateSVF));
            await TryInitializeAsync(() => new CreateThumbnail(_publisher).InitializeAsync(_paths.CreateThumbnail), nameof(CreateThumbnail));
            await TryInitializeAsync(() => new ExtractParameters(_publisher).InitializeAsync(_paths.ExtractParameters), nameof(ExtractParameters));
            await TryInitializeAsync(() => new UpdateParameters(_publisher).InitializeAsync(_paths.UpdateParameters), nameof(UpdateParameters));
            await TryInitializeAsync(() => new CreateBOM(_publisher).InitializeAsync(_paths.CreateBOM), nameof(CreateBOM));
            await TryInitializeAsync(() => new ExportDrawing(_publisher).InitializeAsync(_paths.ExportDrawing), nameof(ExportDrawing));

            await TryInitializeAsync(() => _transferData.InitializeAsync(_paths.EmptyExe), nameof(TransferData));
            await TryInitializeAsync(() => _rfaWork.InitializeAsync(_paths.CreateRFA), nameof(CreateRFA));
            await TryInitializeAsync(() => _exportDrawingWork.InitializeAsync(_paths.ExportDrawing), nameof(ExportDrawing));
            await TryInitializeAsync(() => _updateDrawingsWork.InitializeAsync(_paths.UpdateDrawings), nameof(UpdateDrawings));

            await TryInitializeAsync(() => _adoptWork.InitializeAsync(null /* does not matter */), nameof(AdoptProject));
            await TryInitializeAsync(() => _updateProjectWork.InitializeAsync(null /* does not matter */), nameof(UpdateProject));
        }

        private async Task TryInitializeAsync(Func<Task> initializeFunc, string componentName)
        {
            try
            {
                await initializeFunc();
                System.Diagnostics.Debug.WriteLine($"✅ {componentName} initialized successfully");
            }
            catch (System.Exception ex) when (ex.Message.Contains("App Bundle package is not found"))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ {componentName} AppBundle ZIP file not found - skipping initialization (this is normal for development)");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ {componentName} initialization failed: {ex.Message}");
                // Don't rethrow - allow application to continue without this component
            }
        }

        public async Task CleanUpAsync()
        {
            // delete bundles and activities
            await new DataChecker(_publisher).CleanUpAsync();
            await new CreateSVF(_publisher).CleanUpAsync();
            await new CreateThumbnail(_publisher).CleanUpAsync();
            await new ExtractParameters(_publisher).CleanUpAsync();
            await new UpdateParameters(_publisher).CleanUpAsync();
            await new CreateBOM(_publisher).CleanUpAsync();
            await new ExportDrawing(_publisher).CleanUpAsync();

            await _transferData.CleanUpAsync();
            await _rfaWork.CleanUpAsync();
            await _exportDrawingWork.CleanUpAsync();
            await _updateDrawingsWork.CleanUpAsync();

            await _adoptWork.CleanUpAsync();
            await _updateProjectWork.CleanUpAsync();
        }

        public Task<ProcessingResult> AdoptAsync(AdoptionData projectData)
        {
            return _adoptWork.ProcessAsync(projectData);
        }

        public Task<ProcessingResult> UpdateAsync(UpdateData projectData)
        {
            return _updateProjectWork.ProcessAsync(projectData);
        }

        internal Task<ProcessingResult> TransferAsync(string source, string target)
        {
            return _transferData.ProcessAsync(source, target);
        }

        internal async Task<ProcessingResult> GenerateRfa(ProcessingArgs rfaData)
        {
            ProcessingResult rfaResult = await _rfaWork.ProcessAsync(rfaData);
            if (!rfaResult.Success)
            {
                rfaResult.ErrorMessage = "Failed to generate RFA file";
            }

            return rfaResult;
        }

        internal async Task<ProcessingResult> GenerateDrawing(ProcessingArgs data)
        {
            ProcessingResult result = await _updateDrawingsWork.ProcessAsync(data);
            if (!result.Success)
            {
                result.ErrorMessage = "Failed to update drawing file(s)";
            }

            return result;
        }

        internal async Task<ProcessingResult> ExportDrawingAsync(ProcessingArgs drawingData)
        {
            return await _exportDrawingWork.ProcessAsync(drawingData);
        }
    }
}
