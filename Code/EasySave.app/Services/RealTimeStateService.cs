using System;
using System.IO;
using System.Text.Json;
using EasySave.Models;

namespace EasySave.Services
{
    public class RealTimeStateService
    {
        private readonly string _stateFilePath;
        private readonly object _lock = new object();

        public RealTimeStateService()
        {
            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string appDataDir = Path.Combine(baseDir, "LogsEasySave");

            if (!Directory.Exists(appDataDir))
                Directory.CreateDirectory(appDataDir);

            _stateFilePath = Path.Combine(appDataDir, "state.json");
        }

        public void UpdateState(RealTimeState state)
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonString = JsonSerializer.Serialize(state, options);
                File.WriteAllText(_stateFilePath, jsonString);
            }
        }

        public RealTimeState GetCurrentState()
        {
            lock (_lock)
            {
                if (!File.Exists(_stateFilePath))
                {
                    return new RealTimeState
                    {
                        Status = "Inactive",
                        LastActionTimestamp = DateTime.Now
                    };
                }

                try
                {
                    string json = File.ReadAllText(_stateFilePath);
                    return JsonSerializer.Deserialize<RealTimeState>(json) ?? new RealTimeState();
                }
                catch
                {
                    return new RealTimeState
                    {
                        Status = "Inactive",
                        LastActionTimestamp = DateTime.Now
                    };
                }
            }
        }

        public RealTimeState CreateInitialState(BackupJob job)
        {
            return new RealTimeState
            {
                JobName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now,
                TotalFilesToCopy = 0,
                TotalFilesSize = 0,
                Progress = 0,
                RemainingFiles = 0,
                RemainingSize = 0,
                CurrentSourceFile = "",
                CurrentTargetFile = ""
            };
        }
    }
}
