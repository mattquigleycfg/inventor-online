# Technology Stack

## Overview
APS Configurator for Inventor is a web application that provides Inventor-based model customization capabilities through a web interface, leveraging Autodesk Platform Services (APS).

## Backend Technologies

### Core Framework
- **.NET 8.0** - Modern cross-platform framework for building web applications
- **ASP.NET Core** - Web framework for building APIs and server-side logic
- **C# 10.0** - Primary programming language with implicit usings and nullable reference types

### Autodesk Integration
- **Autodesk.Forge** (v1.*) - Core Forge SDK for APS integration
- **Autodesk.Authentication** (v2.0.1) - Authentication services for APS
- **Autodesk.Oss** (v2.2.3) - Object Storage Service integration
- **Autodesk.ModelDerivative** (v2.2.0) - Model translation and visualization services
- **Autodesk.DataManagement** (v2.0.1) - Data management services
- **Autodesk.Forge.DesignAutomation** (v4.*) - Design Automation API for Inventor automation

### Cloud Storage
- **Azure.Storage.Blobs** (v12.25.0) - Azure Blob Storage integration for file management

### Logging & Monitoring
- **Serilog.AspNetCore** (v8.0.0) - Structured logging framework
- **Serilog.Sinks.File** (v5.0.0) - File-based log storage

### Other Backend Libraries
- **Polly** (v8.5.0) - Resilience and transient-fault-handling library
- **SignalR** - Real-time web functionality for job status updates

## Frontend Technologies

### Core Framework
- **React** (v17.0.2) - JavaScript library for building user interfaces
- **React Router** (v6.2.1) - Client-side routing
- **Redux** (v4.1.2) - State management
- **Redux Thunk** (v2.4.1) - Async middleware for Redux

### UI Components & Styling
- **HIG (Human Interface Guidelines)** - Autodesk's design system components:
  - @hig/avatar, @hig/banner, @hig/button, @hig/checkbox, @hig/dropdown
  - @hig/fonts, @hig/icon-button, @hig/icons, @hig/input, @hig/label
  - @hig/modal, @hig/progress-bar, @hig/project-account-switcher
  - @hig/spacer, @hig/surface, @hig/tabs, @hig/theme-context
  - @hig/theme-data, @hig/tooltip, @hig/top-nav, @hig/typography
- **Bootstrap** (v4.6.1) - CSS framework
- **Styled Components** (v5.3.3) - CSS-in-JS styling
- **Reactstrap** (v9.0.1) - Bootstrap components for React

### Development Tools
- **React Scripts** (v5.0.0) - Create React App tooling
- **TypeScript** (v4.5.5) - Type checking for JavaScript
- **ESLint** - JavaScript linting with multiple plugins
- **Stylelint** (v14.3.0) - CSS linting
- **Jest** - Testing framework
- **Enzyme** (v3.11.0) - React component testing
- **CodeceptJS** (v3.2.3) - End-to-end testing framework
- **Playwright** (v1.18.0) - Browser automation for testing

### Other Frontend Libraries
- **Axios** - HTTP client for API requests
- **OIDC Client** (v1.11.5) - OpenID Connect authentication
- **React Select** (v5.2.2) - Select input control
- **Unzipit** (v1.4.0) - Client-side ZIP file handling

## Build & Deployment

### Build Tools
- **npm** - Package manager
- **Webpack** (via React Scripts) - Module bundler
- **Babel** - JavaScript transpiler

### CI/CD
- **AWS CodeBuild** - Build automation
- **AWS Elastic Beanstalk** - Application deployment
- **Docker** - Containerization
- **AWS CloudWatch** - Monitoring and logging

### Cloud Services
- **AWS** - Primary cloud platform
- **Azure Storage** - Alternative storage solution

## Inventor Integration

### AppBundles (Design Automation Plugins)
- **CreateSVFPlugin** - Generate SVF files for visualization
- **CreateThumbnailPlugin** - Generate model thumbnails
- **DataCheckerPlugin** - Validate model data
- **ExportBOMPlugin** - Export Bill of Materials
- **ExportDrawingAsPdfPlugin** - Export drawings to PDF
- **ExtractParametersPlugin** - Extract model parameters
- **RFAExportRCEPlugin** - Export to RFA format
- **UpdateDrawingsPlugin** - Update drawing views
- **UpdateParametersPlugin** - Update model parameters

## Architecture Patterns
- **MVC Pattern** - Model-View-Controller architecture
- **Repository Pattern** - Data access abstraction
- **Dependency Injection** - IoC container for service management
- **Hub Pattern** - SignalR hubs for real-time communication
- **SPA (Single Page Application)** - React-based frontend