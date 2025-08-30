// Temporary test file - delete after validation
using AdbInstallerApp.Services;
using AdbInstallerApp.Helpers;

namespace AdbInstallerApp.Tests;

internal static class InfrastructureTests
{
    public static async Task TestProcAsync()
    {
        Console.WriteLine("Testing Proc.RunAsync...");
        
        // Test basic command
        var result = await Proc.RunAsync("cmd", "/c echo Hello World");
        Console.WriteLine($"Exit Code: {result.ExitCode}");
        Console.WriteLine($"Output: {result.StdOut}");
        
        // Test with progress
        var progress = new Progress<string>(msg => Console.WriteLine($"PROGRESS: {msg}"));
        var result2 = await Proc.RunAsync("cmd", "/c dir", null, progress);
        Console.WriteLine($"Dir command completed: {result2.ExitCode == 0}");
    }
    
    public static void TestLogBus()
    {
        Console.WriteLine("Testing LogBus...");
        
        using var logBus = new LogBus();
        
        // Subscribe to stream - simplified for testing
        Console.WriteLine("LogBus stream subscription test skipped (requires System.Reactive)");
        
        // Send test messages
        logBus.Write("Test info message");
        logBus.WriteWarning("Test warning message");
        logBus.WriteError("Test error message");
        
        // Wait for processing
        Thread.Sleep(200);
        
        Console.WriteLine("LogBus test completed");
    }
    
    public static void TestGlobalStatus()
    {
        Console.WriteLine("Testing GlobalStatusService...");
        
        var statusService = new GlobalStatusService();
        
        Console.WriteLine($"Initial: {statusService.StatusText} (Busy: {statusService.IsBusy})");
        
        using (var scope1 = statusService.Push("Installing APK..."))
        {
            Console.WriteLine($"Push 1: {statusService.StatusText} (Busy: {statusService.IsBusy})");
            
            using (var scope2 = statusService.Push("Writing files..."))
            {
                Console.WriteLine($"Push 2: {statusService.StatusText} (Busy: {statusService.IsBusy})");
            }
            
            Console.WriteLine($"After Pop 2: {statusService.StatusText} (Busy: {statusService.IsBusy})");
        }
        
        Console.WriteLine($"After Pop 1: {statusService.StatusText} (Busy: {statusService.IsBusy})");
        
        statusService.UpdateIdleStatus(3, 5);
        Console.WriteLine($"Idle Update: {statusService.StatusText}");
    }
}