import React from 'react';
import { Layout, Typography, Divider } from 'antd';
import AzureModelManager from './azureModelManager';

const { Content } = Layout;
const { Title, Paragraph } = Typography;

const AzureTestPage = () => {
    return (
        <Layout style={{ minHeight: '100vh', backgroundColor: '#f0f2f5' }}>
            <Content style={{ padding: '20px 50px' }}>
                <Title level={2}>Azure Blob Storage & Model Derivative Integration</Title>
                <Paragraph>
                    This page demonstrates the integration between Azure Blob Storage and Autodesk Model Derivative API.
                    You can:
                </Paragraph>
                <ul>
                    <li>View files stored in Azure Blob Storage</li>
                    <li>Translate files using Model Derivative API</li>
                    <li>View translated models in the Autodesk Viewer</li>
                    <li>Upload new files for translation</li>
                </ul>
                
                <Divider />
                
                <AzureModelManager />
            </Content>
        </Layout>
    );
};

export default AzureTestPage;