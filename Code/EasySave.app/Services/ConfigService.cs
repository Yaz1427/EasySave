using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.App.Models;

namespace EasySave.App.Services
{
    public class ConfigService
    {
        private const int MaxJobs = 5; //Maximum 5 travaux
        private const string JobsFileName = "jobs.json"; //Nom du fichier

        // Verrouillage pour éviter 2 écritures/lectures simultanées
        private static readonly object _lock = new object();

        private readonly string _jobsFilePath; //Chemin du fichier
        private readonly JsonSerializerOptions _jsonOptions; //Options de sérialisation JSON

        public ConfigService(string? jobsFilePath = null)
        {
            // Par défaut : jobs.json dans le dossier où l'appli est lancée
            _jobsFilePath = string.IsNullOrWhiteSpace(jobsFilePath)
                ? Path.Combine(AppContext.BaseDirectory, JobsFileName)
                : jobsFilePath;

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

                    var json = File.ReadAllText(_jobsFilePath); //Charge le fichier en mémoire

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
                    // En cas de JSON corrompu ou autre : on revient à une liste vide
                    // (Tu peux logguer l'erreur via EasyLog si tu veux)
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
                // Sécurité : max 5 + pas de null + jobs valides
                var cleaned = jobs
                    .Where(j => j != null)
                    .Where(IsValidJob)
                    .Take(MaxJobs)
                    .ToList();

                var directory = Path.GetDirectoryName(_jobsFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(cleaned, _jsonOptions);

                // Écriture atomique; soit l'écriture réussi completement soit rien n'est modifié (évite de casser le fichier si crash pendant écriture)
                var tempPath = _jobsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Copy(tempPath, _jobsFilePath, overwrite: true);
                File.Delete(tempPath);
            }
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private static bool IsValidJob(BackupJob job) // Vérifie si un job est valide
        {
            if (job == null) return false;

            if (string.IsNullOrWhiteSpace(job.Name)) return false;
            if (string.IsNullOrWhiteSpace(job.SourceDir)) return false;
            if (string.IsNullOrWhiteSpace(job.TargetDir)) return false;

            //évite les sauvegardes vers le même répertoire
            return true;
        }

        