using System;

namespace EasySave.App.Models
{
	/// <summary>
	/// Structure pour le fichier "état temps réel" (state.json).
	/// </summary>
	public class RealTimeState
	{
		// Nom du travail en cours
		public string JobName { get; set; } = string.Empty;

		// Horodatage de la dernière action
		public DateTime LastActionTimestamp { get; set; }

		// État : "Active", "Inactive", "End", etc.
		public string Status { get; set; } = "Inactive";

		// Nombre total de fichiers à copier
		public int TotalFilesToCopy { get; set; }

		// Taille totale en octets
		public long TotalFilesSize { get; set; }

		// Progression en pourcentage
		public double Progress { get; set; }

		// Nombre de fichiers restants à transférer
		public int RemainingFiles { get; set; }

		// Taille des fichiers restants
		public long RemainingSize { get; set; }

		// Source et Destination du fichier actuellement traité
		public string CurrentSourceFilePath { get; set; } = string.Empty;
		public string CurrentTargetFilePath { get; set; } = string.Empty;
	}
}