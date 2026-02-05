using System;

namespace EasySave.Models
{
    /// <summary>
    /// Structure pour le fichier "état temps réel" (state.json).
    /// Conforme aux spécifications client
    /// </summary>
    public class RealTimeState
    {
        // Appellation du travail de sauvegarde
        public string JobName { get; set; } = string.Empty;

        // Horodatage de la dernière action
        public DateTime LastActionTimestamp { get; set; }

        // État du travail de Sauvegarde (Actif, Non Actif, End, etc.)
        public string Status { get; set; } = "Inactive";

        // Si le travail est actif :
        // Le nombre total de fichiers éligibles
        public int TotalFilesToCopy { get; set; }

        // La taille des fichiers à transférer
        public long TotalFilesSize { get; set; }

        // La progression (en pourcentage)
        public double Progress { get; set; }

        // Nombre de fichiers restants
        public int RemainingFiles { get; set; }

        // Taille des fichiers restants
        public long RemainingSize { get; set; }

        // Adresse complète du fichier Source en cours de sauvegarde
        public string CurrentSourceFile { get; set; } = string.Empty;

        // Adresse complète du fichier de destination
        public string CurrentTargetFile { get; set; } = string.Empty;
    }
}
