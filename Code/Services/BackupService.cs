using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using EasySave.app.Model;
using Models;
using EasyLog;


namespace EasySave.Services
{
	public class BackupService
	{
		// Dépendances (seront injectées plus tard)
		// private readonly DLL _logger;

		private readonly Logger _logger;

		// Constructeur
		public BackupService()
		{
			_logger = _logger ?? throw new ArgumentNullException(nameof(logger));
		}



		// Méthode principale : exécutera une sauvegarde
		public void ExecuteBackup(BackupJob job)
		{
			// TODO :
			// - Parcourir les fichiers du dossier source
			// - Appliquer la logique Full / Differential
			// - Copier les fichiers
			// - Appeler le logger (via la DLL)
			// - Mettre à jour RealTimeState

			if (job == null) throw new ArgumentNullException(nameof(job));
			if (string.IsNullOrWhiteSpace(job.SourceDir)) throw new ArgumentException("SourceDir vide");
			if (string.IsNullOrWhiteSpace(job.TargetDir)) throw new ArgumentException("TargetDir null");

			// Vérifications répertoires
			if (!Directory.Exists(job.SourceDir))
				throw new DirectoryNotFoundException($"Source introuvable : {job.SourceDir}");

			Directory.CreateDirectory(job.TargetDir);

			// Mesure temps
			var sw = Stopwatch.StartNew();

			// Copie selon type
			if (job.Type == JobType.Full)
			{
				CopyDirectoryFull(job);
			}
			else if (job.Type == JobType.Differential)
			{
				CopyDirectoryDifferential(job);
			}
			else
			{
				throw new NotSupportedException($"Type de job non supporté : {job.Type}");
			}

			sw.Stop();

			var entry = new LogEntry
			{
				Timestamp = DateTime.Now,
				JobName = job.Name,
				SourcePath = job.SourceDir,
				TargetPath = job.TargetDir,
				FileSize = GetDirectorySize(job.TargetDir),
				TransferTime = (int)sw.ElapsedMilliseconds
			};

			_logger.SaveLog(entry);
		}



		// Règle de sauvegarde différentielle
		public bool CheckDifferential(string file)
		{
			// TODO :
			// - Comparer le fichier source et cible
			// - Retourner true si le fichier doit être copié

			if (string.IsNullOrWhiteSpace(file)) return false;

			if (!File.Exists(file)) return false;

		
			return true;
		}

		// Helpers privés

		private void CopyDirectoryFull(BackupJob job)
		{
			foreach (var sourceFile in Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories))
			{
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(job.TargetDir, relativePath);

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile);
			}
		}

		private void CopyDirectoryDifferential(BackupJob job)
		{
			foreach (var sourceFile in Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories))
			{
				var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
				var targetFile = Path.Combine(job.TargetDir, relativePath);

				bool shouldCopy =
					!File.Exists(targetFile) ||
					File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(targetFile) ||
					new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length;

				if (!shouldCopy) continue;

				Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
				CopyFileWithTiming(job, sourceFile, targetFile);

			}
		}

		private void CopyFileWithTiming(BackupJob job, string sourceFile, string targetFile)
		{
			var sw = Stopwatch.StartNew();
			long fileSize = new FileInfo(sourceFile).Length;

			File.Copy(sourceFile, targetFile, overwrite: true);

			sw.Stop();

			var entry = new LogEntry
			{
				Timestamp = DateTime.Now,
				JobName = job.Name,
				SourcePath = sourceFile,
				TargetPath = targetFile,
				fileSize = fileSize, 
				TransferTime = (int)sw.ElapsedMilliseconds
			};
			_logger.SaveLog(entry);
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
