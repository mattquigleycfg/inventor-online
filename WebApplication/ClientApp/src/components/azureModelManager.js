import React, { useState, useEffect } from 'react';
import { Table, Button, Space, Alert, Spin, Modal } from 'antd';
import { EyeOutlined, CloudDownloadOutlined, FileOutlined } from '@ant-design/icons';
import axios from 'axios';
import AzureTranslation from './azureTranslation';
import ModelDerivativeViewer from './modelDerivativeViewer';

const AzureModelManager = () => {
    const [models, setModels] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [viewerUrn, setViewerUrn] = useState(null);
    const [viewerVisible, setViewerVisible] = useState(false);
    const [viewerError, setViewerError] = useState(null);

    useEffect(() => {
        fetchAzureModels();
    }, []);

    const fetchAzureModels = async () => {
        try {
            setLoading(true);
            const response = await axios.get('/api/azuremodels');
            setModels(response.data);
            setError(null);
        } catch (err) {
            setError(err.response?.data?.error || 'Failed to fetch Azure models');
        } finally {
            setLoading(false);
        }
    };

    const handleTranslationComplete = (urn) => {
        // Optionally refresh the model list or show a success message
        console.log('Translation completed with URN:', urn);
        
        // Automatically open viewer with translated model
        viewTranslatedModel(urn);
    };

    const viewTranslatedModel = (urn) => {
        setViewerUrn(urn);
        setViewerVisible(true);
        setViewerError(null);
    };

    const downloadModel = async (modelName) => {
        try {
            const response = await axios.get(`/api/azuremodels/url/${encodeURIComponent(modelName)}`);
            const url = response.data.url;
            
            // Open in new tab
            window.open(url, '_blank');
        } catch (err) {
            console.error('Failed to get download URL:', err);
        }
    };

    const handleViewerError = (error) => {
        setViewerError(error);
    };

    const handleViewerModalClose = () => {
        setViewerVisible(false);
        setViewerUrn(null);
        setViewerError(null);
    };

    const columns = [
        {
            title: 'Name',
            dataIndex: 'displayName',
            key: 'displayName',
            render: (text, record) => (
                <Space>
                    <FileOutlined />
                    {text || record.name}
                </Space>
            ),
        },
        {
            title: 'Size',
            dataIndex: 'size',
            key: 'size',
            render: (size) => {
                if (size === 0) return '-';
                const mb = (size / (1024 * 1024)).toFixed(2);
                return `${mb} MB`;
            },
        },
        {
            title: 'Last Modified',
            dataIndex: 'lastModified',
            key: 'lastModified',
            render: (date) => new Date(date).toLocaleString(),
        },
        {
            title: 'Actions',
            key: 'actions',
            render: (_, record) => (
                <Space>
                    <AzureTranslation 
                        azureFileName={record.name}
                        onTranslationComplete={handleTranslationComplete}
                    />
                    <Button 
                        icon={<CloudDownloadOutlined />}
                        onClick={() => downloadModel(record.name)}
                    >
                        Download
                    </Button>
                </Space>
            ),
        },
    ];

    if (loading) {
        return (
            <div style={{ textAlign: 'center', padding: '50px' }}>
                <Spin size="large" />
                <div>Loading Azure models...</div>
            </div>
        );
    }

    return (
        <div style={{ padding: '20px' }}>
            <h2>Azure Blob Storage Models</h2>
            
            {error && (
                <Alert
                    message="Error"
                    description={error}
                    type="error"
                    showIcon
                    closable
                    onClose={() => setError(null)}
                    style={{ marginBottom: '20px' }}
                />
            )}
            
            <Table
                dataSource={models}
                columns={columns}
                rowKey="name"
                pagination={{
                    pageSize: 10,
                    showSizeChanger: true,
                    showTotal: (total) => `Total ${total} models`,
                }}
            />
            
            <Modal
                title="Model Derivative Viewer"
                visible={viewerVisible}
                onCancel={handleViewerModalClose}
                width="90%"
                style={{ top: 20 }}
                footer={null}
                destroyOnClose
            >
                {viewerError && (
                    <Alert
                        message="Viewer Error"
                        description={viewerError}
                        type="error"
                        showIcon
                        style={{ marginBottom: '20px' }}
                    />
                )}
                
                {viewerUrn && (
                    <div style={{ height: '600px' }}>
                        <ModelDerivativeViewer
                            urn={viewerUrn}
                            onError={handleViewerError}
                            onModelLoaded={() => console.log('Model loaded in viewer')}
                        />
                    </div>
                )}
            </Modal>
        </div>
    );
};

export default AzureModelManager;