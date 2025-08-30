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
        
        Console.WriteLine($"Initial: HasActiveStatus: {statusService.HasActiveStatus}, StackDepth: {statusService.StatusStackDepth}");
        
        using (var scope1 = statusService.CreateStatusScope("Installing APK...", StatusType.Info))
        {
            Console.WriteLine($"Push 1: HasActiveStatus: {statusService.HasActiveStatus}, StackDepth: {statusService.StatusStackDepth}");
            
            using (var scope2 = statusService.CreateStatusScope("Writing files...", StatusType.Progress))
            {
                Console.WriteLine($"Push 2: HasActiveStatus: {statusService.HasActiveStatus}, StackDepth: {statusService.StatusStackDepth}");
                
                statusService.UpdateProgress("Writing files...", 50.0);
                Console.WriteLine($"Progress updated to 50%");
            }
            
            Console.WriteLine($"After Pop 2: HasActiveStatus: {statusService.HasActiveStatus}, StackDepth: {statusService.StatusStackDepth}");
        }
        
        Console.WriteLine($"After Pop 1: HasActiveStatus: {statusService.HasActiveStatus}, StackDepth: {statusService.StatusStackDepth}");
        
        statusService.SetSuccess("Operation completed successfully");
        Console.WriteLine($"Success status set");
        
        statusService.Dispose();
    }
}