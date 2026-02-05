using System;
using System.IO;
using System.Text.Json;
using EasySave.Models;

namespace EasySave.Services
{
    /// <summary>
    /// Service pour gérer l'état temps réel des sauvegardes
    /// </summary>
    public class RealTimeStateService
    {
        private readonly string _stateFilePath;
        private readonly object _lock = new object();

        public RealTimeStateService()
        {
            // Emplacement personnalisé : EasySave\LogsEasySave\state.json
            _stateFilePath = Path.Combine(@"C:\Users\tilal\Documents\CESI\TROISIEME ANNEE\Module Génie Logiciel\EasySave", "LogsEasySave", "state.json");
        }

        /// <summary>
        /// Met à jour l'état temps réel
        /// </summary>
        public void UpdateState(RealTimeState state)
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true // Pour la lisibilité dans Notepad
                };
                
                string jsonString = JsonSerializer.Serialize(state, options);
                File.WriteAllText(_stateFilePath, jsonString);
            }
        }

        /// <summary>
        /// Récupère l'état temps réel actuel
        /// </summary>
        public RealTimeState GetCurrentState()
        {
            lock (_lock)
            {
                if (!File.Exists(_stateFilePath))
                {
                    return new RealTimeState 
                    { 
                        Status = "Inactive",
                        LastActionTimestamp = DateTime.Now
                    };
                }

                try
                {
                    string json = File.ReadAllText(_stateFilePath);
                    return JsonSerializer.Deserialize<RealTimeState>(json) ?? new RealTimeState();
                }
                catch
                {
                    return new RealTimeState 
                    { 
                        Status = "Inactive",
                        LastActionTimestamp = DateTime.Now
                    };
                }
            }
        }

        /// <summary>
        /// Crée un état initial pour un job
        /// </summary>
        public RealTimeState CreateInitialState(BackupJob job)
        {
            return new RealTimeState
            {
                JobName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now,
                TotalFilesToCopy = 0, // Sera calculé pendant l'exécution
                TotalFilesSize = 0,    // Sera calculé pendant l'exécution
                Progress = 0,
                RemainingFiles = 0,
                RemainingSize = 0,
                CurrentSourceFile = "",
                CurrentTargetFile = ""
            };
        }
    }
}
