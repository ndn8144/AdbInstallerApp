# Test DI Container
Write-Host "Testing DI Container..." -ForegroundColor Green

try {
    # Build project
    Write-Host "Building project..." -ForegroundColor Yellow
    dotnet build src/AdbInstallerApp/AdbInstallerApp.csproj
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        
        # Try to run with verbose output
        Write-Host "Running application..." -ForegroundColor Yellow
        dotnet run --project src/AdbInstallerApp/AdbInstallerApp.csproj --verbosity detailed
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
