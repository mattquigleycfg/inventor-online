import React, { useState, useEffect } from 'react';
import axios from 'axios';
import './forgeView.css';

const AzureDebugPanel = () => {
    const [configStatus, setConfigStatus] = useState(null);
    const [files, setFiles] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [selectedFile, setSelectedFile] = useState(null);
    const [fileContent, setFileContent] = useState(null);

    useEffect(() => {
        checkAzureConfiguration();
    }, []);

    const checkAzureConfiguration = async () => {
        setLoading(true);
        try {
            // Check if Azure is configured
            const configResponse = await axios.get('/api/azure/status');
            setConfigStatus(configResponse.data);

            // List available models
            const modelsResponse = await axios.get('/api/azure/models');
            setFiles(modelsResponse.data);
            
            setError(null);
        } catch (err) {
            setError(`Configuration check failed: ${err.message}`);
        } finally {
            setLoading(false);
        }
    };

    const testProxyEndpoint = async (path) => {
        try {
            const response = await axios.get(`/api/azuresvfproxy/${path}`);
            setFileContent({
                path,
                status: 'success',
                contentType: response.headers['content-type'],
                size: response.headers['content-length'],
                data: response.data
            });
        } catch (err) {
            setFileContent({
                path,
                status: 'error',
                error: err.response?.data?.error || err.message
            });
        }
    };

    const testSvfStructure = async (modelName) => {
        const testPaths = [
            `${modelName}/bubble.json`,
            `${modelName}/output/bubble.json`,
            `${modelName}/manifest.json`,
            `${modelName}/output/0/bubble.json`
        ];

        console.log(`Testing SVF structure for ${modelName}...`);
        
        for (const path of testPaths) {
            try {
                const response = await axios.head(`/api/azuresvfproxy/${path}`);
                console.log(`✓ Found: ${path}`);
                return path; // Return the first valid path
            } catch (err) {
                console.log(`✗ Not found: ${path}`);
            }
        }
        
        return null;
    };

    const renderDebugInfo = () => {
        if (loading) return <div>Loading Azure configuration...</div>;
        
        return (
            <div>
                <h3>Azure Configuration Status</h3>
                <pre style={{ backgroundColor: '#f5f5f5', padding: '10px', borderRadius: '4px' }}>
                    {JSON.stringify(configStatus, null, 2)}
                </pre>

                <h3>Available Files</h3>
                {files.length > 0 ? (
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead>
                            <tr style={{ borderBottom: '2px solid #ddd' }}>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Name</th>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Size</th>
                                <th style={{ textAlign: 'left', padding: '8px' }}>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {files.map((file, index) => (
                                <tr key={index} style={{ borderBottom: '1px solid #eee' }}>
                                    <td style={{ padding: '8px' }}>{file.name}</td>
                                    <td style={{ padding: '8px' }}>{(file.size / 1024 / 1024).toFixed(2)} MB</td>
                                    <td style={{ padding: '8px' }}>
                                        <button 
                                            onClick={() => testProxyEndpoint(file.name)}
                                            style={{ marginRight: '10px' }}
                                        >
                                            Test Proxy
                                        </button>
                                        <button 
                                            onClick={async () => {
                                                const svfPath = await testSvfStructure(file.name.replace('.zip', ''));
                                                if (svfPath) {
                                                    alert(`Found SVF at: ${svfPath}`);
                                                } else {
                                                    alert('No SVF structure found');
                                                }
                                            }}
                                        >
                                            Test SVF
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                ) : (
                    <p>No files found in Azure storage</p>
                )}

                {fileContent && (
                    <div style={{ marginTop: '20px' }}>
                        <h3>File Test Result: {fileContent.path}</h3>
                        <pre style={{ backgroundColor: '#f5f5f5', padding: '10px', borderRadius: '4px' }}>
                            {JSON.stringify(fileContent, null, 2)}
                        </pre>
                    </div>
                )}

                <h3>Quick Tests</h3>
                <div style={{ marginTop: '10px' }}>
                    <button 
                        onClick={() => testProxyEndpoint('MRConfigurator/output/bubble.json')}
                        style={{ marginRight: '10px' }}
                    >
                        Test MRConfigurator bubble.json
                    </button>
                    <button 
                        onClick={() => testProxyEndpoint('test/nonexistent.json')}
                        style={{ marginRight: '10px' }}
                    >
                        Test 404 Error
                    </button>
                    <button 
                        onClick={checkAzureConfiguration}
                    >
                        Refresh
                    </button>
                </div>
            </div>
        );
    };

    return (
        <div style={{ padding: '20px' }}>
            <h2>Azure Storage Debug Panel</h2>
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
            {renderDebugInfo()}
        </div>
    );
};

export default AzureDebugPanel;