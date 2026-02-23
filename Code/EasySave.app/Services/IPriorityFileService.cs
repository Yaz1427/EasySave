namespace EasySave.Services
{
    /// <summary>
    /// Interface définissant le contrat pour la gestion des fichiers prioritaires.
    /// Permet de vérifier si un fichier est prioritaire selon son extension.
    /// </summary>
    public interface IPriorityFileService
    {
        /// <summary>
        /// Vérifie si un fichier est prioritaire.
        /// </summary>
        bool IsPriority(string filePath);
    }
}
