# PowerShell script to migrate Design Automation from v2 to v3
param(
    [Parameter(Mandatory=$true)]
    [string]$ClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$ClientSecret,
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "https://developer.api.autodesk.com/da/us-east/v3",
    
    [Parameter(Mandatory=$false)]
    [string]$AuthUrl = "https://developer.api.autodesk.com/authentication/v2/token",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

function Get-AccessToken {
    param($clientId, $clientSecret, $authUrl)
    
    Write-Host "Getting access token..."
    
    $body = @{
        grant_type = "client_credentials"
        scope = "code:all"
    }
    
    $credentials = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("$clientId`:$clientSecret"))
    $headers = @{
        "Authorization" = "Basic $credentials"
        "Content-Type" = "application/x-www-form-urlencoded"
    }
    
    try {
        $response = Invoke-RestMethod -Uri $authUrl -Method Post -Body $body -Headers $headers
        return $response.access_token
    }
    catch {
        Write-Error "Failed to get access token: $_"
        exit 1
    }
}

function Get-Nickname {
    param($accessToken, $baseUrl)
    
    Write-Host "Getting nickname..."
    
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/json"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/forgeapps/me" -Method Get -Headers $headers
        return $response
    }
    catch {
        Write-Error "Failed to get nickname: $_"
        exit 1
    }
}

function Test-V3Endpoints {
    param($accessToken, $baseUrl)
    
    Write-Host "Testing v3 endpoints..."
    
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/json"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/appbundles" -Method Get -Headers $headers
        Write-Host "App bundles endpoint working"
        
        $response = Invoke-RestMethod -Uri "$baseUrl/activities" -Method Get -Headers $headers
        Write-Host "Activities endpoint working"
        
        $response = Invoke-RestMethod -Uri "$baseUrl/engines" -Method Get -Headers $headers
        Write-Host "Engines endpoint working"
        
        return $true
    }
    catch {
        Write-Error "Failed to test v3 endpoints: $_"
        return $false
    }
}

function Remove-V2Aliases {
    param($accessToken, $baseUrl, $nickname, $dryRun)
    
    Write-Host "Processing v2 aliases..."
    
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/json"
    }
    
    $components = @(
        "DataChecker", "CreateSVF", "CreateThumbnail", "ExtractParameters", 
        "UpdateParameters", "CreateBOM", "ExportDrawing", "TransferData", 
        "CreateRFA", "UpdateDrawings", "AdoptProject", "UpdateProject"
    )
    
    foreach ($component in $components) {
        $activityId = "$nickname.$component"
        $bundleId = "$nickname.$component"
        
        $activityUrl = "$baseUrl/activities/$activityId/aliases/alpha"
        $bundleUrl = "$baseUrl/appbundles/$bundleId/aliases/alpha"
        
        if ($dryRun) {
            Write-Host "[DRY RUN] Would delete activity alias: $activityUrl"
            Write-Host "[DRY RUN] Would delete bundle alias: $bundleUrl"
        }
        else {
            try {
                Write-Host "Deleting activity alias: $activityId+alpha"
                Invoke-RestMethod -Uri $activityUrl -Method Delete -Headers $headers
                Write-Host "Success: $activityId+alpha"
            }
            catch {
                Write-Warning "Failed to delete activity $activityId - $_"
            }
            
            try {
                Write-Host "Deleting bundle alias: $bundleId+alpha"
                Invoke-RestMethod -Uri $bundleUrl -Method Delete -Headers $headers
                Write-Host "Success: $bundleId+alpha"
            }
            catch {
                Write-Warning "Failed to delete bundle $bundleId - $_"
            }
        }
    }
}

# Main execution
Write-Host "Starting Design Automation v2 to v3 migration..."
Write-Host "Client ID: $ClientId"
Write-Host "Base URL: $BaseUrl"
Write-Host "Dry Run: $DryRun"
Write-Host ""

$accessToken = Get-AccessToken -clientId $ClientId -clientSecret $ClientSecret -authUrl $AuthUrl
$nickname = Get-Nickname -accessToken $accessToken -baseUrl $BaseUrl

Write-Host "Nickname: $nickname"
Write-Host ""

if (-not (Test-V3Endpoints -accessToken $accessToken -baseUrl $BaseUrl)) {
    Write-Error "v3 endpoints are not working properly. Aborting migration."
    exit 1
}

Write-Host ""
Remove-V2Aliases -accessToken $accessToken -baseUrl $BaseUrl -nickname $nickname -dryRun:$DryRun

Write-Host ""
Write-Host "Migration completed successfully!"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Deploy your application with v3 configuration"
Write-Host "2. Test that all functionality works correctly"
Write-Host "3. Monitor the application for any issues"
Write-Host ""
Write-Host "Your application should now be using Design Automation v3 endpoints." 