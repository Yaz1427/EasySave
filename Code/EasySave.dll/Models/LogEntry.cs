// LogEntry.cs

using System;

namespace EasyLog.Models
{
	public class LogEntry
	{
		public DateTime Timestamp { get; set; } // Horodatage
		public string JobName { get; set; }     // Nom de la sauvegarde
		public string SourcePath { get; set; }  // Adresse source (UNC)
		public string TargetPath { get; set; }  // Adresse destination (UNC)
		public long FileSize { get; set; }      // Taille du fichier
		public int TransferTime { get; set; }   // Temps de transfert en ms (négatif si erreur)
        public int CryptoTimeMs { get; set; }  // Temps de cryptage en ms : 0 pas de cryptage, >0 temps, <0 erreur

    }
}