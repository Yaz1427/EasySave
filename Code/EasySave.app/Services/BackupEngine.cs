using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EasySave.Models;
using EasySave.ViewModel;
using EasyLog.Models;
using EasyLog.Services;

namespace EasySave.Services
{
    public class BackupEngine
    {
        private readonly ConfigService _configService;
        private AppSettings _settings;
        private LoggerService _logger;
        private readonly CryptoSoftService _cryptoSoftService;
        private readonly RealTimeStateService _realTimeStateService;

        // v3.0: Shared synchronization primitives
        private readonly SemaphoreSlim _largeFileSemaphore = new SemaphoreSlim(1, 1);
        private readonly ManualResetEventSlim _businessSoftwarePause = new ManualResetEventSlim(true);
        private readonly object _priorityLock = new object();
        private int _globalPriorityFilesRemaining;
        private readonly ManualResetEventSlim _priorityDoneEvent = new ManualResetEventSlim(true);

        // Business software monitoring
        private Timer? _businessSoftwareTimer;
        private bool _isBusinessSoftwarePaused;

        // Active job view models for pause propagation
        private readonly List<JobViewModel> _activeJobs = new List<JobViewModel>();
        private readonly object _activeJobsLock = new object();

        public event Action<string>? StatusChanged;
        public event Action<bool>? BusinessSoftwareStateChanged;

        public BackupEngine(AppSettings settings)
        {
            _settings = settings;
            _configService = new ConfigService();

            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string logsDir = Path.Combine(baseDir, "LogsEasySave");

            _logger = new LoggerService(logsDir, settings.GetLogFormat());
            _logger.Destination = ParseLogDestination(settings.LogDestination);
            _logger.LogServerUrl = settings.LogServerUrl ?? string.Empty;
            _cryptoSoftService = new CryptoSoftService();
            _cryptoSoftService.Mode = settings.EncryptionMode == "XOR" ? EncryptionMode.XOR : EncryptionMode.AES;
            _realTimeStateService = new RealTimeStateService();
        }

        public bool IsBusinessSoftwarePaused => _isBusinessSoftwarePaused;

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
            _logger.Format = settings.GetLogFormat();
            _logger.Destination = ParseLogDestination(settings.LogDestination);
            _logger.LogServerUrl = settings.LogServerUrl ?? string.Empty;
            _cryptoSoftService.Mode = settings.EncryptionMode == "XOR" ? EncryptionMode.XOR : EncryptionMode.AES;
        }

        private static LogDestination ParseLogDestination(string dest)
        {
            return dest switch
            {
                "Centralized" => LogDestination.Centralized,
                "Both" => LogDestination.Both,
                _ => LogDestination.Local
            };
        }

        public void StartBusinessSoftwareMonitoring()
        {
            _businessSoftwareTimer?.Dispose();
            _businessSoftwareTimer = new Timer(CheckBusinessSoftware, null, 0, 1500);
        }

        public void StopBusinessSoftwareMonitoring()
        {
            _businessSoftwareTimer?.Dispose();
            _businessSoftwareTimer = null;
        }

        private void CheckBusinessSoftware(object? state)
        {
            if (string.IsNullOrWhiteSpace(_settings.BusinessSoftwareProcess))
            {
                if (_isBusinessSoftwarePaused)
                {
                    _isBusinessSoftwarePaused = false;
                    _businessSoftwarePause.Set();
                    ResumeAllJobsFromBusinessSoftware();
                    BusinessSoftwareStateChanged?.Invoke(false);
                }
                return;
            }

            bool isRunning = false;
            try
            {
                var processes = Process.GetProcessesByName(_settings.BusinessSoftwareProcess);
                isRunning = processes.Any();
            }
            catch { }

            if (isRunning && !_isBusinessSoftwarePaused)
            {
                _isBusinessSoftwarePaused = true;
                _businessSoftwarePause.Reset();
                PauseAllJobsForBusinessSoftware();
                BusinessSoftwareStateChanged?.Invoke(true);
                StatusChanged?.Invoke($"Business software '{_settings.BusinessSoftwareProcess}' detected - all jobs paused.");
            }
            else if (!isRunning && _isBusinessSoftwarePaused)
            {
                _isBusinessSoftwarePaused = false;
                _businessSoftwarePause.Set();
                ResumeAllJobsFromBusinessSoftware();
                BusinessSoftwareStateChanged?.Invoke(false);
                StatusChanged?.Invoke("Business software stopped - jobs resuming.");
            }
        }

        private void PauseAllJobsForBusinessSoftware()
        {
            lock (_activeJobsLock)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.State == JobState.Running)
                    {
                        job.State = JobState.Paused;
                        job.StatusText = "Paused (Business Software)";
                        job.PauseEvent.Reset();
                    }
                }
            }
        }

        private void ResumeAllJobsFromBusinessSoftware()
        {
            lock (_activeJobsLock)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.State == JobState.Paused)
                    {
                        job.State = JobState.Running;
                        job.PauseEvent.Set();
                    }
                }
            }
        }

        public void RegisterPriorityFiles(int count)
        {
            lock (_priorityLock)
            {
                _globalPriorityFilesRemaining += count;
                if (_globalPriorityFilesRemaining > 0)
                    _priorityDoneEvent.Reset();
            }
        }

        public void DecrementPriorityFile()
        {
            lock (_priorityLock)
            {
                _globalPriorityFilesRemaining = Math.Max(0, _globalPriorityFilesRemaining - 1);
                if (_globalPriorityFilesRemaining == 0)
                    _priorityDoneEvent.Set();
            }
        }

        public void WaitForPriorityFiles(CancellationToken token)
        {
            // Wait until all priority files across all jobs are done
            while (!_priorityDoneEvent.IsSet)
            {
                token.ThrowIfCancellationRequested();
                _priorityDoneEvent.Wait(200, token);
            }
        }

        public async Task RunJobsInParallel(IEnumerable<JobViewModel> jobViewModels)
        {
            var jobs = jobViewModels.ToList();
            if (jobs.Count == 0) return;

            // Reset global priority count
            lock (_priorityLock)
            {
                _globalPriorityFilesRemaining = 0;
                _priorityDoneEvent.Set();
            }

            // Pre-scan: count priority files across all jobs
            var priorityExts = _settings.PriorityExtensions
                .Select(e => e.StartsWith(".") ? e : "." + e)
                .ToList();

            foreach (var jvm in jobs)
            {
                if (!Directory.Exists(jvm.Job.SourceDir)) continue;

                var allFiles = Directory.EnumerateFiles(jvm.Job.SourceDir, "*", SearchOption.AllDirectories).ToList();
                int priorityCount = allFiles.Count(f =>
                    priorityExts.Any(ext => Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                RegisterPriorityFiles(priorityCount);
            }

            lock (_activeJobsLock)
            {
                _activeJobs.Clear();
                _activeJobs.AddRange(jobs);
            }

            StartBusinessSoftwareMonitoring();

            try
            {
                var tasks = jobs.Select(jvm => Task.Run(() => RunSingleJob(jvm, priorityExts))).ToList();
                await Task.WhenAll(tasks);
            }
            finally
            {
                StopBusinessSoftwareMonitoring();
                lock (_activeJobsLock)
                {
                    _activeJobs.Clear();
                }
            }
        }

        public async Task RunSingleJobAsync(JobViewModel jvm)
        {
            var priorityExts = _settings.PriorityExtensions
                .Select(e => e.StartsWith(".") ? e : "." + e)
                .ToList();

            // Count priority files for this single job
            lock (_priorityLock)
            {
                _globalPriorityFilesRemaining = 0;
                _priorityDoneEvent.Set();
            }

            if (Directory.Exists(jvm.Job.SourceDir))
            {
                var allFiles = Directory.EnumerateFiles(jvm.Job.SourceDir, "*", SearchOption.AllDirectories).ToList();
                int priorityCount = allFiles.Count(f =>
                    priorityExts.Any(ext => Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                RegisterPriorityFiles(priorityCount);
            }

            lock (_activeJobsLock)
            {
                _activeJobs.Clear();
                _activeJobs.Add(jvm);
            }

            StartBusinessSoftwareMonitoring();

            try
            {
                await Task.Run(() => RunSingleJob(jvm, priorityExts));
            }
            finally
            {
                StopBusinessSoftwareMonitoring();
                lock (_activeJobsLock)
                {
                    _activeJobs.Clear();
                }
            }
        }

        private void RunSingleJob(JobViewModel jvm, List<string> priorityExts)
        {
            var job = jvm.Job;
            var cts = new CancellationTokenSource();
            jvm.CancellationSource = cts;
            var token = cts.Token;

            try
            {
                Application.Current.Dispatcher.Invoke(() => jvm.State = JobState.Running);

                if (!Directory.Exists(job.SourceDir))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        jvm.State = JobState.Error;
                        jvm.StatusText = $"Source not found: {job.SourceDir}";
                    });
                    return;
                }

                Directory.CreateDirectory(job.TargetDir);

                string sourceFolderName = new DirectoryInfo(job.SourceDir).Name;
                string actualTargetDir = Path.Combine(job.TargetDir, sourceFolderName);
                Directory.CreateDirectory(actualTargetDir);

                var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();

                // For differential: filter to only modified files
                List<string> filesToCopy;
                if (job.Type == JobType.Differential)
                {
                    filesToCopy = new List<string>();
                    foreach (var sourceFile in allFiles)
                    {
                        var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                        var targetFile = Path.Combine(actualTargetDir, relativePath);
                        bool shouldCopy =
                            !File.Exists(targetFile) ||
                            File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(targetFile) ||
                            new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length;
                        if (shouldCopy) filesToCopy.Add(sourceFile);
                    }
                }
                else
                {
                    filesToCopy = allFiles;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    jvm.TotalFiles = filesToCopy.Count;
                    jvm.TotalSize = filesToCopy.Sum(f => new FileInfo(f).Length);
                    jvm.RemainingFiles = filesToCopy.Count;
                    jvm.RemainingSize = jvm.TotalSize;
                });

                // Update real-time state file
                var state = new RealTimeState
                {
                    JobName = job.Name,
                    Status = "Active",
                    LastActionTimestamp = DateTime.Now,
                    TotalFilesToCopy = filesToCopy.Count,
                    TotalFilesSize = filesToCopy.Sum(f => new FileInfo(f).Length),
                    RemainingFiles = filesToCopy.Count,
                    RemainingSize = filesToCopy.Sum(f => new FileInfo(f).Length),
                    Progress = 0
                };
                _realTimeStateService.UpdateState(state);

                // Separate priority and non-priority files
                var priorityFiles = filesToCopy.Where(f =>
                    priorityExts.Any(ext => Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase))).ToList();
                var normalFiles = filesToCopy.Except(priorityFiles).ToList();

                int processedFiles = 0;
                long processedSize = 0;

                // Process priority files first
                foreach (var sourceFile in priorityFiles)
                {
                    ProcessSingleFile(job, jvm, sourceFile, actualTargetDir, state,
                        filesToCopy.Count, ref processedFiles, ref processedSize, token,
                        isPriority: true);
                }

                // Process non-priority files (wait for all priority files across ALL jobs)
                foreach (var sourceFile in normalFiles)
                {
                    ProcessSingleFile(job, jvm, sourceFile, actualTargetDir, state,
                        filesToCopy.Count, ref processedFiles, ref processedSize, token,
                        isPriority: false);
                }

                // Job completed
                state.Status = "End";
                state.Progress = 100;
                state.RemainingFiles = 0;
                state.RemainingSize = 0;
                state.CurrentSourceFile = "";
                state.CurrentTargetFile = "";
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    jvm.Progress = 100;
                    jvm.RemainingFiles = 0;
                    jvm.RemainingSize = 0;
                    jvm.CurrentFile = string.Empty;
                    jvm.State = JobState.Completed;
                    jvm.StatusText = "Completed";
                });
            }
            catch (OperationCanceledException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (jvm.State != JobState.Stopped)
                        jvm.State = JobState.Stopped;
                    jvm.StatusText = "Stopped";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    jvm.State = JobState.Error;
                    jvm.StatusText = $"Error: {ex.Message}";
                });
            }
        }

        private void ProcessSingleFile(BackupJob job, JobViewModel jvm, string sourceFile,
            string actualTargetDir, RealTimeState state, int totalFileCount,
            ref int processedFiles, ref long processedSize, CancellationToken token, bool isPriority)
        {
            token.ThrowIfCancellationRequested();

            if (!isPriority)
                WaitForPriorityFiles(token);

            WaitForPauseAndBusinessSoftware(jvm, token);

            var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
            var targetFile = Path.Combine(actualTargetDir, relativePath);

            Application.Current.Dispatcher.Invoke(() => jvm.CurrentFile = sourceFile);
            state.CurrentSourceFile = sourceFile;
            state.CurrentTargetFile = targetFile;
            state.LastActionTimestamp = DateTime.Now;
            _realTimeStateService.UpdateState(state);

            long fileSize = new FileInfo(sourceFile).Length;
            long thresholdBytes = _settings.MaxLargeFileSizeKB * 1024;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            if (fileSize > thresholdBytes)
            {
                _largeFileSemaphore.Wait(token);
                try
                {
                    CopyFileWithLogging(job, sourceFile, targetFile);
                }
                finally
                {
                    _largeFileSemaphore.Release();
                }
            }
            else
            {
                CopyFileWithLogging(job, sourceFile, targetFile);
            }

            if (isPriority)
                DecrementPriorityFile();

            processedFiles++;
            processedSize += fileSize;
            double prog = (double)processedFiles / totalFileCount * 100;
            int remaining = totalFileCount - processedFiles;
            long remainingSize = jvm.TotalSize - processedSize;

            Application.Current.Dispatcher.Invoke(() =>
            {
                jvm.Progress = prog;
                jvm.RemainingFiles = remaining;
                jvm.RemainingSize = remainingSize;
            });

            state.Progress = prog;
            state.RemainingFiles = remaining;
            state.RemainingSize = state.TotalFilesSize - processedSize;
            _realTimeStateService.UpdateState(state);
        }

        private void WaitForPauseAndBusinessSoftware(JobViewModel jvm, CancellationToken token)
        {
            // Wait for per-job pause
            while (!jvm.PauseEvent.IsSet)
            {
                token.ThrowIfCancellationRequested();
                jvm.PauseEvent.Wait(300, token);
            }

            // Wait for business software pause
            while (!_businessSoftwarePause.IsSet)
            {
                token.ThrowIfCancellationRequested();
                _businessSoftwarePause.Wait(300, token);
            }
        }

        private void CopyFileWithLogging(BackupJob job, string sourceFile, string targetFile)
        {
            var sw = Stopwatch.StartNew();
            long fileSize = new FileInfo(sourceFile).Length;

            try
            {
                File.Copy(sourceFile, targetFile, overwrite: true);
                sw.Stop();

                int encryptionTime = 0;
                string ext = Path.GetExtension(sourceFile);
                var encryptExts = _settings.EncryptExtensions ?? new List<string>();
                if (encryptExts.Any(e =>
                    e.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    ("." + e.TrimStart('.')).Equals(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    encryptionTime = _cryptoSoftService.EncryptFile(targetFile);
                }

                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    JobName = job.Name,
                    SourcePath = sourceFile,
                    TargetPath = targetFile,
                    FileSize = fileSize,
                    TransferTime = (int)sw.ElapsedMilliseconds,
                    EncryptionTime = encryptionTime
                };

                _logger.SaveLog(entry);
            }
            catch (Exception)
            {
                sw.Stop();
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    JobName = job.Name,
                    SourcePath = sourceFile,
                    TargetPath = targetFile,
                    FileSize = fileSize,
                    TransferTime = -(int)sw.ElapsedMilliseconds,
                    EncryptionTime = 0
                };
                _logger.SaveLog(entry);
                throw;
            }
        }

        public void PauseAll()
        {
            lock (_activeJobsLock)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.State == JobState.Running)
                        job.Pause();
                }
            }
        }

        public void ResumeAll()
        {
            if (_isBusinessSoftwarePaused) return;

            lock (_activeJobsLock)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.State == JobState.Paused)
                        job.Resume();
                }
            }
        }

        public void StopAll()
        {
            lock (_activeJobsLock)
            {
                foreach (var job in _activeJobs)
                {
                    job.Stop();
                }
            }
        }
    }
}
