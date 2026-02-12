<<<<<<< HEAD
﻿// LoggerService.cs
// Version 1.1 / 2.0 - Support for JSON and XML log formats

using System;
using System.IO;
using System.Text.Json;
=======
using System;
using System.IO;
using System.Text.Json;
using System.Xml;
>>>>>>> origin/main
using System.Xml.Serialization;
using System.Collections.Generic;
using EasyLog.Models;

namespace EasyLog.Services 
{
<<<<<<< HEAD
	public class LoggerService
	{
		private readonly string _logDirectory;
		private LogFormat _logFormat;

		
		/// <param name="logDirectory">Directory where log files will be saved (default: Logs folder in app directory)</param>
		/// <param name="logFormat">Log file format (JSON or XML, default: JSON)</param>
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

		
		/// </summary>
		/// <param name="format">New log format</param>
		public void SetLogFormat(LogFormat format)
		{
			_logFormat = format;
		}
		public LogFormat GetLogFormat()
		{
			return _logFormat;
		}

	
		/// <param name="entry">Log entry to save</param>
		public void SaveLog(LogEntry entry)
		{
			switch (_logFormat)
			{
				case LogFormat.JSON:
					SaveLogAsJson(entry);
					break;
				case LogFormat.XML:
					SaveLogAsXml(entry);
					break;
				default:
					throw new NotSupportedException($"Log format {_logFormat} is not supported");
			}
		}

		private void SaveLogAsJson(LogEntry entry)
		{
			string fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
			string filePath = Path.Combine(_logDirectory, fileName);

			List<LogEntry> logs;

			// Read existing entries if file already exists
			if (File.Exists(filePath))
			{
				try
				{
					string existingJson = File.ReadAllText(filePath);
					logs = JsonSerializer.Deserialize<List<LogEntry>>(existingJson) ?? new List<LogEntry>();
				}
				catch (JsonException)
				{
					// If file is corrupted, start fresh
					logs = new List<LogEntry>();
				}
			}
			else
			{
				logs = new List<LogEntry>();
			}

			logs.Add(entry);

			// Serialize with indentation for easy reading in Notepad
			var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			string jsonString = JsonSerializer.Serialize(logs, options);

			File.WriteAllText(filePath, jsonString);
		}


		private void SaveLogAsXml(LogEntry entry)
		{
			string fileName = $"{DateTime.Now:yyyy-MM-dd}.xml";
			string filePath = Path.Combine(_logDirectory, fileName);

			List<LogEntry> logs;

			// Read existing entries if file already exists
			if (File.Exists(filePath))
			{
				try
				{
					XmlSerializer serializer = new XmlSerializer(typeof(List<LogEntry>), new XmlRootAttribute("LogEntries"));
					using (FileStream fs = new FileStream(filePath, FileMode.Open))
					{
						logs = (List<LogEntry>)serializer.Deserialize(fs) ?? new List<LogEntry>();
					}
				}
				catch
				{
					// If file is corrupted, start fresh
					logs = new List<LogEntry>();
				}
			}
			else
			{
				logs = new List<LogEntry>();
			}

			logs.Add(entry);

			// Serialize to XML with proper formatting
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<LogEntry>), new XmlRootAttribute("LogEntries"));
			using (StreamWriter writer = new StreamWriter(filePath))
			{
				xmlSerializer.Serialize(writer, logs);
			}
		}

	
		public string GetTodayLogFilePath()
		{
			string extension = _logFormat == LogFormat.JSON ? "json" : "xml";
			string fileName = $"{DateTime.Now:yyyy-MM-dd}.{extension}";
			return Path.Combine(_logDirectory, fileName);
		}

		public string[] GetAllLogFiles()
		{
			return Directory.GetFiles(_logDirectory, "*.json")
				.Concat(Directory.GetFiles(_logDirectory, "*.xml"))
				.OrderByDescending(f => f)
				.ToArray();
		}
	}
}
=======
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
>>>>>>> origin/main
