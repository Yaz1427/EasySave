using System;

namespace EasyLog.Models
{
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
