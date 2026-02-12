using System;

namespace EasyLog.Models
{
<<<<<<< HEAD
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
=======
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TransferTime { get; set; }
        // v2.0: Encryption time in ms (0 = no encryption, >0 = time, <0 = error code)
        public int EncryptionTime { get; set; }
    }
}
>>>>>>> origin/main
