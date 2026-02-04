// LoggerService.cs

using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using EasyLog.Models;

namespace EasyLog.Services
{
	public class LoggerService
	{
		private readonly string _logDirectory;

		public LoggerService()
		{
			// Emplacement : Éviter C:\temp. On utilise le dossier de l'app ou AppData
			_logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

			if (!Directory.Exists(_logDirectory))
			{
				Directory.CreateDirectory(_logDirectory);
			}
		}

		public void SaveLog(LogEntry entry)
		{
			// Nom du fichier journalier : YYYY-MM-DD.json
			string fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
			string filePath = Path.Combine(_logDirectory, fileName);

			List<LogEntry> logs;

			// Lire l'existant pour ajouter à la suite (si le fichier existe déjà)
			if (File.Exists(filePath))
			{
				string existingJson = File.ReadAllText(filePath);
				logs = JsonSerializer.Deserialize<List<LogEntry>>(existingJson) ?? new List<LogEntry>();
			}
			else
			{
				logs = new List<LogEntry>();
			}

			logs.Add(entry);

			// Sérialisation avec indentation (pagination/lecture Notepad aisée)
			var options = new JsonSerializerOptions { WriteIndented = true };
			string jsonString = JsonSerializer.Serialize(logs, options);

			File.WriteAllText(filePath, jsonString);
		}
	}
}