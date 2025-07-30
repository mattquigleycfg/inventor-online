import React, { useState, useEffect } from 'react';
import axios from 'axios';
import ModelDerivativeViewer from './modelDerivativeViewer';
import './forgeView.css';

const AzureModelDerivativeManager = () => {
    const [azureFiles, setAzureFiles] = useState([]);
    const [processedModels, setProcessedModels] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [selectedUrn, setSelectedUrn] = useState(null);
    const [processingStatus, setProcessingStatus] = useState({});

    useEffect(() => {
        loadAzureFiles();
        loadProcessedModels();
    }, []);

    const loadAzureFiles = async () => {
        try {
            const response = await axios.get('/api/azure/models');
            setAzureFiles(response.data);
        } catch (err) {
            console.error('Error loading Azure files:', err);
            setError('Failed to load files from Azure storage');
        }
    };

    const loadProcessedModels = async () => {
        try {
            const response = await axios.get('/api/azure/model-derivative/processed');
            setProcessedModels(response.data);
        } catch (err) {
            console.error('Error loading processed models:', err);
        }
    };

    const processFile = async (blobName) => {
        setProcessingStatus(prev => ({ ...prev, [blobName]: 'starting' }));
        setError(null);

        try {
            const response = await axios.post('/api/azure/model-derivative/process', {
                blobName: blobName
            });

            if (response.data.success) {
                const urn = response.data.urn;
                setProcessingStatus(prev => ({ ...prev, [blobName]: 'processing' }));
                
                // Start polling for status
                pollTranslationStatus(urn, blobName);
            } else {
                setProcessingStatus(prev => ({ ...prev, [blobName]: 'failed' }));
                setError(response.data.error || 'Processing failed');
            }
        } catch (err) {
            setProcessingStatus(prev => ({ ...prev, [blobName]: 'failed' }));
            setError(err.response?.data?.error || 'Failed to process file');
        }
    };

    const pollTranslationStatus = async (urn, blobName) => {
        const maxAttempts = 60; // 5 minutes with 5-second intervals
        let attempts = 0;

        const checkStatus = async () => {
            try {
                const response = await axios.get(`/api/azure/model-derivative/status/${urn}`);
                const { status, complete, failed } = response.data;

                if (complete) {
                    setProcessingStatus(prev => ({ ...prev, [blobName]: 'complete' }));
                    await loadProcessedModels(); // Refresh the list
                    return;
                }

                if (failed) {
                    setProcessingStatus(prev => ({ ...prev, [blobName]: 'failed' }));
                    setError('Translation failed');
                    return;
                }

                attempts++;
                if (attempts < maxAttempts) {
                    setTimeout(checkStatus, 5000); // Check again in 5 seconds
                } else {
                    setProcessingStatus(prev => ({ ...prev, [blobName]: 'timeout' }));
                    setError('Translation timeout');
                }
            } catch (err) {
                console.error('Error checking status:', err);
                setProcessingStatus(prev => ({ ...prev, [blobName]: 'error' }));
            }
        };

        checkStatus();
    };

    const processAllFiles = async () => {
        setLoading(true);
        setError(null);

        try {
            const response = await axios.post('/api/azure/model-derivative/process-all');
            const { totalFiles, processed, results } = response.data;

            // Start polling for all processing files
            results.forEach(result => {
                if (result.success && result.urn) {
                    pollTranslationStatus(result.urn, result.blobName);
                }
            });

            await loadProcessedModels();
        } catch (err) {
            setError('Failed to process all files');
        } finally {
            setLoading(false);
        }
    };

    const viewModel = (urn) => {
        setSelectedUrn(urn);
    };

    const getFileStatus = (blobName) => {
        // Check processing status
        if (processingStatus[blobName]) {
            return processingStatus[blobName];
        }

        // Check if already processed
        const processed = processedModels.find(m => m.blobName === blobName);
        if (processed) {
            return processed.status;
        }

        return 'not_processed';
    };

    const renderFileRow = (file) => {
        const status = getFileStatus(file.name);
        const isZip = file.name.toLowerCase().endsWith('.zip');
        const processed = processedModels.find(m => m.blobName === file.name);

        return (
            <tr key={file.name}>
                <td style={{ padding: '8px' }}>{file.name}</td>
                <td style={{ padding: '8px' }}>{(file.size / 1024 / 1024).toFixed(2)} MB</td>
                <td style={{ padding: '8px' }}>
                    <span style={{ 
                        padding: '4px 8px', 
                        borderRadius: '4px',
                        backgroundColor: getStatusColor(status),
                        color: 'white',
                        fontSize: '12px'
                    }}>
                        {getStatusLabel(status)}
                    </span>
                </td>
                <td style={{ padding: '8px' }}>
                    {isZip && (
                        <>
                            {status === 'not_processed' && (
                                <button
                                    onClick={() => processFile(file.name)}
                                    style={{ marginRight: '10px' }}
                                    disabled={status === 'processing' || status === 'starting'}
                                >
                                    Process
                                </button>
                            )}
                            {processed && processed.viewerReady && (
                                <button
                                    onClick={() => viewModel(processed.urn)}
                                    style={{ 
                                        backgroundColor: '#28a745',
                                        color: 'white',
                                        border: 'none',
                                        padding: '4px 12px',
                                        borderRadius: '4px',
                                        cursor: 'pointer'
                                    }}
                                >
                                    View 3D
                                </button>
                            )}
                        </>
                    )}
                </td>
            </tr>
        );
    };

    const getStatusColor = (status) => {
        switch (status) {
            case 'complete': return '#28a745';
            case 'processing':
            case 'starting': return '#ffc107';
            case 'failed':
            case 'error':
            case 'timeout': return '#dc3545';
            default: return '#6c757d';
        }
    };

    const getStatusLabel = (status) => {
        switch (status) {
            case 'complete': return 'Ready';
            case 'processing': return 'Processing...';
            case 'starting': return 'Starting...';
            case 'failed': return 'Failed';
            case 'error': return 'Error';
            case 'timeout': return 'Timeout';
            case 'not_processed': return 'Not Processed';
            default: return status;
        }
    };

    return (
        <div style={{ padding: '20px' }}>
            <h2>Azure Model Derivative Manager</h2>
            <p>Process files from Azure Blob Storage through Autodesk Model Derivative API</p>

            {error && (
                <div style={{
                    backgroundColor: '#f8d7da',
                    color: '#721c24',
                    padding: '10px',
                    borderRadius: '4px',
                    marginBottom: '20px'
                }}>
                    {error}
                </div>
            )}

            <div style={{ marginBottom: '20px' }}>
                <button
                    onClick={loadAzureFiles}
                    style={{ marginRight: '10px' }}
                >
                    Refresh Files
                </button>
                <button
                    onClick={processAllFiles}
                    disabled={loading}
                    style={{
                        backgroundColor: '#17a2b8',
                        color: 'white',
                        border: 'none',
                        padding: '8px 16px',
                        borderRadius: '4px',
                        cursor: loading ? 'not-allowed' : 'pointer'
                    }}
                >
                    {loading ? 'Processing...' : 'Process All ZIP Files'}
                </button>
            </div>

            <h3>Files in Azure Storage</h3>
            {azureFiles.length > 0 ? (
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <thead>
                        <tr style={{ borderBottom: '2px solid #ddd' }}>
                            <th style={{ textAlign: 'left', padding: '8px' }}>File Name</th>
                            <th style={{ textAlign: 'left', padding: '8px' }}>Size</th>
                            <th style={{ textAlign: 'left', padding: '8px' }}>Status</th>
                            <th style={{ textAlign: 'left', padding: '8px' }}>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {azureFiles.map(file => renderFileRow(file))}
                    </tbody>
                </table>
            ) : (
                <p>No files found in Azure storage</p>
            )}

            {selectedUrn && (
                <div style={{ marginTop: '40px' }}>
                    <h3>3D Viewer</h3>
                    <button
                        onClick={() => setSelectedUrn(null)}
                        style={{ marginBottom: '10px' }}
                    >
                        Close Viewer
                    </button>
                    <div style={{ height: '600px', border: '1px solid #ddd' }}>
                        <ModelDerivativeViewer urn={selectedUrn} />
                    </div>
                </div>
            )}

            <div style={{ marginTop: '40px' }}>
                <h3>How it Works</h3>
                <ol>
                    <li>Files are downloaded from Azure Blob Storage</li>
                    <li>Uploaded to Autodesk Forge Object Storage Service (OSS)</li>
                    <li>Processed through Model Derivative API to generate viewable formats</li>
                    <li>Can be viewed in the Autodesk Viewer using the generated URN</li>
                </ol>
                <p><strong>Note:</strong> Only ZIP files containing Inventor models can be processed.</p>
            </div>
        </div>
    );
};

export default AzureModelDerivativeManager;