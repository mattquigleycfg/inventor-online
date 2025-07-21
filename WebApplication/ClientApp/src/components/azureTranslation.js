import React, { useState } from 'react';
import { Button, Modal, Progress, Alert, Upload, Input, Space } from 'antd';
import { UploadOutlined, CloudUploadOutlined } from '@ant-design/icons';
import axios from 'axios';

const AzureTranslation = ({ azureFileName, onTranslationComplete }) => {
    const [isModalVisible, setIsModalVisible] = useState(false);
    const [isTranslating, setIsTranslating] = useState(false);
    const [translationProgress, setTranslationProgress] = useState(0);
    const [translationStatus, setTranslationStatus] = useState('');
    const [error, setError] = useState(null);
    const [urn, setUrn] = useState(null);
    const [uploadFile, setUploadFile] = useState(null);
    const [rootFilename, setRootFilename] = useState('');

    const startTranslation = async (file = null) => {
        setIsTranslating(true);
        setError(null);
        setTranslationProgress(0);
        
        try {
            let response;
            
            if (file) {
                // Upload and translate file
                const formData = new FormData();
                formData.append('file', file);
                if (rootFilename) {
                    formData.append('rootFilename', rootFilename);
                }
                
                response = await axios.post('/api/modelderivative/translate/upload', formData, {
                    headers: {
                        'Content-Type': 'multipart/form-data'
                    }
                });
            } else if (azureFileName) {
                // Translate Azure file
                response = await axios.post(`/api/modelderivative/translate/azure/${encodeURIComponent(azureFileName)}`);
            } else {
                throw new Error('No file specified for translation');
            }
            
            const { urn: translationUrn } = response.data;
            setUrn(translationUrn);
            
            // Poll for translation progress
            pollTranslationProgress(translationUrn);
            
        } catch (err) {
            setError(err.response?.data?.error || err.message);
            setIsTranslating(false);
        }
    };

    const pollTranslationProgress = async (translationUrn) => {
        const maxAttempts = 60; // 5 minutes with 5-second intervals
        let attempts = 0;
        
        const checkProgress = async () => {
            try {
                const response = await axios.get(`/api/modelderivative/translate/progress/${translationUrn}`);
                const { status, progress, hasDerivatives } = response.data;
                
                setTranslationStatus(status);
                
                if (status === 'success' && hasDerivatives) {
                    setTranslationProgress(100);
                    setIsTranslating(false);
                    if (onTranslationComplete) {
                        onTranslationComplete(translationUrn);
                    }
                    return;
                } else if (status === 'failed' || status === 'timeout') {
                    throw new Error(`Translation ${status}`);
                } else if (status === 'inprogress' || status === 'pending') {
                    // Update progress
                    const progressPercent = progress === 'complete' ? 90 : 
                                          parseInt(progress?.replace('%', '') || '0');
                    setTranslationProgress(progressPercent);
                    
                    attempts++;
                    if (attempts < maxAttempts) {
                        setTimeout(checkProgress, 5000); // Check every 5 seconds
                    } else {
                        throw new Error('Translation timeout');
                    }
                }
            } catch (err) {
                setError(err.response?.data?.error || err.message);
                setIsTranslating(false);
            }
        };
        
        checkProgress();
    };

    const handleFileUpload = (file) => {
        setUploadFile(file);
        return false; // Prevent auto upload
    };

    const showModal = () => {
        setIsModalVisible(true);
    };

    const handleOk = () => {
        if (uploadFile) {
            startTranslation(uploadFile);
        } else if (azureFileName) {
            startTranslation();
        }
    };

    const handleCancel = () => {
        setIsModalVisible(false);
        setUploadFile(null);
        setRootFilename('');
    };

    return (
        <>
            {azureFileName ? (
                <Button 
                    icon={<CloudUploadOutlined />} 
                    onClick={showModal}
                    type="primary"
                >
                    Translate with Model Derivative
                </Button>
            ) : (
                <Button 
                    icon={<UploadOutlined />} 
                    onClick={showModal}
                >
                    Upload & Translate File
                </Button>
            )}
            
            <Modal
                title={azureFileName ? `Translate ${azureFileName}` : "Upload & Translate File"}
                visible={isModalVisible}
                onOk={handleOk}
                onCancel={handleCancel}
                okText="Start Translation"
                okButtonProps={{ disabled: isTranslating || (!azureFileName && !uploadFile) }}
                cancelButtonProps={{ disabled: isTranslating }}
                width={600}
            >
                {!azureFileName && (
                    <Space direction="vertical" style={{ width: '100%' }}>
                        <Upload
                            beforeUpload={handleFileUpload}
                            maxCount={1}
                            accept=".ipt,.iam,.dwg,.stp,.step,.igs,.iges,.sat"
                        >
                            <Button icon={<UploadOutlined />}>Select File</Button>
                        </Upload>
                        
                        {uploadFile && (
                            <>
                                <div>Selected: {uploadFile.name}</div>
                                <Input
                                    placeholder="Root filename (for assemblies with multiple files)"
                                    value={rootFilename}
                                    onChange={(e) => setRootFilename(e.target.value)}
                                />
                            </>
                        )}
                    </Space>
                )}
                
                {isTranslating && (
                    <div style={{ marginTop: 20 }}>
                        <Progress percent={translationProgress} status="active" />
                        <div>Status: {translationStatus}</div>
                    </div>
                )}
                
                {error && (
                    <Alert
                        message="Translation Error"
                        description={error}
                        type="error"
                        showIcon
                        style={{ marginTop: 20 }}
                    />
                )}
                
                {urn && !isTranslating && !error && (
                    <Alert
                        message="Translation Complete"
                        description={`URN: ${urn}`}
                        type="success"
                        showIcon
                        style={{ marginTop: 20 }}
                    />
                )}
            </Modal>
        </>
    );
};

export default AzureTranslation;