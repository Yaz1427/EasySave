//ConfigService.cs

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
        private const int MaxJobs = 5; //Maximum 5 travaux
        private const string JobsFileName = "jobs.json"; //Nom du fichier
        private const string SettingsFileName = "settings.json"; //Nom du fichier de parametres

        // Verrouillage pour eviter 2 ecritures/lectures simultanees
        private static readonly object _lock = new object();

        private readonly string _jobsFilePath; //Chemin du fichier
        private readonly string _settingsFilePath; //Chemin du fichier de parametres
        private readonly JsonSerializerOptions _jsonOptions; //Options de serialisation JSON

        public ConfigService(string? jobsFilePath = null)
        {
            // Par defaut : jobs.json dans le dossier ou l'appli est lancee
            _jobsFilePath = string.IsNullOrWhiteSpace(jobsFilePath)
                ? Path.Combine(AppContext.BaseDirectory, JobsFileName)
                : jobsFilePath;

            _settingsFilePath = Path.Combine(
                Path.GetDirectoryName(_jobsFilePath) ?? AppContext.BaseDirectory,
                SettingsFileName);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public List<BackupJob> LoadJobs() // Lit jobs.json et retourne la liste des jobs
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_jobsFilePath))
                        return new List<BackupJob>();

                    var json = File.ReadAllText(_jobsFilePath); //Charge le fichier en memoire

                    if (string.IsNullOrWhiteSpace(json))
                        return new List<BackupJob>();

                    var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json, _jsonOptions)
                               ?? new List<BackupJob>();

                    // max 5, supprime les jobs null
                    jobs = jobs.Where(j => j != null).Take(MaxJobs).ToList();

                    // Optionnel : enlever les jobs "vides" (ex: nom vide)
                    jobs = jobs.Where(IsValidJob).ToList();

                    return jobs;
                }
                catch // quoi faire si sa se passe mal
                {
                    // En cas de JSON corrompu ou autre : on revient a une liste vide
                    return new List<BackupJob>();
                }
            }
        }

        /// Sauvegarde des jobs dans jobs.json 
        public void SaveJobs(List<BackupJob> jobs)
        {
            if (jobs == null) throw new ArgumentNullException(nameof(jobs));

            lock (_lock)
            {
                // Securite : max 5 + pas de null + jobs valides
                var cleaned = jobs
                    .Where(j => j != null)
                    .Where(IsValidJob)
                    .Take(MaxJobs)
                    .ToList();

                var directory = Path.GetDirectoryName(_jobsFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(cleaned, _jsonOptions);

                // Ecriture atomique
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

        /// <summary>
        /// Sauvegarde le format de log dans settings.json
        /// </summary>
        public void SaveLogFormat(LogFormat format)
        {
            lock (_lock)
            {
                var settings = new AppSettings { LogFormat = format };

                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private static bool IsValidJob(BackupJob job) // Verifie si un job est valide
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
