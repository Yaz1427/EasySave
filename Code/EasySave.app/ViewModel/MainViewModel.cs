using System;
using System.Collections.Generic;
using System.Linq;
using EasySave.Services;
using EasySave.Models;
using EasyLog.Models;

namespace EasySave.ViewModel
{
    /// <summary>
    /// Chef d'orchestre : lie la vue aux services de sauvegarde et de configuration.
    /// </summary>
    public class MainViewModel
    {
        // Attributs privés conformes au diagramme UML
        private List<BackupJob> _jobs;
        private BackupService _backupService;
        private ConfigService _configService;

        /// <summary>
        /// Constructeur : Initialise les services et charge les travaux existants.
        /// </summary>
        public MainViewModel()
        {
            // On instancie les services (développés par tes collègues)
            _configService = new ConfigService();

            // Charger le format de log depuis les parametres
            LogFormat logFormat = _configService.LoadLogFormat();
            _backupService = new BackupService(logFormat);

            // On charge la liste des 5 travaux au démarrage
            _jobs = _configService.LoadJobs();
        }

        /// <summary>
        /// Exécute un travail de sauvegarde spécifique par son index (0 à 4).
        /// </summary>
        public void ExecuteJob(int index)
        {
            // Vérification de sécurité sur l'index (5 jobs max pour la V1)
            if (index >= 0 && index < _jobs.Count)
            {
                BackupJob jobToRun = _jobs[index];

                // Appel du service de Deshani pour faire la copie réelle
                _backupService.ExecuteBackup(jobToRun);
            }
            else
            {
                throw new IndexOutOfRangeException("L'index du travail de sauvegarde est invalide.");
            }
        }

        /// <summary>
        /// Exécute séquentiellement tous les travaux de sauvegarde configurés.
        /// </summary>
        public void ExecuteAllJobs()
        {
            foreach (var job in _jobs)
            {
                _backupService.ExecuteBackup(job);
            }
        }

        /// <summary>
        /// Crée un nouveau travail de sauvegarde et l'ajoute à la liste
        /// </summary>
        public bool CreateJob(string name, string sourceDir, string targetDir, JobType type)
        {
            // Vérifications de base
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
                return false;

            // Vérifier qu'on ne dépasse pas 5 jobs
            if (_jobs.Count >= 5)
                return false;

            // Créer le nouveau job
            var newJob = new BackupJob
            {
                Name = name,
                SourceDir = sourceDir,
                TargetDir = targetDir,
                Type = type
            };

            // Ajouter à la liste
            _jobs.Add(newJob);

            // Sauvegarder dans le fichier
            _configService.SaveJobs(_jobs);

            return true;
        }

        /// <summary>
        /// Supprime un travail de sauvegarde par son index
        /// </summary>
        public bool DeleteJob(int index)
        {
            if (index >= 0 && index < _jobs.Count)
            {
                _jobs.RemoveAt(index);
                _configService.SaveJobs(_jobs);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Retourne la liste de tous les travaux de sauvegarde
        /// </summary>
        public List<BackupJob> GetAllJobs()
        {
            return _jobs.ToList();
        }

        /// <summary>
        /// Change le format du fichier log (JSON ou XML) et persiste le choix
        /// </summary>
        public void SetLogFormat(LogFormat format)
        {
            _backupService.SetLogFormat(format);
            _configService.SaveLogFormat(format);
        }

        /// <summary>
        /// Retourne le format de log actuel
        /// </summary>
        public LogFormat GetLogFormat()
        {
            return _backupService.GetLogFormat();
        }

        // Propriété pour que la vue puisse éventuellement lister les noms des jobs
        public List<BackupJob> Jobs => _jobs;
    }
}
