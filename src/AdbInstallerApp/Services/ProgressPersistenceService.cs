using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Service for persisting progress data across app restarts
    /// </summary>
    public class ProgressPersistenceService
    {
        private readonly string _persistenceFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        
        public ProgressPersistenceService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "AdbInstallerApp");
            Directory.CreateDirectory(appFolder);
            
            _persistenceFilePath = Path.Combine(appFolder, "progress_state.json");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
        
        /// <summary>
        /// Save current progress state to disk
        /// </summary>
        public async Task SaveProgressStateAsync(ProgressState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                await File.WriteAllTextAsync(_persistenceFilePath, json);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - persistence is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to save progress state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load progress state from disk
        /// </summary>
        public async Task<ProgressState?> LoadProgressStateAsync()
        {
            try
            {
                if (!File.Exists(_persistenceFilePath))
                    return null;
                    
                var json = await File.ReadAllTextAsync(_persistenceFilePath);
                return JsonSerializer.Deserialize<ProgressState>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - return null to indicate no saved state
                System.Diagnostics.Debug.WriteLine($"Failed to load progress state: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Clear saved progress state
        /// </summary>
        public void ClearProgressState()
        {
            try
            {
                if (File.Exists(_persistenceFilePath))
                {
                    File.Delete(_persistenceFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear progress state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if there's a saved progress state
        /// </summary>
        public bool HasSavedState()
        {
            return File.Exists(_persistenceFilePath);
        }
    }
    
    /// <summary>
    /// Represents the progress state that can be persisted
    /// </summary>
    public class ProgressState
    {
        public List<PersistedOperation> ActiveOperations { get; set; } = new();
        public List<PersistedOperation> QueuedOperations { get; set; } = new();
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public string AppVersion { get; set; } = "1.0.0";
    }
    
    /// <summary>
    /// Represents an operation that can be persisted
    /// </summary>
    public class PersistedOperation
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public double Progress { get; set; }
        public string Message { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EstimatedCompletion { get; set; }
        public string OperationType { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        // Installation-specific data
        public List<string> DeviceSerials { get; set; } = new();
        public List<string> ApkPaths { get; set; } = new();
        public List<string> GroupNames { get; set; } = new();
        public bool Reinstall { get; set; }
        public bool GrantPermissions { get; set; }
        public bool AllowDowngrade { get; set; }
    }
}
