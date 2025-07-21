import React, { useState, useEffect } from 'react';
import { Button, Card, Space, Typography, Alert, Spin, Divider, Tag, message } from 'antd';
import { CheckCircleOutlined, CloudOutlined, ApiOutlined, ExperimentOutlined, LoadingOutlined } from '@ant-design/icons';
import axios from 'axios';
import AzureModelManager from './azureModelManager';

const { Title, Text, Paragraph } = Typography;

const AzureIntegrationTest = () => {
    const [testResults, setTestResults] = useState({});
    const [loading, setLoading] = useState({});
    const [showModelManager, setShowModelManager] = useState(false);

    // Test 1: Check Azure Configuration
    const testAzureConfig = async () => {
        setLoading(prev => ({ ...prev, config: true }));
        try {
            const response = await axios.get('/api/test/azure/check-config');
            setTestResults(prev => ({ ...prev, config: response.data }));
            message.success('Azure configuration check completed');
        } catch (error) {
            setTestResults(prev => ({ ...prev, config: { status: 'error', error: error.message } }));
            message.error('Azure configuration check failed');
        }
        setLoading(prev => ({ ...prev, config: false }));
    };

    // Test 2: List Blobs
    const testListBlobs = async () => {
        setLoading(prev => ({ ...prev, blobs: true }));
        try {
            const response = await axios.get('/api/test/azure/list-blobs');
            setTestResults(prev => ({ ...prev, blobs: response.data }));
            message.success('Blob listing completed');
        } catch (error) {
            setTestResults(prev => ({ ...prev, blobs: { status: 'error', error: error.message } }));
            message.error('Blob listing failed');
        }
        setLoading(prev => ({ ...prev, blobs: false }));
    };

    // Test 3: Create Test Bucket
    const testCreateBucket = async () => {
        setLoading(prev => ({ ...prev, bucket: true }));
        try {
            const response = await axios.post('/api/test/azure/create-test-bucket');
            setTestResults(prev => ({ ...prev, bucket: response.data }));
            message.success('Test bucket created');
        } catch (error) {
            setTestResults(prev => ({ ...prev, bucket: { status: 'error', error: error.message } }));
            message.error('Bucket creation failed');
        }
        setLoading(prev => ({ ...prev, bucket: false }));
    };

    // Test 4: Generate SAS URL
    const testGenerateSAS = async () => {
        setLoading(prev => ({ ...prev, sas: true }));
        try {
            const response = await axios.get('/api/test/azure/generate-sas?blobName=test.txt');
            setTestResults(prev => ({ ...prev, sas: response.data }));
            message.success('SAS URL generated');
        } catch (error) {
            setTestResults(prev => ({ ...prev, sas: { status: 'error', error: error.message } }));
            message.error('SAS generation failed');
        }
        setLoading(prev => ({ ...prev, sas: false }));
    };

    // Run all tests
    const runAllTests = async () => {
        await testAzureConfig();
        await testListBlobs();
        await testCreateBucket();
        await testGenerateSAS();
    };

    useEffect(() => {
        // Run config test on mount
        testAzureConfig();
    }, []);

    const renderTestResult = (key, title) => {
        const result = testResults[key];
        const isLoading = loading[key];

        return (
            <Card
                size="small"
                title={
                    <Space>
                        {isLoading && <LoadingOutlined />}
                        {result && result.status === 'success' && <CheckCircleOutlined style={{ color: '#52c41a' }} />}
                        {result && result.status === 'error' && <CheckCircleOutlined style={{ color: '#ff4d4f' }} />}
                        {title}
                    </Space>
                }
                style={{ marginBottom: 16 }}
            >
                {isLoading && <Spin />}
                {result && (
                    <pre style={{ fontSize: '12px', maxHeight: '200px', overflow: 'auto' }}>
                        {JSON.stringify(result, null, 2)}
                    </pre>
                )}
            </Card>
        );
    };

    return (
        <div style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
            <Title level={2}>
                <CloudOutlined /> Azure Integration Test Suite
            </Title>
            
            <Alert
                message="Test Instructions"
                description={
                    <div>
                        <Paragraph>Use this page to test the Azure Blob Storage and Model Derivative integration:</Paragraph>
                        <ol>
                            <li>Click "Run All Tests" to execute all integration tests</li>
                            <li>Review the results in each test card</li>
                            <li>Use "Show Model Manager" to test the full UI integration</li>
                            <li>Check the browser console for additional debug information</li>
                        </ol>
                    </div>
                }
                type="info"
                showIcon
                style={{ marginBottom: 20 }}
            />

            <Space style={{ marginBottom: 20 }}>
                <Button type="primary" onClick={runAllTests} icon={<ExperimentOutlined />}>
                    Run All Tests
                </Button>
                <Button onClick={() => setShowModelManager(!showModelManager)} icon={<ApiOutlined />}>
                    {showModelManager ? 'Hide' : 'Show'} Model Manager
                </Button>
            </Space>

            <Divider />

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '16px' }}>
                {renderTestResult('config', 'Azure Configuration')}
                {renderTestResult('blobs', 'List Blobs')}
                {renderTestResult('bucket', 'Create Forge Bucket')}
                {renderTestResult('sas', 'Generate SAS URL')}
            </div>

            {showModelManager && (
                <>
                    <Divider />
                    <AzureModelManager />
                </>
            )}

            <Divider />

            <Card title="API Endpoints for Manual Testing" size="small">
                <Space direction="vertical" style={{ width: '100%' }}>
                    <Text code>GET /api/test/azure/check-config</Text>
                    <Text code>GET /api/test/azure/list-blobs?prefix=svf</Text>
                    <Text code>POST /api/test/azure/create-test-bucket</Text>
                    <Text code>GET /api/test/azure/generate-sas?blobName=test.txt</Text>
                    <Text code>POST /api/test/azure/test-full-workflow?azureBlobName=MRConfigurator.zip</Text>
                    <Text code>GET /api/test/azure/check-translation?urn=YOUR_URN</Text>
                </Space>
            </Card>

            <Divider />

            <Alert
                message="Testing Tips"
                description={
                    <ul>
                        <li>Make sure the application is running: <code>dotnet run</code></li>
                        <li>Check that your Azure SAS token is valid (expires: 2026-07-21)</li>
                        <li>Use F12 Developer Tools to monitor network requests</li>
                        <li>Check application logs for detailed error messages</li>
                    </ul>
                }
                type="warning"
            />
        </div>
    );
};

export default AzureIntegrationTest;