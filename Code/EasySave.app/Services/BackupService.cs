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
        // Dépendances
        private readonly LoggerService _logger;
        private readonly RealTimeStateService _realTimeStateService;

        // v3.0 - Gestion globale des fichiers prioritaires (entre tous les jobs parallèles)
        private static int _globalPriorityRemaining = 0;
        private static readonly ManualResetEventSlim _noPriorityPendingEvent = new(true);

        // v3.0 - Extensions prioritaires (paramètres généraux)
        private List<string> _priorityExtensions;

        // Constructeur
        public BackupService(LogFormat logFormat = LogFormat.JSON, List<string>? priorityExtensions = null)
        {
            string logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Logs");
            _logger = new LoggerService(logsDir, logFormat);
            _realTimeStateService = new RealTimeStateService();
            _priorityExtensions = priorityExtensions ?? new List<string>();
        }

        public void SetLogFormat(LogFormat format)
        {
            _logger.SetLogFormat(format);
        }

        public LogFormat GetLogFormat()
        {
            return _logger.GetLogFormat();
        }

        public void SetPriorityExtensions(List<string> extensions)
        {
            _priorityExtensions = extensions ?? new List<string>();
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

            // Mesure temps
            var sw = Stopwatch.StartNew();

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
            // Pre-compute file sizes to avoid O(n²) disk reads
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

            // v3.0 - On annonce globalement qu'il y a des prioritaires à traiter
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
                    var sourceFile = allFiles[idx];
                    var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                    var targetFile = Path.Combine(job.TargetDir, relativePath);

                    state.CurrentSourceFile = sourceFile;
                    state.CurrentTargetFile = targetFile;
                    state.LastActionTimestamp = DateTime.Now;
                    _realTimeStateService.UpdateState(state);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                    CopyFileWithTiming(job, sourceFile, targetFile, state);

                    priorityProcessed++;

                    // v3.0 - Un prioritaire terminé => décrément global
                    if (Interlocked.Decrement(ref _globalPriorityRemaining) == 0)
                        _noPriorityPendingEvent.Set();

                    remainingSize -= fileSizes[idx];
                    processedFiles++;
                    state.Progress = (double)processedFiles / allFiles.Count * 100;
                    state.RemainingFiles = allFiles.Count - processedFiles;
                    state.RemainingSize = remainingSize;
                    _realTimeStateService.UpdateState(state);
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
                // v3.0 - Tant qu'il reste au moins 1 prioritaire dans n'importe quel job => on attend
                _noPriorityPendingEvent.Wait();

                var sourceFile = allFiles[idx];
                var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                var targetFile = Path.Combine(job.TargetDir, relativePath);

                state.CurrentSourceFile = sourceFile;
                state.CurrentTargetFile = targetFile;
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                CopyFileWithTiming(job, sourceFile, targetFile, state);

                remainingSize -= fileSizes[idx];
                processedFiles++;
                state.Progress = (double)processedFiles / allFiles.Count * 100;
                state.RemainingFiles = allFiles.Count - processedFiles;
                state.RemainingSize = remainingSize;
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

            // Pre-compute file sizes
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

            // v3.0 - On annonce globalement qu'il y a des prioritaires à traiter
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
                    var sourceFile = filesToCopy[idx];
                    var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                    var targetFile = Path.Combine(job.TargetDir, relativePath);

                    state.CurrentSourceFile = sourceFile;
                    state.CurrentTargetFile = targetFile;
                    state.LastActionTimestamp = DateTime.Now;
                    _realTimeStateService.UpdateState(state);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                    CopyFileWithTiming(job, sourceFile, targetFile, state);

                    priorityProcessed++;

                    if (Interlocked.Decrement(ref _globalPriorityRemaining) == 0)
                        _noPriorityPendingEvent.Set();

                    remainingSize -= fileSizes[idx];
                    processedFiles++;
                    state.Progress = (double)processedFiles / filesToCopy.Count * 100;
                    state.RemainingFiles = filesToCopy.Count - processedFiles;
                    state.RemainingSize = remainingSize;
                    _realTimeStateService.UpdateState(state);
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

            // 2) Ensuite : non prioritaires (attendre la fin globale des prioritaires)
            foreach (int idx in nonPriorityIndices)
            {
                _noPriorityPendingEvent.Wait();

                var sourceFile = filesToCopy[idx];
                var relativePath = Path.GetRelativePath(job.SourceDir, sourceFile);
                var targetFile = Path.Combine(job.TargetDir, relativePath);

                state.CurrentSourceFile = sourceFile;
                state.CurrentTargetFile = targetFile;
                state.LastActionTimestamp = DateTime.Now;
                _realTimeStateService.UpdateState(state);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                CopyFileWithTiming(job, sourceFile, targetFile, state);

                remainingSize -= fileSizes[idx];
                processedFiles++;
                state.Progress = (double)processedFiles / filesToCopy.Count * 100;
                state.RemainingFiles = filesToCopy.Count - processedFiles;
                state.RemainingSize = remainingSize;
                _realTimeStateService.UpdateState(state);
            }
        }

        private void CopyFileWithTiming(BackupJob job, string sourceFile, string targetFile, RealTimeState state)
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
                    TransferTime = (int)sw.ElapsedMilliseconds
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
                    TransferTime = -(int)sw.ElapsedMilliseconds
                };

                _logger.SaveLog(entry);
                throw;
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
}
