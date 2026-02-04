namespace EasySave.Models
{
    /// <summary>
    /// Type de sauvegarde autorisé par ProSoft.
    /// </summary>
    public enum JobType
    {
        Full,         // Sauvegarde complète
        Differential  // Sauvegarde différentielle
    }

    /// <summary>
    /// Représente un travail de sauvegarde (un des 5 slots).
    /// </summary>
    public class BackupJob
    {
        // Nom de la sauvegarde
        public string Name { get; set; } = string.Empty;

        // Répertoire source (format UNC ou local)
        public string SourceDir { get; set; } = string.Empty;

        // Répertoire cible
        public string TargetDir { get; set; } = string.Empty;

        // Type : complète ou différentielle
        public JobType Type { get; set; }
    }
}
