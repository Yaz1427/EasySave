using System;
using System.Collections.Generic;
using EasySave.App.Models;
using EasySave.App.Services;

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
            _backupService = new BackupService();

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

        /// <summary
        /// Exécute séquentiellement tous les travaux de sauvegarde configurés.
        /// </summary>
        public void ExecuteAllJobs()
        {
            foreach (var job in _jobs)
            {
                _backupService.ExecuteBackup(job);
            }
        }

        // Propriété pour que la vue puisse éventuellement lister les noms des jobs
        public List<BackupJob> Jobs => _jobs;
    }
}


