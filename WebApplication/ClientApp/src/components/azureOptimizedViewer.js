import React, { Component } from 'react';
import PropTypes from 'prop-types';
import Script from 'react-load-script';
import { viewerCss, viewerJs } from './shared';
import repo from '../Repository';
import './forgeView.css';

let Autodesk = null;

/**
 * Optimized viewer for Azure-hosted SVF files.
 * Uses the proxy endpoint for better performance and authentication.
 */
export class AzureOptimizedViewer extends Component {
    constructor(props) {
        super(props);
        
        this.viewerDiv = React.createRef();
        this.viewer = null;
    }

    handleScriptLoad() {
        const options = {
            accessToken: repo.getAccessToken(),
            env: 'AutodeskProduction',
            api: 'derivativeV2'
        };

        // If using Azure proxy, configure for local environment
        if (this.props.useAzureProxy) {
            options.env = 'Local';
            options.endpoint = '/api/azuresvfproxy/';
        }

        Autodesk = window.Autodesk;

        const container = this.viewerDiv.current;
        this.viewer = new Autodesk.Viewing.GuiViewer3D(container);

        Autodesk.Viewing.Initializer(options, this.handleViewerInit.bind(this));
    }

    handleViewerInit() {
        const errorCode = this.viewer.start();
        if (errorCode) {
            console.error('Failed to start viewer:', errorCode);
            if (this.props.onError) {
                this.props.onError('Failed to initialize viewer');
            }
            return;
        }

        // Configure viewer settings
        this.viewer.setTheme('dark-theme');
        this.viewer.setQualityLevel(false, true);
        
        // Set default camera orientation
        this.viewer.addEventListener(Autodesk.Viewing.EXTENSION_LOADED_EVENT, (event) => {
            const viewCubeExtensionId = "Autodesk.ViewCubeUi";
            
            if (event.extensionId === viewCubeExtensionId) {
                const viewCubeUI = event.target.getExtension(viewCubeExtensionId);
                viewCubeUI.setViewCube("front top right");
                this.viewer.removeEventListener(Autodesk.Viewing.EXTENSION_LOADED_EVENT);
            }
        });

        // Load the model
        if (this.props.svfUrl || this.props.urn) {
            this.loadModel();
        }
    }

    loadModel() {
        if (this.props.svfUrl) {
            // Direct SVF URL loading (for Azure proxy)
            this.loadSvfDirectly(this.props.svfUrl);
        } else if (this.props.urn) {
            // URN-based loading (for Model Derivative)
            this.loadFromUrn(this.props.urn);
        }
    }

    loadSvfDirectly(svfUrl) {
        // For Azure proxy, construct the bubble.json URL
        let bubbleUrl = svfUrl;
        
        if (this.props.useAzureProxy) {
            // Handle both extract and direct proxy endpoints
            if (this.props.projectId && this.props.hash) {
                bubbleUrl = `/api/azuresvfproxy/extract/${this.props.projectId}/${this.props.hash}/output/bubble.json`;
            } else {
                // Direct proxy path
                bubbleUrl = svfUrl.endsWith('bubble.json') ? svfUrl : `${svfUrl}/bubble.json`;
            }
        }

        console.log('Loading SVF from:', bubbleUrl);

        Autodesk.Viewing.Document.load(
            bubbleUrl,
            (doc) => this.onDocumentLoadSuccess(doc),
            (errorCode, errorMsg) => this.onDocumentLoadFailure(errorCode, errorMsg)
        );
    }

    loadFromUrn(urn) {
        const documentId = `urn:${urn}`;
        
        Autodesk.Viewing.Document.load(
            documentId,
            (doc) => this.onDocumentLoadSuccess(doc),
            (errorCode, errorMsg) => this.onDocumentLoadFailure(errorCode, errorMsg)
        );
    }

    onDocumentLoadSuccess(doc) {
        const viewables = doc.getRoot().getDefaultGeometry();
        
        if (!viewables) {
            console.error('Document contains no viewables.');
            if (this.props.onError) {
                this.props.onError('No viewable content found in the model');
            }
            return;
        }

        this.viewer.loadDocumentNode(doc, viewables).then(() => {
            console.log('Model loaded successfully');
            if (this.props.onModelLoaded) {
                this.props.onModelLoaded();
            }
        }).catch((error) => {
            console.error('Error loading model:', error);
            if (this.props.onError) {
                this.props.onError(error.message || 'Failed to load model');
            }
        });
    }

    onDocumentLoadFailure(errorCode, errorMsg) {
        console.error('Failed to load document:', errorCode, errorMsg);
        if (this.props.onError) {
            this.props.onError(`Failed to load document: ${errorMsg}`);
        }
    }

    componentDidUpdate(prevProps) {
        if (Autodesk && this.viewer && 
            (this.props.svfUrl !== prevProps.svfUrl || 
             this.props.urn !== prevProps.urn)) {
            
            // Unload current model
            this.viewer.unloadModel();
            
            // Load new model
            if (this.props.svfUrl || this.props.urn) {
                this.loadModel();
            }
        }
    }

    componentWillUnmount() {
        if (this.viewer) {
            this.viewer.finish();
            this.viewer = null;
            Autodesk.Viewing.shutdown();
        }
    }

    render() {
        return (
            <div className="modelContainer fullheight">
                <div className="viewer" id="AzureOptimizedViewer">
                    <div ref={this.viewerDiv}></div>
                    <link rel="stylesheet" type="text/css" href={viewerCss} />
                    <Script url={viewerJs} onLoad={this.handleScriptLoad.bind(this)} />
                </div>
            </div>
        );
    }
}

// PropTypes
AzureOptimizedViewer.propTypes = {
    svfUrl: PropTypes.string,
    urn: PropTypes.string,
    useAzureProxy: PropTypes.bool,
    projectId: PropTypes.string,
    hash: PropTypes.string,
    onError: PropTypes.func,
    onModelLoaded: PropTypes.func
};

AzureOptimizedViewer.defaultProps = {
    useAzureProxy: true
};

export default AzureOptimizedViewer;