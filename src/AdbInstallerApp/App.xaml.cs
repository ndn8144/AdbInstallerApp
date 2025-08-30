using System.Windows;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdbInstallerApp.Services;
using AdbInstallerApp.ViewModels;
using AdbInstallerApp.Views;


namespace AdbInstallerApp
{
    public partial class App : Application
    {
        private IHost? _host;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                MessageBox.Show("Starting application...", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Configure dependency injection
                _host = CreateHostBuilder().Build();
                MessageBox.Show("DI container built successfully", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Get services from DI container
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MessageBox.Show("MainWindow created successfully", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                
                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
                MessageBox.Show("MainViewModel created successfully", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                
                mainWindow.DataContext = mainViewModel;
                mainWindow.Show();
                
                MessageBox.Show("Application started successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }
        
        private IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register core services
                    services.AddSingleton<ILogBus, LogBus>();
                    services.AddSingleton<IGlobalStatusService, GlobalStatusService>();
                    services.AddSingleton<AdbService>();
                    services.AddSingleton<OptimizedProgressService>();
                    
                    // Register additional services that MainViewModel needs
                    services.AddSingleton<DeviceMonitor>();
                    services.AddSingleton<ApkRepoIndexer>();
                    services.AddSingleton<ApkValidationService>();
                    services.AddSingleton<InstallOrchestrator>();
                    services.AddSingleton<IApkAnalyzerService, ApkAnalyzerService>();
                    
                    // EnhancedInstallQueue needs InstallOrchestrator, so register it after
                    services.AddSingleton<EnhancedInstallQueue>(provider =>
                    {
                        var installer = provider.GetRequiredService<InstallOrchestrator>();
                        return new EnhancedInstallQueue(installer, maxConcurrentOperations: 2);
                    });
                    
                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<LogViewerViewModel>();
                    
                    // Register Views
                    services.AddTransient<MainWindow>();
                });
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}