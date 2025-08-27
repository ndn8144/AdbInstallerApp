using System.Windows;
using System;


namespace AdbInstallerApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}