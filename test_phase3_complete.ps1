# Test Phase 3 Complete - Foundation Migration + PM Session + AdvancedInstallOrchestrator
Write-Host "Testing Phase 3 Complete Status..." -ForegroundColor Green

# Test 1: Foundation Migration Status
Write-Host "`nTest 1: Foundation Migration Status..." -ForegroundColor Yellow
$processRunnerUsage = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | 
    Select-String -Pattern "ProcessRunner\.RunAsync" | 
    Select-Object -ExpandProperty Line

if ($processRunnerUsage) {
    Write-Host "❌ FAILED: Found ProcessRunner.RunAsync usage:" -ForegroundColor Red
    $processRunnerUsage | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host "✅ PASSED: ProcessRunner.RunAsync completely removed" -ForegroundColor Green
}

# Test 2: Proc.RunAsync Usage
Write-Host "`nTest 2: Proc.RunAsync Usage..." -ForegroundColor Yellow
$procUsage = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | 
    Select-String -Pattern "Proc\.RunAsync" | 
    Select-Object -ExpandProperty Line

if ($procUsage) {
    Write-Host "✅ PASSED: Found Proc.RunAsync usage:" -ForegroundColor Green
    Write-Host "  Total usages: $($procUsage.Count)" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: No Proc.RunAsync usage found" -ForegroundColor Red
}

# Test 3: AdbService Dependencies
Write-Host "`nTest 3: AdbService Dependencies..." -ForegroundColor Yellow
$adbServiceConstructor = Get-Content "src/AdbInstallerApp/Services/AdbService.cs" | 
    Select-String -Pattern "public AdbService\(.*GlobalStatusService" | 
    Select-Object -ExpandProperty Line

if ($adbServiceConstructor) {
    Write-Host "✅ PASSED: AdbService constructor accepts GlobalStatusService" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: AdbService constructor missing GlobalStatusService parameter" -ForegroundColor Red
}

# Test 4: RunAdbAsync Helper
Write-Host "`nTest 4: RunAdbAsync Helper..." -ForegroundColor Yellow
$runAdbAsync = Get-Content "src/AdbInstallerApp/Services/AdbService.cs" | 
    Select-String -Pattern "private async Task<ProcResult> RunAdbAsync" | 
    Select-Object -ExpandProperty Line

if ($runAdbAsync) {
    Write-Host "✅ PASSED: RunAdbAsync helper method exists" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: RunAdbAsync helper method missing" -ForegroundColor Red
}

# Test 5: PM Session Methods
Write-Host "`nTest 5: PM Session Methods..." -ForegroundColor Yellow
$pmSessionMethods = @(
    "CreateInstallSessionAsync",
    "WriteToSessionAsync", 
    "CommitInstallSessionAsync",
    "AbandonInstallSessionAsync"
)

$allMethodsExist = $true
foreach ($method in $pmSessionMethods) {
    $methodExists = Get-Content "src/AdbInstallerApp/Services/AdbService.cs" | 
        Select-String -Pattern "public async Task.*$method" | 
        Select-Object -ExpandProperty Line
    
    if ($methodExists) {
        Write-Host "  ✅ $method exists" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $method missing" -ForegroundColor Red
        $allMethodsExist = $false
    }
}

if ($allMethodsExist) {
    Write-Host "✅ PASSED: All PM Session methods exist" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: Some PM Session methods missing" -ForegroundColor Red
}

# Test 6: Supporting Types
Write-Host "`nTest 6: Supporting Types..." -ForegroundColor Yellow
$supportingTypes = @(
    "InstallErrorType",
    "InstallationException",
    "InstallSessionOptions"
)

$allTypesExist = $true
foreach ($type in $supportingTypes) {
    $typeExists = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | 
        Select-String -Pattern "enum $type|class $type|record $type" | 
        Select-Object -ExpandProperty Line
    
    if ($typeExists) {
        Write-Host "  ✅ $type exists" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $type missing" -ForegroundColor Red
        $allTypesExist = $false
    }
}

if ($allTypesExist) {
    Write-Host "✅ PASSED: All supporting types exist" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: Some supporting types missing" -ForegroundColor Red
}

# Test 7: Required Packages
Write-Host "`nTest 7: Required Packages..." -ForegroundColor Yellow
$requiredPackages = @(
    "System.Threading.Channels",
    "System.Reactive", 
    "System.IO.Hashing",
    "Microsoft.Extensions.Hosting",
    "Microsoft.Extensions.DependencyInjection"
)

$allPackagesExist = $true
foreach ($package in $requiredPackages) {
    $packageExists = Get-Content "src/AdbInstallerApp/AdbInstallerApp.csproj" | 
        Select-String -Pattern "PackageReference Include=`"$package`"" | 
        Select-Object -ExpandProperty Line
    
    if ($packageExists) {
        Write-Host "  ✅ $package exists" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $package missing" -ForegroundColor Red
        $allPackagesExist = $false
    }
}

if ($allPackagesExist) {
    Write-Host "✅ PASSED: All required packages exist" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: Some required packages missing" -ForegroundColor Red
}

# Test 8: AdvancedInstallOrchestrator.RunAsync
Write-Host "`nTest 8: AdvancedInstallOrchestrator.RunAsync..." -ForegroundColor Yellow
$runAsyncMethod = Get-Content "src/AdbInstallerApp/Services/AdvancedInstallOrchestrator.cs" | 
    Select-String -Pattern "public async Task<InstallationResult> RunAsync" | 
    Select-Object -ExpandProperty Line

if ($runAsyncMethod) {
    Write-Host "✅ PASSED: RunAsync method exists" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: RunAsync method missing" -ForegroundColor Red
}

# Test 9: AdvancedInstallOrchestrator Helper Methods
Write-Host "`nTest 9: AdvancedInstallOrchestrator Helper Methods..." -ForegroundColor Yellow
$helperMethods = @(
    "ValidateAndGroupApksAsync",
    "ExecuteDevicePlanAsync",
    "ExecuteInstallationUnitAsync",
    "InstallUsingPmSessionAsync",
    "DetermineInstallStrategy"
)

$allHelperMethodsExist = $true
foreach ($method in $helperMethods) {
    $methodExists = Get-Content "src/AdbInstallerApp/Services/AdvancedInstallOrchestrator.cs" | 
        Select-String -Pattern "private async Task.*$method|private InstallStrategy $method" | 
        Select-Object -ExpandProperty Line
    
    if ($methodExists) {
        Write-Host "  ✅ $method exists" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $method missing" -ForegroundColor Red
        $allHelperMethodsExist = $false
    }
}

if ($allHelperMethodsExist) {
    Write-Host "✅ PASSED: All helper methods exist" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: Some helper methods missing" -ForegroundColor Red
}

# Test 10: Build Success
Write-Host "`nTest 10: Build Success..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build src/AdbInstallerApp/AdbInstallerApp.csproj --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ PASSED: Project builds successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ FAILED: Project build failed" -ForegroundColor Red
        Write-Host "Build output: $buildResult" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ FAILED: Build test failed with exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Final Summary
Write-Host "`n" + "="*70 -ForegroundColor Cyan
Write-Host "PHASE 3 COMPLETE STATUS SUMMARY" -ForegroundColor Cyan
Write-Host "="*70 -ForegroundColor Cyan

$tests = @(
    @{ Name = "ProcessRunner.RunAsync Removed"; Status = $null -eq $processRunnerUsage },
    @{ Name = "Proc.RunAsync Usage"; Status = $null -ne $procUsage },
    @{ Name = "AdbService Constructor Updated"; Status = $null -ne $adbServiceConstructor },
    @{ Name = "RunAdbAsync Helper Exists"; Status = $null -ne $runAdbAsync },
    @{ Name = "PM Session Methods"; Status = $allMethodsExist },
    @{ Name = "Supporting Types"; Status = $allTypesExist },
    @{ Name = "Required Packages"; Status = $allPackagesExist },
    @{ Name = "AdvancedInstallOrchestrator.RunAsync"; Status = $null -ne $runAsyncMethod },
    @{ Name = "Helper Methods"; Status = $allHelperMethodsExist },
    @{ Name = "Build Success"; Status = $LASTEXITCODE -eq 0 }
)

$passedTests = 0
$totalTests = $tests.Count

foreach ($test in $tests) {
    $status = $test.Status ? "✅ PASS" : "❌ FAIL"
    $color = $test.Status ? "Green" : "Red"
    Write-Host "$status $($test.Name)" -ForegroundColor $color
    if ($test.Status) { $passedTests++ }
}

Write-Host "`nOverall Status: $passedTests/$totalTests tests passed" -ForegroundColor $(if ($passedTests -eq $totalTests) { "Green" } else { "Red" })

if ($passedTests -eq $totalTests) {
    Write-Host "`n🎉 PHASE 3 COMPLETE! All requirements implemented successfully." -ForegroundColor Green
    Write-Host "Ready for real device testing and core services implementation." -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Phase 3 incomplete. Please fix failed tests before proceeding." -ForegroundColor Yellow
}
