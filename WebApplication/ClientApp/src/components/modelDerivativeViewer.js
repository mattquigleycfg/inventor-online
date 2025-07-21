import React, { Component } from 'react';
import Script from 'react-load-script';
import { viewerCss, viewerJs } from './shared';
import repo from '../Repository';
import './forgeView.css';

let Autodesk = null;

export class ModelDerivativeViewer extends Component {
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

        Autodesk = window.Autodesk;

        const container = this.viewerDiv.current;
        this.viewer = new Autodesk.Viewing.GuiViewer3D(container);

        Autodesk.Viewing.Initializer(options, this.handleViewerInit.bind(this));
    }

    handleViewerInit() {
        const errorCode = this.viewer.start();
        if (errorCode) {
            console.error('Failed to start viewer:', errorCode);
            return;
        }

        // Configure viewer settings
        this.viewer.setTheme('dark-theme');
        this.viewer.setQualityLevel(false, true); // Set quality to high
        
        // Set default camera orientation
        this.viewer.addEventListener(Autodesk.Viewing.EXTENSION_LOADED_EVENT, (event) => {
            const viewCubeExtensionId = "Autodesk.ViewCubeUi";
            
            if (event.extensionId === viewCubeExtensionId) {
                const viewCubeUI = event.target.getExtension(viewCubeExtensionId);
                viewCubeUI.setViewCube("front top right");
                this.viewer.removeEventListener(Autodesk.Viewing.EXTENSION_LOADED_EVENT);
            }
        });

        // Load the model if URN is provided
        if (this.props.urn) {
            this.loadModel(this.props.urn);
        }
    }

    loadModel(urn) {
        const documentId = `urn:${urn}`;
        
        Autodesk.Viewing.Document.load(
            documentId,
            (doc) => this.onDocumentLoadSuccess(doc),
            (errorCode, errorMsg) => this.onDocumentLoadFailure(errorCode, errorMsg)
        );
    }

    onDocumentLoadSuccess(doc) {
        // Get the default viewable
        const viewables = doc.getRoot().getDefaultGeometry();
        
        if (!viewables) {
            console.error('Document contains no viewables.');
            if (this.props.onError) {
                this.props.onError('No viewable content found in the model');
            }
            return;
        }

        // Load the viewable
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
        if (Autodesk && this.viewer && (this.props.urn !== prevProps.urn)) {
            // Unload current model
            this.viewer.unloadModel();
            
            // Load new model
            if (this.props.urn) {
                this.loadModel(this.props.urn);
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
                <div className="viewer" id="ModelDerivativeViewer">
                    <div ref={this.viewerDiv}></div>
                    <link rel="stylesheet" type="text/css" href={viewerCss} />
                    <Script url={viewerJs} onLoad={this.handleScriptLoad.bind(this)} />
                </div>
            </div>
        );
    }
}

export default ModelDerivativeViewer;