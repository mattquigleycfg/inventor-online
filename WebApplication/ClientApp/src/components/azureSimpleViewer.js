import React, { Component } from 'react';
import Script from 'react-load-script';
import { viewerCss, viewerJs } from './shared';
import './forgeView.css';

let Autodesk = null;

/**
 * Simplified Azure Viewer for loading SVF files from Azure Blob Storage
 * This component handles direct loading of SVF files using bubble.json
 */
export class AzureSimpleViewer extends Component {
    constructor(props) {
        super(props);
        
        this.viewerDiv = React.createRef();
        this.viewer = null;
        this.state = {
            viewerInitialized: false,
            error: null
        };
    }

    handleScriptLoad() {
        console.log('Viewer script loaded, initializing...');
        Autodesk = window.Autodesk;

        const options = {
            env: 'AutodeskProduction', // Use production environment for viewer assets
            api: 'derivativeV2',
            getAccessToken: (callback) => {
                // For Azure-hosted files, we don't need Autodesk authentication
                // The files are accessed via Azure SAS URLs
                callback('', 86400);
            }
        };

        Autodesk.Viewing.Initializer(options, () => {
            this.initializeViewer();
        });
    }

    initializeViewer() {
        const container = this.viewerDiv.current;
        this.viewer = new Autodesk.Viewing.GuiViewer3D(container);
        
        const startResult = this.viewer.start();
        if (startResult > 0) {
            console.error('Failed to create viewer:', startResult);
            this.setState({ error: 'Failed to initialize viewer' });
            return;
        }

        // Configure viewer
        this.viewer.setTheme('light-theme');
        this.viewer.setQualityLevel(false, true);
        
        this.setState({ viewerInitialized: true });

        // Load model if URL is provided
        if (this.props.azureBubbleUrl) {
            this.loadModelFromAzure(this.props.azureBubbleUrl);
        }
    }

    loadModelFromAzure(bubbleUrl) {
        if (!this.viewer || !this.state.viewerInitialized) {
            console.error('Viewer not initialized');
            return;
        }

        console.log('Loading model from Azure:', bubbleUrl);

        // For Azure-hosted SVF files, we need to use the proxy endpoint
        // to handle CORS and authentication
        const proxyUrl = `/api/azuresvfproxy/${bubbleUrl}`;

        // Load the bubble.json manifest
        Autodesk.Viewing.Document.load(
            proxyUrl,
            (doc) => this.onDocumentLoadSuccess(doc),
            (errorCode, errorMsg, errors) => this.onDocumentLoadFailure(errorCode, errorMsg, errors)
        );
    }

    onDocumentLoadSuccess(doc) {
        console.log('Document loaded successfully');
        
        // Get the default viewable (usually the 3D view)
        const viewables = doc.getRoot().getDefaultGeometry();
        
        if (!viewables) {
            console.error('No viewables found in document');
            this.setState({ error: 'No viewable content found' });
            return;
        }

        // Load the viewable
        this.viewer.loadDocumentNode(doc, viewables).then(() => {
            console.log('Model loaded successfully');
            
            // Fit model to view
            this.viewer.fitToView();
            
            // Call success callback
            if (this.props.onModelLoaded) {
                this.props.onModelLoaded();
            }
        }).catch((error) => {
            console.error('Error loading viewable:', error);
            this.setState({ error: `Failed to load model: ${error.message}` });
            
            if (this.props.onError) {
                this.props.onError(error);
            }
        });
    }

    onDocumentLoadFailure(errorCode, errorMsg, errors) {
        console.error('Failed to load document:', errorCode, errorMsg, errors);
        
        let message = `Failed to load document: ${errorMsg || errorCode}`;
        
        // Check for specific error codes
        if (errorCode === 'Autodesk.Viewing.Document.Error.NETWORK_FILE_NOT_FOUND') {
            message = 'Model file not found in Azure storage';
        } else if (errorCode === 'Autodesk.Viewing.Document.Error.NETWORK_FAILURE') {
            message = 'Network error while loading model';
        }
        
        this.setState({ error: message });
        
        if (this.props.onError) {
            this.props.onError({ code: errorCode, message });
        }
    }

    componentDidUpdate(prevProps) {
        // Reload model if URL changes
        if (this.state.viewerInitialized && 
            this.props.azureBubbleUrl !== prevProps.azureBubbleUrl) {
            
            // Unload current model
            if (this.viewer) {
                this.viewer.unloadModel();
            }
            
            // Load new model
            if (this.props.azureBubbleUrl) {
                this.loadModelFromAzure(this.props.azureBubbleUrl);
            }
        }
    }

    componentWillUnmount() {
        if (this.viewer) {
            this.viewer.finish();
            this.viewer = null;
        }
    }

    render() {
        const { error } = this.state;
        
        return (
            <div className="modelContainer fullheight">
                <div className="viewer" id="AzureSimpleViewer">
                    {error && (
                        <div style={{
                            position: 'absolute',
                            top: '50%',
                            left: '50%',
                            transform: 'translate(-50%, -50%)',
                            backgroundColor: '#f8d7da',
                            color: '#721c24',
                            padding: '20px',
                            borderRadius: '4px',
                            border: '1px solid #f5c6cb',
                            zIndex: 10
                        }}>
                            {error}
                        </div>
                    )}
                    <div ref={this.viewerDiv} style={{ height: '100%' }}></div>
                    <link rel="stylesheet" type="text/css" href={viewerCss} />
                    <Script url={viewerJs} onLoad={this.handleScriptLoad.bind(this)} />
                </div>
            </div>
        );
    }
}

export default AzureSimpleViewer;