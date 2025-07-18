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
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Forge.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication.Definitions;
using WebApplication.Processing;

namespace WebApplication.Services
{
    /// <summary>
    /// Background service for async initialization that doesn't block web server startup
    /// </summary>
    public class InitializationService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InitializationService> _logger;

        public InitializationService(
            IServiceScopeFactory serviceScopeFactory,
            IConfiguration configuration,
            ILogger<InitializationService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Background initialization service started");

                // Wait a short delay to ensure web server is up
                await Task.Delay(2000, stoppingToken);

                using var scope = _serviceScopeFactory.CreateScope();
                var services = scope.ServiceProvider;

                // Get required services
                var initializer = services.GetRequiredService<Initializer>();
                var publisher = services.GetRequiredService<Publisher>();
                var forgeConfiguration = services.GetRequiredService<IOptions<ForgeConfiguration>>();

                // Check if clear operation is requested
                if (_configuration.GetValue<bool>("clear"))
                {
                    _logger.LogInformation("-- Background Clean up --");
                    
                    string clientIdCanDeleteUserBuckets = _configuration.GetValue<string>("clientIdCanDeleteUserBuckets");
                    string clientId = forgeConfiguration.Value.ClientId;
                    bool deleteUserBuckets = (clientIdCanDeleteUserBuckets == clientId);
                    
                    await initializer.ClearAsync(deleteUserBuckets);
                    _logger.LogInformation("Background cleanup completed");
                }

                // Check if initialization is requested
                if (_configuration.GetValue<bool>("initialize"))
                {
                    _logger.LogInformation("-- Background Initialization --");
                    
                    // Force polling for initialization (callbacks won't work during startup)
                    var oldCheckType = publisher.CompletionCheck;
                    publisher.CompletionCheck = CompletionCheck.Polling;

                    try
                    {
                        await initializer.InitializeAsync();
                        _logger.LogInformation("Background initialization completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background initialization failed");
                        // Don't crash the application, just log the error
                    }
                    finally
                    {
                        // Reset completion check method
                        publisher.CompletionCheck = oldCheckType;
                    }
                }

                // Check if bundle initialization is requested
                if (_configuration.GetValue<bool>("bundles"))
                {
                    _logger.LogInformation("-- Background AppBundles and Activities Initialization --");
                    
                    try
                    {
                        await initializer.InitializeBundlesAsync();
                        _logger.LogInformation("Background bundle initialization completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background bundle initialization failed");
                        // Don't crash the application, just log the error
                    }
                }

                _logger.LogInformation("Background initialization service completed all tasks");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background initialization service was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in background initialization service");
            }
        }
    }
} 