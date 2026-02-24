using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EasySave.Models;

namespace EasySave.Services
{
    public class RealTimeStateService
    {
        private readonly string _stateFilePath;
        private static readonly object _lock = new object();

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

        // v3.0: Write all job states at once (thread-safe, atomic)
        public void UpdateState(RealTimeState state)
        {
            lock (_lock)
            {
                // Read existing states
                List<RealTimeState> allStates = ReadAllStates();

                // Update or add this job's state
                int idx = allStates.FindIndex(s => s.JobName == state.JobName);
                if (idx >= 0)
                    allStates[idx] = state;
                else
                    allStates.Add(state);

                WriteAllStates(allStates);
            }
        }

        public List<RealTimeState> GetAllStates()
        {
            lock (_lock)
            {
                return ReadAllStates();
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
                    // Try to deserialize as list first (v3.0 format)
                    var list = JsonSerializer.Deserialize<List<RealTimeState>>(json);
                    if (list != null && list.Count > 0)
                        return list[list.Count - 1];

                    return new RealTimeState { Status = "Inactive", LastActionTimestamp = DateTime.Now };
                }
                catch
                {
                    try
                    {
                        // Fallback: single object (v2.0 compat)
                        string json = File.ReadAllText(_stateFilePath);
                        return JsonSerializer.Deserialize<RealTimeState>(json) ?? new RealTimeState();
                    }
                    catch
                    {
                        return new RealTimeState { Status = "Inactive", LastActionTimestamp = DateTime.Now };
                    }
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

        private List<RealTimeState> ReadAllStates()
        {
            if (!File.Exists(_stateFilePath))
                return new List<RealTimeState>();

            try
            {
                string json = File.ReadAllText(_stateFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<RealTimeState>();

                return JsonSerializer.Deserialize<List<RealTimeState>>(json) ?? new List<RealTimeState>();
            }
            catch
            {
                return new List<RealTimeState>();
            }
        }

        private void WriteAllStates(List<RealTimeState> states)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(states, options);

            // Atomic write
            string tempPath = _stateFilePath + ".tmp";
            File.WriteAllText(tempPath, jsonString);
            File.Copy(tempPath, _stateFilePath, overwrite: true);
            File.Delete(tempPath);
        }
    }
}
