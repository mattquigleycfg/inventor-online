# Changelog

All notable changes to the APS Configurator for Inventor project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Tech stack documentation (TECH_STACK.md)
- Project changelog (CHANGELOG.md)
- Simplified Azure SVF viewer component (azureSimpleViewer.js)
- Azure viewer demo page (azureViewerDemo.js)
- Comprehensive Azure SVF integration guide (AZURE_SVF_GUIDE.md)

### Changed
- Refactored AzureSvfProxyController to handle both direct URLs and relative paths
- Improved proxy endpoint to support full Azure URL passthrough
- Enhanced error handling and logging in Azure model loading

### Fixed
- Azure Blob Storage SVF file loading issues
- CORS problems when loading models from Azure
- Path resolution for nested SVF resources

## [0.1.1] - 2025-01-29

### Added
- Retry logic for bucket object retrieval in ProjectService with Polly policy for handling eventual consistency
- Azure integration endpoints for SAS URL generation and file upload
- Azure Blob Storage service methods for file management
- Azure.Storage.Blobs package reference (v12.25.0)
- TryInitializeAsync helper method for AppBundle initialization
- Azure-first flow prioritizing environment variables for SAS tokens

### Changed
- Refactored AzureBlobService to update model information structure with JSON output
- Updated ForgeOSS service methods to ensure access token assignment before API calls
- Modified App and ProjectSwitcher components to avoid automatic data fetching in Azure-only mode
- Updated trace log files and .gitignore for improved file management
- Upgraded Autodesk.Forge.DesignAutomation package to version 4.*

### Fixed
- AppBundle initialization to gracefully handle missing ZIP files
- Missing code:all scope for Design Automation v3 authentication
- Removed redundant null checks for DefaultHeaders in ForgeOSS service methods

### Removed
- ProjectSwitcher component (simplified toolbar rendering)
- Model existence check in AzureBlobService (allows viewer to handle 404 errors)

## [0.1.0] - Earlier

### Initial Features
- Web-based Inventor model configuration
- Parameter editing and real-time updates
- BOM (Bill of Materials) export
- Drawing generation and PDF export
- Model visualization using Autodesk Viewer
- Multi-project support
- User authentication and authorization
- Design Automation integration for Inventor operations
- Real-time job status updates via SignalR