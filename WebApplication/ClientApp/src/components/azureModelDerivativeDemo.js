import React from 'react';
import AzureModelDerivativeManager from './azureModelDerivativeManager';

const AzureModelDerivativeDemo = () => {
    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
            <div style={{ 
                backgroundColor: '#0696d7', 
                color: 'white', 
                padding: '20px',
                marginBottom: '20px'
            }}>
                <h1 style={{ margin: 0 }}>Azure + Model Derivative Integration</h1>
                <p style={{ margin: '10px 0 0 0' }}>
                    Process Inventor files from Azure Blob Storage using Autodesk Model Derivative API
                </p>
            </div>

            <div style={{ maxWidth: '1400px', margin: '0 auto', padding: '0 20px' }}>
                <AzureModelDerivativeManager />

                <div style={{ 
                    marginTop: '40px', 
                    padding: '20px',
                    backgroundColor: 'white',
                    borderRadius: '8px',
                    boxShadow: '0 2px 4px rgba(0,0,0,0.1)'
                }}>
                    <h2>Integration Overview</h2>
                    
                    <div style={{ marginBottom: '20px' }}>
                        <h3>Workflow Steps:</h3>
                        <ol>
                            <li><strong>Azure Storage:</strong> Files are stored in Azure Blob Storage</li>
                            <li><strong>Download:</strong> Files are downloaded from Azure to the server</li>
                            <li><strong>Upload to OSS:</strong> Files are uploaded to Autodesk Object Storage Service</li>
                            <li><strong>Translation:</strong> Model Derivative API processes the files to create viewable formats</li>
                            <li><strong>View:</strong> Processed models can be viewed in the Autodesk Viewer</li>
                        </ol>
                    </div>

                    <div style={{ marginBottom: '20px' }}>
                        <h3>Supported File Types:</h3>
                        <ul>
                            <li>ZIP files containing Inventor assemblies (.iam)</li>
                            <li>ZIP files containing Inventor parts (.ipt)</li>
                            <li>Other CAD formats supported by Model Derivative API</li>
                        </ul>
                    </div>

                    <div style={{ marginBottom: '20px' }}>
                        <h3>Key Features:</h3>
                        <ul>
                            <li>Automatic file detection from Azure Blob Storage</li>
                            <li>Batch processing of multiple files</li>
                            <li>Real-time translation status monitoring</li>
                            <li>Integrated 3D viewer for processed models</li>
                            <li>Persistent URN storage for quick access</li>
                        </ul>
                    </div>

                    <div style={{ 
                        backgroundColor: '#e7f3ff',
                        padding: '15px',
                        borderRadius: '4px',
                        border: '1px solid #bee5eb'
                    }}>
                        <h4 style={{ marginTop: 0 }}>Note:</h4>
                        <p style={{ marginBottom: 0 }}>
                            Processing time varies based on file size and complexity. 
                            Typical processing takes 30 seconds to 2 minutes for most Inventor models.
                        </p>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AzureModelDerivativeDemo;