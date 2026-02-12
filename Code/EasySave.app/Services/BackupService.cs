using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EasySave.Models;
using EasyLog;
using EasyLog.Services;
using EasyLog.Models;

namespace EasySave.Services
{
	public class BackupService
	{
		// Dépendances
		private readonly LoggerService _logger;
		private readonly RealTimeStateService _realTimeStateService;

		// Constructeur
		public BackupService()
		{
			// ✅ Chemin logs "comme avant" = dossier Logs à côté de l'exe
			// + choix du format (mets Xml si tu veux)
			_logger = new LoggerService(LogFormat.Json); // ou LogFormat.Xml
			_realTimeStateService = new RealTimeStateService();
		}

		// Méthode principale : exécutera une sauvegarde
		public void ExecuteBackup(BackupJob job)
		{
			if (job == null) throw new ArgumentNullException(nameof(job));
			if (string.IsNullOrWhiteSpace(job.SourceDir)) throw new ArgumentException("SourceDir vide");
			if (string.IsNullOrWhiteSpace(job.TargetDir)) throw new ArgumentException("TargetDir null");

			// Vérifications répertoires
			if (!Directory.Exists(job.SourceDir))
				throw new DirectoryNotFoundException($"Source introuvable : {job.SourceDir}");

			Directory.CreateDirectory(job.TargetDir);

			// Initialiser l'état temps réel
			var state = _realTimeStateService.CreateInitialState(job);
			_realTimeStateService.UpdateState(state);

			// Calculer les informations initiales
			var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
			state.TotalFilesToCopy = allFiles.Count;
			state.TotalFilesSize = allFiles.Sum(f => new FileInfo(f).Length);
			state.RemainingFiles = allFiles.Count;
			state.RemainingSize = state.TotalFilesSize;
			_realTimeStateService.UpdateState(state);

			// Mesure temps total job
			var swJob = Stopwatch.StartNew();

			try
			{
				// Copie selon type
				if (job.Type == JobType.Full)
				{
					CopyDirectoryFull(job, state);
				}
				else if (job.Type == JobType.Differential)
				{
					CopyDirectoryDifferential(job, state);
				}
				else
				{
					throw new NotSupportedException($"Type de job non supporté : {job.Type}");
				}

				// Marquer comme terminé
				state.Status = "End";
				state.Progress = 100;
				state.RemainingFiles = 0;
				state.RemainingSize = 0;
				state.CurrentSourceFile = "";
				state.CurrentTargetFile = "";
				state.LastActionTimestamp = DateTime.Now;
				_realTimeStateService.UpdateState(state);
			}
			finally
			{
				swJob.Stop();

				// ✅ Log "job terminé" (log journalier)
				// EncryptTime = 0 car pas encore CryptoSoft dans ce code
				var entry = new LogEntry
				{
					Timestamp = DateTime.Now,
					JobName = job.Name,
					SourcePath = job.SourceDir,
					TargetPath = job.TargetDir,
					FileSize = GetDirectorySize(job.TargetDir),
					TransferTime = (int)swJob.ElapsedMilliseconds,
					EncryptTime = 0,
					Message = "Job finished"
				};

				_logger.SaveLog(entry);
			}
		}

		// Règle de sauvegarde différentielle
		public bool CheckDifferential(string file)
		{
			if (string.IsNullOrWhiteSpace(file)) return false;
			if (!File.Exists(file)) return false;
			return true;
		}

		// Helpers privés

		private void CopyDirectoryFull(BackupJob job, RealTimeState state)
		{
			var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
			int processedFiles = 0;

			foreach (var sourceFile in allFiles)
			{
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(job.TargetDir, relativePath);

				// Mettre à jour l'état temps réel
				state.CurrentSourceFile = sourceFile;
				state.CurrentTargetFile = targetFile;
				state.LastActionTimestamp = DateTime.Now;
				_realTimeStateService.UpdateState(state);

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile);

				processedFiles++;
				state.Progress = allFiles.Count == 0 ? 100 : (double)processedFiles / allFiles.Count * 100;
				state.RemainingFiles = allFiles.Count - processedFiles;
				state.RemainingSize = allFiles.Skip(processedFiles).Sum(f => new FileInfo(f).Length);
				_realTimeStateService.UpdateState(state);
			}
		}

		private void CopyDirectoryDifferential(BackupJob job, RealTimeState state)
		{
			var allFiles = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
			var filesToCopy = new List<string>();

			// Déterminer les fichiers à copier
			foreach (var sourceFile in allFiles)
			{
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(job.TargetDir, relativePath);

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
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(job.TargetDir, relativePath);

				// Mettre à jour l'état temps réel
				state.CurrentSourceFile = sourceFile;
				state.CurrentTargetFile = targetFile;
				state.LastActionTimestamp = DateTime.Now;
				_realTimeStateService.UpdateState(state);

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile);

				processedFiles++;
				state.Progress = filesToCopy.Count == 0 ? 100 : (double)processedFiles / filesToCopy.Count * 100;
				state.RemainingFiles = filesToCopy.Count - processedFiles;
				state.RemainingSize = filesToCopy.Skip(processedFiles).Sum(f => new FileInfo(f).Length);
				_realTimeStateService.UpdateState(state);
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

				var entry = new LogEntry
				{
					Timestamp = DateTime.Now,
					JobName = job.Name,
					SourcePath = sourceFile,
					TargetPath = targetFile,
					FileSize = fileSize,
					TransferTime = (int)sw.ElapsedMilliseconds,
					EncryptTime = 0, // ✅ pas de CryptoSoft ici pour l'instant
					Message = "File copied"
				};

				_logger.SaveLog(entry);
			}
			catch (Exception ex)
			{
				sw.Stop();

				var entry = new LogEntry
				{
					Timestamp = DateTime.Now,
					JobName = job.Name,
					SourcePath = sourceFile,
					TargetPath = targetFile,
					FileSize = fileSize,
					TransferTime = -Math.Abs((int)sw.ElapsedMilliseconds),
					EncryptTime = -1, // ✅ erreur cryptage (ici on met -1 par défaut)
					Message = $"Error: {ex.Message}"
				};

				_logger.SaveLog(entry);
				throw;
			}
		}

		private long GetDirectorySize(string dir)
		{
			long size = 0;
			foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
				size += new FileInfo(f).Length;
			return size;
		}
	}
}