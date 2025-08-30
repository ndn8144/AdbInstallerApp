# Test Foundation Migration Complete
Write-Host "Testing Foundation Migration Status..." -ForegroundColor Green

# Test 1: Check if ProcessRunner.RunAsync is completely removed
Write-Host "`nTest 1: Checking for ProcessRunner.RunAsync usage..." -ForegroundColor Yellow
$processRunnerUsage = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | 
    Select-String -Pattern "ProcessRunner\.RunAsync" | 
    Select-Object -ExpandProperty Line

if ($processRunnerUsage) {
    Write-Host "❌ FAILED: Found ProcessRunner.RunAsync usage:" -ForegroundColor Red
    $processRunnerUsage | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host "✅ PASSED: No ProcessRunner.RunAsync usage found" -ForegroundColor Green
}

# Test 2: Check if Proc.RunAsync is properly used
Write-Host "`nTest 2: Checking Proc.RunAsync usage..." -ForegroundColor Yellow
$procUsage = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | 
    Select-String -Pattern "Proc\.RunAsync" | 
    Select-Object -ExpandProperty Line

if ($procUsage) {
    Write-Host "✅ PASSED: Found Proc.RunAsync usage:" -ForegroundColor Green
    $procUsage | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
    if ($procUsage.Count -gt 5) {
        Write-Host "  ... and $($procUsage.Count - 5) more" -ForegroundColor Green
    }
} else {
    Write-Host "❌ FAILED: No Proc.RunAsync usage found" -ForegroundColor Red
}

# Test 3: Check if AdbService constructor accepts dependencies
Write-Host "`nTest 3: Checking AdbService constructor..." -ForegroundColor Yellow
$adbServiceConstructor = Get-Content "src/AdbInstallerApp/Services/AdbService.cs" | 
    Select-String -Pattern "public AdbService\(.*GlobalStatusService" | 
    Select-Object -ExpandProperty Line

if ($adbServiceConstructor) {
    Write-Host "✅ PASSED: AdbService constructor accepts GlobalStatusService" -ForegroundColor Green
    Write-Host "  $($adbServiceConstructor.Trim())" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: AdbService constructor missing GlobalStatusService parameter" -ForegroundColor Red
}

# Test 4: Check if RunAdbAsync helper exists
Write-Host "`nTest 4: Checking RunAdbAsync helper..." -ForegroundColor Yellow
$runAdbAsync = Get-Content "src/AdbInstallerApp/Services/AdbService.cs" | 
    Select-String -Pattern "private async Task<ProcResult> RunAdbAsync" | 
    Select-Object -ExpandProperty Line

if ($runAdbAsync) {
    Write-Host "✅ PASSED: RunAdbAsync helper method exists" -ForegroundColor Green
    Write-Host "  $($runAdbAsync.Trim())" -ForegroundColor Green
} else {
    Write-Host "❌ FAILED: RunAdbAsync helper method missing" -ForegroundColor Red
}

# Test 5: Check if PM Session methods exist
Write-Host "`nTest 5: Checking PM Session methods..." -ForegroundColor Yellow
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

# Test 6: Check if supporting types exist
Write-Host "`nTest 6: Checking supporting types..." -ForegroundColor Yellow
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

# Test 7: Check if packages are added
Write-Host "`nTest 7: Checking required packages..." -ForegroundColor Yellow
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

# Final Summary
Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "FOUNDATION MIGRATION STATUS SUMMARY" -ForegroundColor Cyan
Write-Host "="*60 -ForegroundColor Cyan

$tests = @(
    @{ Name = "ProcessRunner.RunAsync Removed"; Status = $null -eq $processRunnerUsage },
    @{ Name = "Proc.RunAsync Usage"; Status = $null -ne $procUsage },
    @{ Name = "AdbService Constructor Updated"; Status = $null -ne $adbServiceConstructor },
    @{ Name = "RunAdbAsync Helper Exists"; Status = $null -ne $runAdbAsync },
    @{ Name = "PM Session Methods"; Status = $allMethodsExist },
    @{ Name = "Supporting Types"; Status = $allTypesExist },
    @{ Name = "Required Packages"; Status = $allPackagesExist }
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
    Write-Host "`n🎉 FOUNDATION MIGRATION COMPLETE! Ready for Phase 3B and 3C." -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Foundation migration incomplete. Please fix failed tests before proceeding." -ForegroundColor Yellow
}
