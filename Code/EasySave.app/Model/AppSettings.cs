using System;
using System.Collections.Generic;
using EasyLog.Services;

namespace EasySave.Models
{
    public class AppSettings
    {
        // Language: "en" or "fr"
        public string Language { get; set; } = "en";

        // Log format: JSON or XML
        public string LogFormat { get; set; } = "JSON";

        // Extensions to encrypt (e.g. ".txt", ".docx", ".pdf")
        public List<string> EncryptExtensions { get; set; } = new List<string>();

        // Business software process name (e.g. "CalculatorApp" or "notepad")
        public string BusinessSoftwareProcess { get; set; } = string.Empty;

        // Encryption mode: "AES" or "XOR"
        public string EncryptionMode { get; set; } = "AES";

        // v3.0: Priority file extensions (e.g. ".docx", ".xlsx")
        public List<string> PriorityExtensions { get; set; } = new List<string>();

        // v3.0: Max large file size in KB (files above this cannot transfer in parallel)
        public long MaxLargeFileSizeKB { get; set; } = 1024;

        // v3.0: Log destination - "Local", "Centralized", "Both"
        public string LogDestination { get; set; } = "Local";

        // v3.0: Docker log server URL (e.g. "http://localhost:5080")
        public string LogServerUrl { get; set; } = string.Empty;

        public LogFormat GetLogFormat()
        {
            return LogFormat.Equals("XML", StringComparison.OrdinalIgnoreCase)
                ? EasyLog.Services.LogFormat.XML
                : EasyLog.Services.LogFormat.JSON;
        }
    }
}
