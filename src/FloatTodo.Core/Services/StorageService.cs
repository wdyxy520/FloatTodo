using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using FloatTodo.Core.Models;

namespace FloatTodo.Core.Services;

public sealed class StorageService : IDisposable
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private AppSettings? _pendingSettings;

    public StorageService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsPath))
        {
            _settingsFilePath = customSettingsPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "FloatTodo");
            _settingsFilePath = Path.Combine(dir, "settings.json");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }
    }

    public void SaveSettingsDebounced(AppSettings settings, int delayMs = 300)
    {
        lock (_lock)
        {
            _pendingSettings = settings;

            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(OnDebounceTimerFired, null, delayMs, Timeout.Infinite);
            }
            else
            {
                _debounceTimer.Change(delayMs, Timeout.Infinite);
            }
        }
    }

    private void OnDebounceTimerFired(object? state)
    {
        AppSettings? settingsToSave = null;
        lock (_lock)
        {
            settingsToSave = _pendingSettings;
            _pendingSettings = null;
        }

        if (settingsToSave != null)
        {
            SaveSettingsImmediately(settingsToSave);
        }
    }

    public void SaveSettingsImmediately(AppSettings settings)
    {
        lock (_lock)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                string tempFile = _settingsFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _settingsFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to save settings immediately: {ex.Message}");
            }
        }
    }

    private static void DebugLog(string msg)
    {
        System.Diagnostics.Debug.WriteLine($"[StorageService] {msg}");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            if (_pendingSettings != null)
            {
                SaveSettingsImmediately(_pendingSettings);
                _pendingSettings = null;
            }
        }
    }
}
