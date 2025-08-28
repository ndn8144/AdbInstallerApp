# PowerShell script to fix event method calls in AdvancedInstallOrchestrator.cs
$filePath = "z:\AdbInstallerApp\src\AdbInstallerApp\Services\AdvancedInstallOrchestrator.cs"
$content = Get-Content $filePath -Raw

# Replace all incorrect event calls with proper RaiseEvent calls
$content = $content -replace 'InstallationEvent\?\.Invoke\(this, new InstallationEventArgs\(([^)]+)\)\);', 'RaiseEvent($1);'

# Write back to file
Set-Content $filePath $content -NoNewline
Write-Host "Fixed event calls in AdvancedInstallOrchestrator.cs"
