using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EasySave.Models;
using EasyLog.Services;
using EasyLog.Models;

namespace EasySave.Services
{
    public class BackupService
    {
        private readonly LoggerService _logger;
        private readonly RealTimeStateService _realTimeStateService;
        private readonly CryptoSoftService _cryptoSoftService;
        private readonly BusinessSoftwareService _businessSoftwareService;
        private List<string> _encryptExtensions;

        public BackupService(AppSettings settings)
        {
            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string logsDir = Path.Combine(baseDir, "LogsEasySave");

            _logger = new LoggerService(logsDir, settings.GetLogFormat());
            _realTimeStateService = new RealTimeStateService();
            _cryptoSoftService = new CryptoSoftService();
            _cryptoSoftService.Mode = settings.EncryptionMode == "XOR" ? EncryptionMode.XOR : EncryptionMode.AES;
            _businessSoftwareService = new BusinessSoftwareService(settings.BusinessSoftwareProcess);
            _encryptExtensions = settings.EncryptExtensions ?? new List<string>();
        }

        public void UpdateSettings(AppSettings settings)
        {
            _logger.Format = settings.GetLogFormat();
            _businessSoftwareService.ProcessName = settings.BusinessSoftwareProcess;
            _cryptoSoftService.Mode = settings.EncryptionMode == "XOR" ? EncryptionMode.XOR : EncryptionMode.AES;
            _encryptExtensions = settings.EncryptExtensions ?? new List<string>();
        }

        /// <summary>
        /// Checks if the business software is currently running.
        /// </summary>
        public bool IsBusinessSoftwareRunning()
        {
            return _businessSoftwareService.IsRunning();
        }

        public void ExecuteBackup(BackupJob job, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (string.IsNullOrWhiteSpace(job.SourceDir)) throw new ArgumentException("SourceDir is empty.");
            if (string.IsNullOrWhiteSpace(job.TargetDir)) throw new ArgumentException("TargetDir is empty.");

            // v2.0: Block if business software is running
            if (_businessSoftwareService.IsRunning())
                throw new InvalidOperationException("Business software is running. Backup blocked.");

            if (!Directory.Exists(job.SourceDir))
                throw new DirectoryNotFoundException($"Source not found: {job.SourceDir}");

            Directory.CreateDirectory(job.TargetDir);

            var state = _realTimeStateService.CreateInitialState(job);
            _realTimeStateService.UpdateState(state);

            var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
            state.TotalFilesToCopy = allFiles.Count;
            state.TotalFilesSize = allFiles.Sum(f => new FileInfo(f).Length);
            state.RemainingFiles = allFiles.Count;
            state.RemainingSize = state.TotalFilesSize;
            _realTimeStateService.UpdateState(state);

            try
            {
                if (job.Type == JobType.Full)
                    CopyDirectoryFull(job, state, progress, cancellationToken);
                else if (job.Type == JobType.Differential)
                    CopyDirectoryDifferential(job, state, progress, cancellationToken);
                else
                    throw new NotSupportedException($"Unsupported job type: {job.Type}");

                state.Status = "End";
                state.Progress = 100;
                state.RemainingFiles = 0;
                state.RemainingSize = 0;
                state.CurrentSourceFile = "";
                state.CurrentTargetFile = "";
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);
            }
            catch (BusinessSoftwareRunningException)
            {
                state.Status = "Stopped - Business software detected";
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                var stopEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    JobName = job.Name,
                    SourcePath = state.CurrentSourceFile,
                    TargetPath = state.CurrentTargetFile,
                    FileSize = 0,
                    TransferTime = -1,
                    EncryptionTime = 0
                };
                _logger.SaveLog(stopEntry);
                throw;
            }
        }

        private void CopyDirectoryFull(BackupJob job, RealTimeState state, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            string sourceFolderName = new DirectoryInfo(job.SourceDir).Name;
            string actualTargetDir = Path.Combine(job.TargetDir, sourceFolderName);
            Directory.CreateDirectory(actualTargetDir);

            var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
            int processedFiles = 0;

            foreach (var sourceFile in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // v2.0: Check business software between files
                if (_businessSoftwareService.IsRunning())
                    throw new BusinessSoftwareRunningException();

                var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                var targetFile = Path.Combine(actualTargetDir, relativePath);

                state.CurrentSourceFile = sourceFile;
                state.CurrentTargetFile = targetFile;
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                CopyFileWithTiming(job, sourceFile, targetFile);

                processedFiles++;
                state.Progress = (double)processedFiles / allFiles.Count * 100;
                state.RemainingFiles = allFiles.Count - processedFiles;
                state.RemainingSize = allFiles.Skip(processedFiles).Sum(f => new FileInfo(f).Length);
                _realTimeStateService.UpdateState(state);
                progress?.Report(state.Progress);
            }
        }

        private void CopyDirectoryDifferential(BackupJob job, RealTimeState state, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            string sourceFolderName = new DirectoryInfo(job.SourceDir).Name;
            string actualTargetDir = Path.Combine(job.TargetDir, sourceFolderName);
            Directory.CreateDirectory(actualTargetDir);

            var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
            var filesToCopy = new List<string>();

            foreach (var sourceFile in allFiles)
            {
                var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                var targetFile = Path.Combine(actualTargetDir, relativePath);

                bool shouldCopy =
                    !File.Exists(targetFile) ||
                    File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(targetFile) ||
                    new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length;

                if (shouldCopy)
                    filesToCopy.Add(sourceFile);
            }

            int processedFiles = 0;

            foreach (var sourceFile in filesToCopy)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // v2.0: Check business software between files
                if (_businessSoftwareService.IsRunning())
                    throw new BusinessSoftwareRunningException();

                var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                var targetFile = Path.Combine(actualTargetDir, relativePath);

                state.CurrentSourceFile = sourceFile;
                state.CurrentTargetFile = targetFile;
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                CopyFileWithTiming(job, sourceFile, targetFile);

                processedFiles++;
                state.Progress = (double)processedFiles / filesToCopy.Count * 100;
                state.RemainingFiles = filesToCopy.Count - processedFiles;
                state.RemainingSize = filesToCopy.Skip(processedFiles).Sum(f => new FileInfo(f).Length);
                _realTimeStateService.UpdateState(state);
                progress?.Report(state.Progress);
            }
        }

        private void CopyFileWithTiming(BackupJob job, string sourceFile, string targetFile)
        {
            var sw = Stopwatch.StartNew();
            long fileSize = new FileInfo(sourceFile).Length;

            try
            {
                File.Copy(sourceFile, targetFile, overwrite: true);
                sw.Stop();

                // v2.0: Encrypt if needed
                int encryptionTime = 0;
                string ext = Path.GetExtension(sourceFile);
                if (_encryptExtensions.Any(e =>
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
    }

    public class BusinessSoftwareRunningException : Exception
    {
        public BusinessSoftwareRunningException()
            : base("Business software detected. Backup stopped after current file.")
        {
        }
    }
    private bool ShouldEncrypt(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return _configService.CryptoExtensions.Contains(ext);
        }

    }
