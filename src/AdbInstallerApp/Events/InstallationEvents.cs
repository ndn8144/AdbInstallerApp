using System;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.Events
{
    /// <summary>
    /// Base class for all installation events
    /// </summary>
    public abstract record InstallationEvent(DateTime Timestamp, string Serial, string Message)
    {
        public static InstallationEvent PlanBuilt(string serial, int units) => 
            new InfoEvent(DateTime.Now, serial, $"Plan built: {units} installation unit(s)");
            
        public static InstallationEvent DeviceStart(string serial, int index, int total) => 
            new InfoEvent(DateTime.Now, serial, $"Starting device [{index}/{total}] {serial}");
            
        public static InstallationEvent UnitStart(string serial, string packageName, int index, int total, int fileCount) => 
            new InfoEvent(DateTime.Now, serial, $"[{index}/{total}] Installing {packageName} ({fileCount} file{(fileCount > 1 ? "s" : "")})");
            
        public static InstallationEvent AdbCommand(string serial, string command) => 
            new CommandEvent(DateTime.Now, serial, $"ADB: {command}");
            
        public static InstallationEvent UnitSuccess(string serial, string packageName, TimeSpan duration) => 
            new SuccessEvent(DateTime.Now, serial, $"✅ SUCCESS {packageName} ({duration.TotalSeconds:F1}s)");
            
        public static InstallationEvent UnitFailure(string serial, string packageName, string error, bool canRetry = false) => 
            new ErrorEvent(DateTime.Now, serial, $"❌ FAILED {packageName}: {error}{(canRetry ? " (will retry)" : "")}");
            
        public static InstallationEvent Warning(string serial, string message) => 
            new WarningEvent(DateTime.Now, serial, $"⚠️ {message}");
            
        public static InstallationEvent DeviceCompleted(string serial, int successful, int failed, int skipped) => 
            new InfoEvent(DateTime.Now, serial, $"Device completed: {successful} success, {failed} failed, {skipped} skipped");
            
        public static InstallationEvent AllCompleted(InstallationSummary summary) => 
            new InfoEvent(DateTime.Now, "", $"🎉 All installations completed: {summary.GetSummary()}");
            
        public static InstallationEvent PreflightCheck(string serial, string message) => 
            new InfoEvent(DateTime.Now, serial, $"🔍 Preflight: {message}");
            
        public static InstallationEvent SplitMatching(string serial, string packageName, int matched, int total) => 
            new InfoEvent(DateTime.Now, serial, $"📦 {packageName}: matched {matched}/{total} split APKs");
            
        public static InstallationEvent DeviceUnauthorized(string serial) => 
            new WarningEvent(DateTime.Now, serial, "Device unauthorized - enable USB debugging and accept computer");
            
        public static InstallationEvent DeviceOffline(string serial) => 
            new WarningEvent(DateTime.Now, serial, "Device offline - check USB connection");
            
        public static InstallationEvent RetryAttempt(string serial, string packageName, int attempt, int maxRetries) => 
            new WarningEvent(DateTime.Now, serial, $"🔄 Retry {attempt}/{maxRetries} for {packageName}");
    }

    /// <summary>
    /// Information event (normal operation)
    /// </summary>
    public sealed record InfoEvent(DateTime Timestamp, string Serial, string Message) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Warning event (non-critical issues)
    /// </summary>
    public sealed record WarningEvent(DateTime Timestamp, string Serial, string Message) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Error event (failures)
    /// </summary>
    public sealed record ErrorEvent(DateTime Timestamp, string Serial, string Message) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Success event (successful operations)
    /// </summary>
    public sealed record SuccessEvent(DateTime Timestamp, string Serial, string Message) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Command event (ADB commands being executed)
    /// </summary>
    public sealed record CommandEvent(DateTime Timestamp, string Serial, string Message) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Progress event with percentage
    /// </summary>
    public sealed record ProgressEvent(DateTime Timestamp, string Serial, string Message, double Progress) : InstallationEvent(Timestamp, Serial, Message);

    /// <summary>
    /// Event arguments for installation events
    /// </summary>
    public class InstallationEventArgs : EventArgs
    {
        public InstallationEvent Event { get; }
        
        public InstallationEventArgs(InstallationEvent installEvent)
        {
            Event = installEvent;
        }
    }

    /// <summary>
    /// Progress update event arguments
    /// </summary>
    public class InstallationProgressArgs : EventArgs
    {
        public string Serial { get; }
        public int CurrentDevice { get; }
        public int TotalDevices { get; }
        public int CurrentUnit { get; }
        public int TotalUnits { get; }
        public string CurrentOperation { get; }
        public double OverallProgress { get; }
        
        public InstallationProgressArgs(string serial, int currentDevice, int totalDevices, 
            int currentUnit, int totalUnits, string currentOperation, double overallProgress)
        {
            Serial = serial;
            CurrentDevice = currentDevice;
            TotalDevices = totalDevices;
            CurrentUnit = currentUnit;
            TotalUnits = totalUnits;
            CurrentOperation = currentOperation;
            OverallProgress = overallProgress;
        }
    }
}
