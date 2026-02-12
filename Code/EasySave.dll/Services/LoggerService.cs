using System;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using EasyLog.Models;

namespace EasyLog.Services
{
    public enum LogFormat
    {
        JSON,
        XML
    }

    public class LoggerService
    {
        private readonly string _logDirectory;
        private LogFormat _format;

        public LoggerService(string? logDirectory = null, LogFormat format = LogFormat.JSON)
        {
            _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "Logs")
                : logDirectory;

            _format = format;

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public LogFormat Format
        {
            get => _format;
            set => _format = value;
        }

        public void SaveLog(LogEntry entry)
        {
            if (_format == LogFormat.XML)
                SaveLogXml(entry);
            else
                SaveLogJson(entry);
        }

        private void SaveLogJson(LogEntry entry)
        {
            string fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
            string filePath = Path.Combine(_logDirectory, fileName);

            List<LogEntry> logs;

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

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(logs, options);
            File.WriteAllText(filePath, jsonString);
        }

        private void SaveLogXml(LogEntry entry)
        {
            string fileName = $"{DateTime.Now:yyyy-MM-dd}.xml";
            string filePath = Path.Combine(_logDirectory, fileName);

            List<LogEntry> logs;

            if (File.Exists(filePath))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(List<LogEntry>));
                    using var reader = new StreamReader(filePath);
                    logs = (List<LogEntry>?)serializer.Deserialize(reader) ?? new List<LogEntry>();
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

            var xmlSerializer = new XmlSerializer(typeof(List<LogEntry>));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = false
            };

            using var writer = XmlWriter.Create(filePath, settings);
            xmlSerializer.Serialize(writer, logs);
        }
    }
}
