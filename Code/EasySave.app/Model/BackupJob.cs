using System;

namespace EasySave.Models
{
    public enum JobType
    {
        Full,
        Differential
    }

    public class BackupJob
    {
        public string Name { get; set; } = string.Empty;
        public string SourceDir { get; set; } = string.Empty;
        public string TargetDir { get; set; } = string.Empty;
        public JobType Type { get; set; }
    }
}
