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

public LoggerService(string logDirectory = null)
{
_logDirectory = string.IsNullOrWhiteSpace(logDirectory)
? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")
: logDirectory;

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

// Lire l'existant pour ajouter a la suite (si le fichier existe deja)
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

// Serialisation avec indentation (pagination/lecture Notepad aisee)
var options = new JsonSerializerOptions { WriteIndented = true };
string jsonString = JsonSerializer.Serialize(logs, options);

File.WriteAllText(filePath, jsonString);
}
}
}