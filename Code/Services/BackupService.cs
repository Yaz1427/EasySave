using EasySave.app.Model;

namespace EasySave.Services
{
	public class BackupService
	{
		// Dépendances (seront injectées plus tard)
		// private readonly DLL _logger;

		public BackupService()
		{
		}

		// Méthode principale : exécutera une sauvegarde
		public void ExecuteBackup(BackupJob job)
		{
			// TODO :
			// - Parcourir les fichiers du dossier source
			// - Appliquer la logique Full / Differential
			// - Copier les fichiers
			// - Appeler le logger (via la DLL)
			// - Mettre à jour RealTimeState
		}

		// Règle de sauvegarde différentielle
		public bool CheckDifferential(string file)
		{
			// TODO :
			// - Comparer le fichier source et cible
			// - Retourner true si le fichier doit être copié
			return false;
		}
	}
}
