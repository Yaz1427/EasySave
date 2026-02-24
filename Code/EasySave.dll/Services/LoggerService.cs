using System;
using System.IO;
using System.Net.Http;
using System.Text;
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

    // v3.0: Log destination options
    public enum LogDestination
    {
        Local,
        Centralized,
        Both
    }

    public class LoggerService
    {
        private readonly string _logDirectory;
        private LogFormat _format;
        private static readonly object _fileLock = new object();

        // v3.0: Log centralization
        private LogDestination _destination = LogDestination.Local;
        private string _logServerUrl = string.Empty;
        private string _machineName = Environment.MachineName;
        private string _userName = Environment.UserName;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

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

        public LogDestination Destination
        {
            get => _destination;
            set => _destination = value;
        }

        public string LogServerUrl
        {
            get => _logServerUrl;
            set => _logServerUrl = value ?? string.Empty;
        }

        public void SaveLog(LogEntry entry)
        {
            bool saveLocal = _destination == LogDestination.Local || _destination == LogDestination.Both;
            bool sendRemote = (_destination == LogDestination.Centralized || _destination == LogDestination.Both)
                              && !string.IsNullOrWhiteSpace(_logServerUrl);

            if (saveLocal)
            {
                lock (_fileLock)
                {
                    if (_format == LogFormat.XML)
                        SaveLogXml(entry);
                    else
                        SaveLogJson(entry);
                }
            }

            if (sendRemote)
            {
                SendLogToServer(entry);
            }
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

            // Atomic write using temp file
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, jsonString);
            File.Copy(tempPath, filePath, overwrite: true);
            File.Delete(tempPath);
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

            // Atomic write using temp file
            string tempPath = filePath + ".tmp";
            using (var writer = XmlWriter.Create(tempPath, settings))
            {
                xmlSerializer.Serialize(writer, logs);
            }
            File.Copy(tempPath, filePath, overwrite: true);
            File.Delete(tempPath);
        }

        // v3.0: Send log entry to the centralized Docker log server
        private void SendLogToServer(LogEntry entry)
        {
            try
            {
                var payload = new
                {
                    MachineName = _machineName,
                    UserName = _userName,
                    entry.Timestamp,
                    entry.JobName,
                    entry.SourcePath,
                    entry.TargetPath,
                    entry.FileSize,
                    entry.TransferTime,
                    entry.EncryptionTime
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = _logServerUrl.TrimEnd('/') + "/api/logs";
                var response = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();
            }
            catch
            {
                // If server is unreachable, silently fail to avoid blocking backups
            }
        }
    }
}
