namespace EasySave.Services
{
    /// <summary>
    /// Service responsable de la gestion des fichiers prioritaires.
    /// Il compare les extensions des fichiers avec la liste définie dans les paramètres.
    /// </summary>
    public class PriorityFileService : IPriorityFileService
    {
        private readonly List<string> _priorityExtensions;

        public PriorityFileService(List<string> priorityExtensions)
        {
            _priorityExtensions = priorityExtensions
                .Select(e => e.ToLower())
                .ToList();
        }

        public bool IsPriority(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return _priorityExtensions.Contains(extension);
        }
    }
}
