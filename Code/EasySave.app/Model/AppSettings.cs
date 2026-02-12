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

        public LogFormat GetLogFormat()
        {
            return LogFormat.Equals("XML", StringComparison.OrdinalIgnoreCase)
                ? EasyLog.Services.LogFormat.XML
                : EasyLog.Services.LogFormat.JSON;
        }
    }
}
