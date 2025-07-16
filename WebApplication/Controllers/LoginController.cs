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
using System.Threading.Tasks;
using System.Web;
using Autodesk.Forge.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication.Definitions;
using WebApplication.Services;
using WebApplication.State;
using WebApplication.Utilities;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("login")]
    public class LoginController : ControllerBase
    {
        private static readonly ProfileDTO AnonymousProfile = new ProfileDTO { Name = "Anonymous", AvatarUrl = "logo-xs-white-BG.svg" };

        private readonly ILogger<LoginController> _logger;
        private readonly ProfileProvider _profileProvider;
        private readonly InviteOnlyModeConfiguration _inviteOnlyModeConfig;
        private readonly CallbackUrlsConfiguration _callbackUrlsConfig;

        /// <summary>
        /// Forge configuration.
        /// </summary>
        public ForgeConfiguration Configuration { get; }

        public LoginController(ILogger<LoginController> logger, IOptions<ForgeConfiguration> optionsAccessor, ProfileProvider profileProvider, IOptions<InviteOnlyModeConfiguration> inviteOnlyModeOptionsAccessor, IOptions<CallbackUrlsConfiguration> callbackUrlsOptionsAccessor)
        {
            _logger = logger;
            _profileProvider = profileProvider;
            Configuration = optionsAccessor.Value.Validate();
            _inviteOnlyModeConfig = inviteOnlyModeOptionsAccessor.Value;
            _callbackUrlsConfig = callbackUrlsOptionsAccessor.Value;
        }

        [HttpGet]
        public RedirectResult Get()
        {
            _logger.LogInformation("Authorize against the Oxygen");

            // Determine the appropriate callback URL based on environment
            var callbackUrl = GetCallbackUrl();
            var encodedHost = HttpUtility.UrlEncode(callbackUrl);

            // prepare scope
            var scopes = new[] { "user-profile:read" };
            var fullScope = string.Join("%20", scopes); // it's not necessary now, but kept in case we need it in future

            // build auth url (https://aps.autodesk.com/en/docs/oauth/v2/reference/http/authorize-GET)
            string baseUrl = Configuration.AuthenticationAddress.GetLeftPart(System.UriPartial.Authority);
            var authUrl = $"{baseUrl}/authentication/v2/authorize?response_type=token&client_id={Configuration.ClientId}&redirect_uri={encodedHost}&scope={fullScope}";
            return Redirect(authUrl);
        }

        private string GetCallbackUrl()
        {
            var currentHost = HttpContext.Request.Host.Host;
            var currentScheme = HttpContext.Request.Scheme;
            
            // Check if we're in local development
            if (currentHost == "localhost" || currentHost == "127.0.0.1")
            {
                return _callbackUrlsConfig.Development;
            }
            
            // Check if we're on Azure App Service
            if (currentHost.Contains("azurewebsites.net") || 
                currentHost == "3d.con-formgroup.com.au")
            {
                return _callbackUrlsConfig.Production;
            }
            
            // Check legacy hosts
            if (currentHost == "inventor-config-demo.autodesk.io" || 
                currentHost == "inventor-config-demo-dev.autodesk.io")
            {
                return $"https://{currentHost}/";
            }
            
            // Default to production or fallback to current request URL
            return _callbackUrlsConfig.Production ?? 
                   $"{(currentScheme == "http" ? "https" : currentScheme)}{Uri.SchemeDelimiter}{HttpContext.Request.Host}";
        }

        [HttpGet("profile")]
        public async Task<ActionResult<ProfileDTO>> Profile()
        {
            _logger.LogInformation("Get profile");
            if (_profileProvider.IsAuthenticated)
            {
                dynamic profile = await _profileProvider.GetProfileAsync();
                if (_inviteOnlyModeConfig.Enabled)
                {
                    var inviteOnlyChecker = new InviteOnlyChecker(_inviteOnlyModeConfig);
                    if (!profile.emailVerified || !inviteOnlyChecker.IsInvited(profile.emailId))
                    {
                        return StatusCode(403);
                    }
                }
                return new ProfileDTO { Name = profile.firstName + " " + profile.lastName, AvatarUrl = profile.profileImages.sizeX40 };
            }
            else
            {
                return AnonymousProfile;
            }
        }
    }
}
