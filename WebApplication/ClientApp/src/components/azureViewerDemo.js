import React, { useState } from 'react';
import AzureSimpleViewer from './azureSimpleViewer';
import './forgeView.css';

/**
 * Demo component showing how to load models from Azure Blob Storage
 */
const AzureViewerDemo = () => {
    const [modelUrl, setModelUrl] = useState('');
    const [inputUrl, setInputUrl] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    // Example Azure URLs for different model structures
    const exampleModels = [
        {
            name: 'MR Configurator',
            bubbleUrl: 'MRConfigurator/output/bubble.json'
        },
        {
            name: 'Custom Model (SVF in root)',
            bubbleUrl: 'models/mymodel/bubble.json'
        },
        {
            name: 'Project Model',
            bubbleUrl: 'projects/project123/svf/bubble.json'
        }
    ];

    const loadModel = (bubbleUrl) => {
        setError(null);
        setLoading(true);
        setModelUrl(bubbleUrl);
    };

    const handleModelLoaded = () => {
        setLoading(false);
        console.log('Model loaded successfully');
    };

    const handleError = (error) => {
        setLoading(false);
        setError(error.message || 'Failed to load model');
        console.error('Model loading error:', error);
    };

    const handleCustomUrlSubmit = (e) => {
        e.preventDefault();
        if (inputUrl) {
            loadModel(inputUrl);
        }
    };

    return (
        <div style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
            {/* Control Panel */}
            <div style={{ 
                padding: '20px', 
                backgroundColor: '#f5f5f5', 
                borderBottom: '1px solid #ddd'
            }}>
                <h2>Azure Blob Storage Model Viewer</h2>
                
                <div style={{ marginBottom: '20px' }}>
                    <h3>Quick Load Examples:</h3>
                    <div style={{ display: 'flex', gap: '10px', marginBottom: '10px' }}>
                        {exampleModels.map((model, index) => (
                            <button
                                key={index}
                                onClick={() => loadModel(model.bubbleUrl)}
                                style={{
                                    padding: '8px 16px',
                                    backgroundColor: '#0696d7',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer'
                                }}
                            >
                                {model.name}
                            </button>
                        ))}
                    </div>
                </div>

                <div style={{ marginBottom: '20px' }}>
                    <h3>Custom Azure Path:</h3>
                    <form onSubmit={handleCustomUrlSubmit} style={{ display: 'flex', gap: '10px' }}>
                        <input
                            type="text"
                            value={inputUrl}
                            onChange={(e) => setInputUrl(e.target.value)}
                            placeholder="Enter path to bubble.json (e.g., models/mymodel/bubble.json)"
                            style={{
                                flex: 1,
                                padding: '8px',
                                border: '1px solid #ddd',
                                borderRadius: '4px'
                            }}
                        />
                        <button
                            type="submit"
                            style={{
                                padding: '8px 16px',
                                backgroundColor: '#28a745',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: 'pointer'
                            }}
                        >
                            Load Model
                        </button>
                    </form>
                    <small style={{ color: '#666' }}>
                        Note: Path should be relative to your Azure container root
                    </small>
                </div>

                {loading && (
                    <div style={{ color: '#0696d7', marginTop: '10px' }}>
                        Loading model...
                    </div>
                )}

                {error && (
                    <div style={{ 
                        color: '#dc3545', 
                        marginTop: '10px',
                        padding: '10px',
                        backgroundColor: '#f8d7da',
                        border: '1px solid #f5c6cb',
                        borderRadius: '4px'
                    }}>
                        Error: {error}
                    </div>
                )}

                {modelUrl && !loading && !error && (
                    <div style={{ color: '#28a745', marginTop: '10px' }}>
                        ✓ Model loaded: {modelUrl}
                    </div>
                )}
            </div>

            {/* Viewer Container */}
            <div style={{ flex: 1, position: 'relative' }}>
                {modelUrl && (
                    <AzureSimpleViewer
                        azureBubbleUrl={modelUrl}
                        onModelLoaded={handleModelLoaded}
                        onError={handleError}
                    />
                )}
                
                {!modelUrl && (
                    <div style={{
                        position: 'absolute',
                        top: '50%',
                        left: '50%',
                        transform: 'translate(-50%, -50%)',
                        textAlign: 'center',
                        color: '#666'
                    }}>
                        <h3>No Model Loaded</h3>
                        <p>Select a model from the examples above or enter a custom path</p>
                    </div>
                )}
            </div>
        </div>
    );
};

export default AzureViewerDemo;