# Test Fixed DI Container
Write-Host "Testing Fixed DI Container..." -ForegroundColor Green

try {
    # Kill any existing instances
    Write-Host "Killing existing instances..." -ForegroundColor Yellow
    Get-Process -Name "AdbInstallerApp" -ErrorAction SilentlyContinue | Stop-Process -Force
    
    # Wait a moment
    Start-Sleep -Seconds 2
    
    # Build project
    Write-Host "Building project..." -ForegroundColor Yellow
    dotnet build src/AdbInstallerApp/AdbInstallerApp.csproj
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        
        # Check if only one UI will be created
        Write-Host "Checking App.xaml for StartupUri..." -ForegroundColor Yellow
        $appXaml = Get-Content "src/AdbInstallerApp/App.xaml" -Raw
        if ($appXaml -match "StartupUri") {
            Write-Host "WARNING: StartupUri still exists in App.xaml!" -ForegroundColor Red
        } else {
            Write-Host "✅ StartupUri removed from App.xaml" -ForegroundColor Green
        }
        
        # Check App.xaml.cs for DI setup
        Write-Host "Checking App.xaml.cs for DI setup..." -ForegroundColor Yellow
        $appCs = Get-Content "src/AdbInstallerApp/App.xaml.cs" -Raw
        if ($appCs -match "services\.AddSingleton<IApkAnalyzerService") {
            Write-Host "✅ ApkAnalyzerService registered in DI" -ForegroundColor Green
        } else {
            Write-Host "❌ ApkAnalyzerService not registered in DI" -ForegroundColor Red
        }
        
        if ($appCs -match "services\.AddSingleton<OptimizedProgressService") {
            Write-Host "✅ OptimizedProgressService registered in DI" -ForegroundColor Green
        } else {
            Write-Host "❌ OptimizedProgressService not registered in DI" -ForegroundColor Red
        }
        
        # Run application
        Write-Host "Running application..." -ForegroundColor Yellow
        Write-Host "Expected: Only ONE UI window should appear" -ForegroundColor Cyan
        Write-Host "Expected: No more popup test messages" -ForegroundColor Cyan
        
        dotnet run --project src/AdbInstallerApp/AdbInstallerApp.csproj
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
