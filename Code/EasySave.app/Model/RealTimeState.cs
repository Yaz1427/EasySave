using System;

namespace EasySave.Models
{
    /// <summary>
    /// Real-time state structure for backup jobs (state.json).
    /// Contains live progress information per backup job.
    /// </summary>
    public class RealTimeState
    {
        // Backup job name
        public string JobName { get; set; } = string.Empty;

        // Timestamp of the last action
        public DateTime LastActionTimestamp { get; set; }

        // Backup job state (Active, Inactive, End, etc.)
        public string Status { get; set; } = "Inactive";

        // Total number of eligible files
        public int TotalFilesToCopy { get; set; }

        // Total size of files to transfer (bytes)
        public long TotalFilesSize { get; set; }

        // Progress percentage
        public double Progress { get; set; }

        // Number of remaining files
        public int RemainingFiles { get; set; }

        // Size of remaining files (bytes)
        public long RemainingSize { get; set; }

        // Full path of the source file currently being copied
        public string CurrentSourceFile { get; set; } = string.Empty;

        // Full path of the target destination file
        public string CurrentTargetFile { get; set; } = string.Empty;
    }
}
