// LoggerService.cs

using System;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using EasyLog.Models;

namespace EasyLog.Services
{
public class LoggerService
{
private readonly string _logDirectory;
private LogFormat _logFormat;

public LoggerService(string logDirectory = null, LogFormat logFormat = LogFormat.JSON)
{
_logDirectory = string.IsNullOrWhiteSpace(logDirectory)
? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")
: logDirectory;

_logFormat = logFormat;

if (!Directory.Exists(_logDirectory))
{
Directory.CreateDirectory(_logDirectory);
}
}

public void SetLogFormat(LogFormat format)
{
_logFormat = format;
}

public LogFormat GetLogFormat()
{
return _logFormat;
}

public void SaveLog(LogEntry entry)
{
if (_logFormat == LogFormat.XML)
{
SaveLogXml(entry);
}
else
{
SaveLogJson(entry);
}
}

private void SaveLogJson(LogEntry entry)
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

private void SaveLogXml(LogEntry entry)
{
// Nom du fichier journalier : YYYY-MM-DD.xml
string fileName = $"{DateTime.Now:yyyy-MM-dd}.xml";
string filePath = Path.Combine(_logDirectory, fileName);

List<LogEntry> logs;

// Lire l'existant pour ajouter a la suite (si le fichier existe deja)
if (File.Exists(filePath))
{
try
{
var serializer = new XmlSerializer(typeof(List<LogEntry>));
using (var reader = new StreamReader(filePath))
{
logs = (List<LogEntry>)serializer.Deserialize(reader) ?? new List<LogEntry>();
}
}
catch
{
logs = new List<LogEntry>();
}
}
else
{
logs = new List<LogEntry>();
}

logs.Add(entry);

// Serialisation XML avec indentation
var xmlSerializer = new XmlSerializer(typeof(List<LogEntry>));
var settings = new XmlWriterSettings { Indent = true, IndentChars = "  " };

using (var writer = XmlWriter.Create(filePath, settings))
{
xmlSerializer.Serialize(writer, logs);
}
}
}
}