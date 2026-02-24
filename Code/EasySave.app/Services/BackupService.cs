using System.Diagnostics;
using EasySave.Models;
using EasyLog.Services;
using EasyLog.Models;
using System.IO;
using System.Linq;

namespace EasySave.Services
{
	public class BackupService
	{
		private readonly LoggerService _logger;
		private readonly RealTimeStateService _realTimeStateService;
		private readonly CryptoSoftService _cryptoSoftService;
		private readonly BusinessSoftwareService _businessSoftwareService;
		private List<string> _encryptExtensions;

		// v3.0 - Gestion globale des fichiers prioritaires (entre tous les jobs parallèles)
		private static int _globalPriorityRemaining = 0;
		private static readonly ManualResetEventSlim _noPriorityPendingEvent = new(true);
		private List<string> _priorityExtensions;

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
			_priorityExtensions = new List<string>();
		}

		public void UpdateSettings(AppSettings settings)
		{
			_logger.Format = settings.GetLogFormat();
			_businessSoftwareService.ProcessName = settings.BusinessSoftwareProcess;
			_cryptoSoftService.Mode = settings.EncryptionMode == "XOR" ? EncryptionMode.XOR : EncryptionMode.AES;
			_encryptExtensions = settings.EncryptExtensions ?? new List<string>();
		}

		public void SetPriorityExtensions(List<string> extensions)
		{
			_priorityExtensions = extensions ?? new List<string>();
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
			catch (OperationCanceledException)
			{
				state.Status = "Stopped by user";
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

				return;
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
			var fileSizes = allFiles.Select(f => new FileInfo(f).Length).ToList();
			long remainingSize = fileSizes.Sum();
			int processedFiles = 0;

			// v3.0 - Séparer les fichiers prioritaires et non prioritaires
			var priorityIndices = new List<int>();
			var nonPriorityIndices = new List<int>();
			for (int i = 0; i < allFiles.Count; i++)
			{
				if (IsPriorityFile(allFiles[i]))
					priorityIndices.Add(i);
				else
					nonPriorityIndices.Add(i);
			}

			if (priorityIndices.Count > 0)
			{
				Interlocked.Add(ref _globalPriorityRemaining, priorityIndices.Count);
				_noPriorityPendingEvent.Reset();
			}

			// 1) D'abord : traiter les fichiers prioritaires
			int priorityProcessed = 0;
			try
			{
				foreach (int idx in priorityIndices)
				{
					cancellationToken.ThrowIfCancellationRequested();
					WaitIfPausedOrBusinessRunning(job, cancellationToken);

					var sourceFile = allFiles[idx];
					var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
					var targetFile = Path.Combine(actualTargetDir, relativePath);

					state.CurrentSourceFile = sourceFile;
					state.CurrentTargetFile = targetFile;
					state.LastActionTimestamp = DateTime.Now;
					_realTimeStateService.UpdateState(state);

					Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
					CopyFileWithTiming(job, sourceFile, targetFile, cancellationToken);

					priorityProcessed++;

					if (Interlocked.Decrement(ref _globalPriorityRemaining) == 0)
						_noPriorityPendingEvent.Set();

					remainingSize -= fileSizes[idx];
					processedFiles++;
					state.Progress = (double)processedFiles / allFiles.Count * 100;
					state.RemainingFiles = allFiles.Count - processedFiles;
					state.RemainingSize = remainingSize;
					_realTimeStateService.UpdateState(state);
					progress?.Report(state.Progress);
				}
			}
			finally
			{
				int remaining = priorityIndices.Count - priorityProcessed;
				if (remaining > 0)
				{
					if (Interlocked.Add(ref _globalPriorityRemaining, -remaining) == 0)
						_noPriorityPendingEvent.Set();
				}
			}

			// 2) Ensuite : traiter les fichiers non prioritaires
			foreach (int idx in nonPriorityIndices)
			{
				_noPriorityPendingEvent.Wait();
				cancellationToken.ThrowIfCancellationRequested();
				WaitIfPausedOrBusinessRunning(job, cancellationToken);

				var sourceFile = allFiles[idx];
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(actualTargetDir, relativePath);

				state.CurrentSourceFile = sourceFile;
				state.CurrentTargetFile = targetFile;
				state.LastActionTimestamp = DateTime.Now;
				_realTimeStateService.UpdateState(state);

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile, cancellationToken);

				remainingSize -= fileSizes[idx];
				processedFiles++;
				state.Progress = (double)processedFiles / allFiles.Count * 100;
				state.RemainingFiles = allFiles.Count - processedFiles;
				state.RemainingSize = remainingSize;
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

			var fileSizes = filesToCopy.Select(f => new FileInfo(f).Length).ToList();
			long remainingSize = fileSizes.Sum();
			int processedFiles = 0;

			// v3.0 - Séparer prioritaires / non prioritaires
			var priorityIndices = new List<int>();
			var nonPriorityIndices = new List<int>();
			for (int i = 0; i < filesToCopy.Count; i++)
			{
				if (IsPriorityFile(filesToCopy[i]))
					priorityIndices.Add(i);
				else
					nonPriorityIndices.Add(i);
			}

			if (priorityIndices.Count > 0)
			{
				Interlocked.Add(ref _globalPriorityRemaining, priorityIndices.Count);
				_noPriorityPendingEvent.Reset();
			}

			// 1) D'abord : prioritaires
			int priorityProcessed = 0;
			try
			{
				foreach (int idx in priorityIndices)
				{
					cancellationToken.ThrowIfCancellationRequested();
					WaitIfPausedOrBusinessRunning(job, cancellationToken);

					var sourceFile = filesToCopy[idx];
					var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
					var targetFile = Path.Combine(actualTargetDir, relativePath);

					state.CurrentSourceFile = sourceFile;
					state.CurrentTargetFile = targetFile;
					state.LastActionTimestamp = DateTime.Now;
					_realTimeStateService.UpdateState(state);

					Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
					CopyFileWithTiming(job, sourceFile, targetFile, cancellationToken);

					priorityProcessed++;

					if (Interlocked.Decrement(ref _globalPriorityRemaining) == 0)
						_noPriorityPendingEvent.Set();

					remainingSize -= fileSizes[idx];
					processedFiles++;
					state.Progress = (double)processedFiles / filesToCopy.Count * 100;
					state.RemainingFiles = filesToCopy.Count - processedFiles;
					state.RemainingSize = remainingSize;
					_realTimeStateService.UpdateState(state);
					progress?.Report(state.Progress);
				}
			}
			finally
			{
				int remaining = priorityIndices.Count - priorityProcessed;
				if (remaining > 0)
				{
					if (Interlocked.Add(ref _globalPriorityRemaining, -remaining) == 0)
						_noPriorityPendingEvent.Set();
				}
			}

			// 2) Ensuite : non prioritaires
			foreach (int idx in nonPriorityIndices)
			{
				_noPriorityPendingEvent.Wait();
				cancellationToken.ThrowIfCancellationRequested();
				WaitIfPausedOrBusinessRunning(job, cancellationToken);

				var sourceFile = filesToCopy[idx];
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(actualTargetDir, relativePath);

				state.CurrentSourceFile = sourceFile;
				state.CurrentTargetFile = targetFile;
				state.LastActionTimestamp = DateTime.Now;
				_realTimeStateService.UpdateState(state);

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile, cancellationToken);

				remainingSize -= fileSizes[idx];
				processedFiles++;
				state.Progress = (double)processedFiles / filesToCopy.Count * 100;
				state.RemainingFiles = filesToCopy.Count - processedFiles;
				state.RemainingSize = remainingSize;
				_realTimeStateService.UpdateState(state);
				progress?.Report(state.Progress);
			}
		}

		private void CopyFileCancellable(string sourceFile, string targetFile, BackupJob job, CancellationToken token)
		{
			const int bufferSize = 1024 * 1024; // 1MB

			using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: false);
			using var dest = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: false);

			var buffer = new byte[bufferSize];
			int read;

			while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
			{
				WaitIfPausedOrBusinessRunning(job, token);
				dest.Write(buffer, 0, read);
			}
		}

		private void CopyFileWithTiming(BackupJob job, string sourceFile, string targetFile, CancellationToken token)
		{
			var sw = Stopwatch.StartNew();
			long fileSize = new FileInfo(sourceFile).Length;

			try
			{
				CopyFileCancellable(sourceFile, targetFile, job, token);
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

		// v3.0 : Wait loop for pause
		private void WaitIfPausedOrBusinessRunning(BackupJob job, CancellationToken token)
		{
			while (true)
			{
				token.ThrowIfCancellationRequested();

				if (job.IsStopped)
					throw new OperationCanceledException("Job stopped by user.");

				bool mustPause = job.IsPaused || _businessSoftwareService.IsRunning();

				if (!mustPause)
					return;

				Thread.Sleep(200);
			}
		}

		// v3.0 - Normalise les extensions (".pdf" / "pdf" -> ".pdf")
		private static string NormalizeExt(string ext)
		{
			if (string.IsNullOrWhiteSpace(ext)) return "";
			ext = ext.Trim();
			return ext.StartsWith(".") ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
		}

		// v3.0 - Détermine si un fichier est prioritaire selon son extension
		private bool IsPriorityFile(string filePath)
		{
			string ext = NormalizeExt(Path.GetExtension(filePath));
			return _priorityExtensions.Any(e => NormalizeExt(e) == ext);
		}

		private long GetDirectorySize(string dir)
		{
			long size = 0;
			foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
				size += new FileInfo(f).Length;
			return size;
		}
	}

	public class BusinessSoftwareRunningException : Exception
	{
		public BusinessSoftwareRunningException()
			: base("Business software detected. Backup stopped after current file.")
		{
		}
	}
}
