
using System;
using System.Xml.Serialization;

namespace EasyLog.Models
{
	public class LogEntry
	{
		public DateTime Timestamp { get; set; }     // Horodatage
		public string JobName { get; set; }         // Nom de la sauvegarde
		public string SourcePath { get; set; }      // Adresse source (UNC)
		public string TargetPath { get; set; }      // Adresse destination (UNC)
		public long FileSize { get; set; }          // Taille du fichier en octets
		public int TransferTime { get; set; }       // Temps de transfert en ms (négatif si erreur)
		public int CryptTime { get; set; }          // Temps de cryptage en ms
	}
}