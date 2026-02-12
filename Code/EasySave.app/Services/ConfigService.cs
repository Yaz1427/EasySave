using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.Models;
using EasyLog.Models;

namespace EasySave.Services
{
    public class ConfigService
    {
        private const string JobsFileName = "jobs.json";
        private const string SettingsFileName = "settings.json";

        private static readonly object _lock = new object();

        private readonly string _jobsFilePath;
        private readonly string _settingsFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigService(string? configDirectory = null)
        {
            string configDir = string.IsNullOrWhiteSpace(configDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave")
                : configDirectory;

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            _jobsFilePath = Path.Combine(configDir, JobsFileName);
            _settingsFilePath = Path.Combine(configDir, SettingsFileName);

            _settingsFilePath = Path.Combine(
                Path.GetDirectoryName(_jobsFilePath) ?? AppContext.BaseDirectory,
                SettingsFileName);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        // --- Jobs ---

        public List<BackupJob> LoadJobs()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_jobsFilePath))
                        return new List<BackupJob>();

                    var json = File.ReadAllText(_jobsFilePath);

                    if (string.IsNullOrWhiteSpace(json))
                        return new List<BackupJob>();

                    var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json, _jsonOptions)
                               ?? new List<BackupJob>();

                    // v2.0: unlimited jobs, just filter nulls and invalid
                    jobs = jobs.Where(j => j != null).Where(IsValidJob).ToList();
                    return jobs;
                }
                catch
                {
                    return new List<BackupJob>();
                }
            }
        }

        public void SaveJobs(List<BackupJob> jobs)
        {
            if (jobs == null) throw new ArgumentNullException(nameof(jobs));

            lock (_lock)
            {
                var cleaned = jobs
                    .Where(j => j != null)
                    .Where(IsValidJob)
                    .ToList();

                var directory = Path.GetDirectoryName(_jobsFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(cleaned, _jsonOptions);

                var tempPath = _jobsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Copy(tempPath, _jobsFilePath, overwrite: true);
                File.Delete(tempPath);
            }
        }

        /// <summary>
        /// Charge le format de log depuis settings.json (JSON par defaut)
        /// </summary>
        public LogFormat LoadLogFormat()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_settingsFilePath))
                        return LogFormat.JSON;

                    var json = File.ReadAllText(_settingsFilePath);
                    if (string.IsNullOrWhiteSpace(json))
                        return LogFormat.JSON;

                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (settings != null && Enum.IsDefined(typeof(LogFormat), settings.LogFormat))
                        return settings.LogFormat;

                    return LogFormat.JSON;
                }
                catch
                {
                    return LogFormat.JSON;
                }
            }
        }

        // --- Settings ---

        public AppSettings LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_settingsFilePath))
                        return new AppSettings();

                    var json = File.ReadAllText(_settingsFilePath);
                    if (string.IsNullOrWhiteSpace(json))
                        return new AppSettings();

                    return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (_lock)
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
        }

        // --- Helpers ---

        private static bool IsValidJob(BackupJob job)
        {
            if (job == null) return false;
            if (string.IsNullOrWhiteSpace(job.Name)) return false;
            if (string.IsNullOrWhiteSpace(job.SourceDir)) return false;
            if (string.IsNullOrWhiteSpace(job.TargetDir)) return false;
            return true;
        }

        /// <summary>
        /// Classe interne pour les parametres de l'application
        /// </summary>
        private class AppSettings
        {
            public LogFormat LogFormat { get; set; } = LogFormat.JSON;
        }
    }
}
