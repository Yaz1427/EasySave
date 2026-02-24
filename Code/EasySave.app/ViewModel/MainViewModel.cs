using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using EasyLog.Models;
using EasySave.Models;
using EasySave.Services;

namespace EasySave.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private BackupService _backupService;
        private AppSettings _settings;

        private ObservableCollection<BackupJob> _jobs;
        private BackupJob? _selectedJob;
        private string _statusMessage = string.Empty;
        private bool _isRunning;
        private double _currentProgress;
        private string _currentJobName = string.Empty;

        // Form fields for creating a new job
        private string _newJobName = string.Empty;
        private string _newJobSourceDir = string.Empty;
        private string _newJobTargetDir = string.Empty;
        private JobType _newJobType = JobType.Full;
        private bool _isEditing;
        private BackupJob? _editingJob;
        private CancellationTokenSource? _cts;

        // Limite le nombre de jobs exécutés en parallèle
		private readonly SemaphoreSlim _parallelJobsSemaphore = new SemaphoreSlim(4);

		// Log viewer
		private ObservableCollection<LogEntry> _logEntries = new();
        private ObservableCollection<string> _logFiles = new();
        private string? _selectedLogFile;

        // Settings fields
        private string _settingsLanguage = "en";
        private string _settingsLogFormat = "JSON";
        private string _settingsEncryptExtensions = string.Empty;
        private string _settingsBusinessSoftware = string.Empty;
        private string _settingsEncryptionMode = "AES";

        public MainViewModel()
        {
            _configService = new ConfigService();
            _settings = _configService.LoadSettings();
            _backupService = new BackupService(_settings);

            var jobsList = _configService.LoadJobs();
            _jobs = new ObservableCollection<BackupJob>(jobsList);

            // Load settings into fields
            _settingsLanguage = _settings.Language;
            _settingsLogFormat = _settings.LogFormat;
            _settingsEncryptExtensions = string.Join("; ", _settings.EncryptExtensions);
            _settingsBusinessSoftware = _settings.BusinessSoftwareProcess;
            _settingsEncryptionMode = _settings.EncryptionMode;

            // Commands
            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => CanCreateJob());
            DeleteJobCommand = new RelayCommand(_ => DeleteJob(), _ => SelectedJob != null && !IsRunning);
            ExecuteJobCommand = new RelayCommand(_ => ExecuteSelectedJobs(), _ => SelectedJob != null && !IsRunning);
            ExecuteAllJobsCommand = new RelayCommand(_ => ExecuteAllJobs(), _ => Jobs.Count > 0 && !IsRunning);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            BrowseSourceCommand = new RelayCommand(_ => BrowseFolder(isSource: true));
            BrowseTargetCommand = new RelayCommand(_ => BrowseFolder(isSource: false));
            EditJobCommand = new RelayCommand(_ => StartEditJob(), _ => SelectedJob != null && !IsRunning);
            SaveEditCommand = new RelayCommand(_ => SaveEditJob(), _ => IsEditing && CanCreateJob());
            CancelEditCommand = new RelayCommand(_ => CancelEditJob(), _ => IsEditing);
            RefreshLogsCommand = new RelayCommand(_ => LoadLogFiles());
            OpenLogsFolderCommand = new RelayCommand(_ => OpenLogsFolder());
            // existing stop command (global stop/cancel)
            StopBackupCommand = new RelayCommand(_ => StopBackup(), _ => IsRunning);

            // Play / Pause commands (fix : were missing)
            PlayBackupCommand = new RelayCommand(_ => PlaySelected(), _ => SelectedJob != null && IsRunning);
            PauseBackupCommand = new RelayCommand(_ => PauseSelected(), _ => SelectedJob != null && IsRunning);

			ExecuteSelectedJobsCommand = new RelayCommand(_ => ExecuteSelectedJobs());

			LoadLogFiles();

			// --- Ajout dans le constructeur (après l'initialisation des autres commandes) ---
			PlayJobCommand = new RelayCommand(p => { if (p is BackupJob job) PlayJob(job); });
			PauseJobCommand = new RelayCommand(p => { if (p is BackupJob job) PauseJob(job); });
			StopJobCommand  = new RelayCommand(p => { if (p is BackupJob job) StopJob(job); });
		}

        // Properties

        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set => SetProperty(ref _jobs, value);
        }

        public BackupJob? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (SetProperty(ref _selectedJob, value))
                {
                    // Ask WPF to re-evaluate command can-execute
                    (PlayBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PauseBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (EditJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    // Update commands that depend on IsRunning
                    (StopBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PlayBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PauseBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (EditJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ExecuteJobCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ExecuteAllJobsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public double CurrentProgress
        {
            get => _currentProgress;
            set => SetProperty(ref _currentProgress, value);
        }

        public string CurrentJobName
        {
            get => _currentJobName;
            set => SetProperty(ref _currentJobName, value);
        }

        // New job form
        public string NewJobName
        {
            get => _newJobName;
            set => SetProperty(ref _newJobName, value);
        }

        public string NewJobSourceDir
        {
            get => _newJobSourceDir;
            set => SetProperty(ref _newJobSourceDir, value);
        }

        public string NewJobTargetDir
        {
            get => _newJobTargetDir;
            set => SetProperty(ref _newJobTargetDir, value);
        }

        public JobType NewJobType
        {
            get => _newJobType;
            set => SetProperty(ref _newJobType, value);
        }

        // Settings
        public string SettingsLanguage
        {
            get => _settingsLanguage;
            set => SetProperty(ref _settingsLanguage, value);
        }

        public string SettingsLogFormat
        {
            get => _settingsLogFormat;
            set => SetProperty(ref _settingsLogFormat, value);
        }

        public string SettingsEncryptExtensions
        {
            get => _settingsEncryptExtensions;
            set => SetProperty(ref _settingsEncryptExtensions, value);
        }

        public string SettingsBusinessSoftware
        {
            get => _settingsBusinessSoftware;
            set => SetProperty(ref _settingsBusinessSoftware, value);
        }

        public string SettingsEncryptionMode
        {
            get => _settingsEncryptionMode;
            set => SetProperty(ref _settingsEncryptionMode, value);
        }

        // Commands 

        public ICommand CreateJobCommand { get; }
        public ICommand DeleteJobCommand { get; }
        public ICommand ExecuteJobCommand { get; }
        public ICommand ExecuteAllJobsCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }
        public ICommand EditJobCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand RefreshLogsCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }
        public ICommand StopBackupCommand { get; }

        // Added commands
        public ICommand PauseBackupCommand { get; }
        public ICommand PlayBackupCommand { get; }

		public ICommand ExecuteSelectedJobsCommand { get; }
		public ICommand PlayJobCommand { get; }
		public ICommand PauseJobCommand { get; }
		public ICommand StopJobCommand { get; }

		public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(IsNotEditing));
            }
        }

        public bool IsNotEditing => !IsEditing;

        // Log viewer
        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        public ObservableCollection<string> LogFiles
        {
            get => _logFiles;
            set => SetProperty(ref _logFiles, value);
        }

        public string? SelectedLogFile
        {
            get => _selectedLogFile;
            set
            {
                if (SetProperty(ref _selectedLogFile, value))
                    LoadLogEntries();
            }
        }

        //  Methods

        private bool CanCreateJob()
        {
            return !string.IsNullOrWhiteSpace(NewJobName)
                && !string.IsNullOrWhiteSpace(NewJobSourceDir)
                && !string.IsNullOrWhiteSpace(NewJobTargetDir)
                && !IsRunning;
        }

        private void CreateJob()
        {
            var newJob = new BackupJob
            {
                Name = NewJobName.Trim(),
                SourceDir = NewJobSourceDir.Trim(),
                TargetDir = NewJobTargetDir.Trim(),
                Type = NewJobType
            };

            Jobs.Add(newJob);
            _configService.SaveJobs(Jobs.ToList());

            StatusMessage = $"Job '{newJob.Name}' created.";

            // Clear form
            NewJobName = string.Empty;
            NewJobSourceDir = string.Empty;
            NewJobTargetDir = string.Empty;
            NewJobType = JobType.Full;
        }

        private void StartEditJob()
        {
            if (SelectedJob == null) return;

            _editingJob = SelectedJob;
            NewJobName = SelectedJob.Name;
            NewJobSourceDir = SelectedJob.SourceDir;
            NewJobTargetDir = SelectedJob.TargetDir;
            NewJobType = SelectedJob.Type;
            IsEditing = true;
            StatusMessage = $"Editing '{SelectedJob.Name}'...";
        }

        private void SaveEditJob()
        {
            if (_editingJob == null) return;

            _editingJob.Name = NewJobName.Trim();
            _editingJob.SourceDir = NewJobSourceDir.Trim();
            _editingJob.TargetDir = NewJobTargetDir.Trim();
            _editingJob.Type = NewJobType;

            _configService.SaveJobs(Jobs.ToList());

            // Refresh DataGrid
            int idx = Jobs.IndexOf(_editingJob);
            if (idx >= 0)
            {
                Jobs.RemoveAt(idx);
                Jobs.Insert(idx, _editingJob);
            }

            StatusMessage = $"Job '{_editingJob.Name}' updated.";
            CancelEditJob();
        }

        private void CancelEditJob()
        {
            _editingJob = null;
            IsEditing = false;
            NewJobName = string.Empty;
            NewJobSourceDir = string.Empty;
            NewJobTargetDir = string.Empty;
            NewJobType = JobType.Full;
        }

        private void DeleteJob()
        {
            if (SelectedJob == null) return;

            string name = SelectedJob.Name;
            Jobs.Remove(SelectedJob);
            _configService.SaveJobs(Jobs.ToList());
            SelectedJob = null;
            StatusMessage = $"Job '{name}' deleted.";
        }

		private async void ExecuteSelectedJobs()
		{
			var selectedJobs = Jobs.Where(j => j.IsSelected).ToList();
			if (!selectedJobs.Any()) return;

			IsRunning = true;
			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			int success = 0;
			int failed = 0;

			var tasks = selectedJobs.Select(job => Task.Run(async () =>
			{
				await _parallelJobsSemaphore.WaitAsync(token);
				try
				{
					job.IsStopped = false;
					job.IsPaused = false;
					job.Status = "Running";
					job.Progress = 0;

					var uiProgress = new Progress<double>(p =>
					{
						job.Progress = p;
					});

					_backupService.ExecuteBackup(job, uiProgress, token);

					job.Progress = 100;
					job.Status = "End";
					Interlocked.Increment(ref success);
				}
				catch (OperationCanceledException)
				{
					job.Status = "Stopped";
					Interlocked.Increment(ref failed);
				}
				catch (BusinessSoftwareRunningException)
				{
					job.Status = "Paused - Business software";
					Interlocked.Increment(ref failed);
				}
				catch
				{
					job.Status = "Error";
					Interlocked.Increment(ref failed);
				}
				finally
				{
					_parallelJobsSemaphore.Release();
				}
			}, token)).ToList();

			try
			{
				await Task.WhenAll(tasks);
			}
			finally
			{
				StatusMessage = $"Completed: {success} succeeded, {failed} failed.";
				_cts?.Dispose();
				_cts = null;
				IsRunning = false;
			}
		}

		private async void ExecuteAllJobs()
		{
			if (Jobs.Count == 0) return;

			IsRunning = true;
			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			int success = 0;
			int failed = 0;

			var tasks = Jobs.Select(job => Task.Run(async () =>
			{
				await _parallelJobsSemaphore.WaitAsync(token);
				try
				{
					job.IsStopped = false;
					job.IsPaused = false;
					job.Status = "Running";
					job.Progress = 0;

					var uiProgress = new Progress<double>(p =>
					{
						job.Progress = p;
					});

					_backupService.ExecuteBackup(job, uiProgress, token);

					job.Progress = 100;
					job.Status = "End";
					Interlocked.Increment(ref success);
				}
				catch (OperationCanceledException)
				{
					job.Status = "Stopped";
					Interlocked.Increment(ref failed);
				}
				catch (BusinessSoftwareRunningException)
				{
					job.Status = "Paused - Business software";
					Interlocked.Increment(ref failed);
				}
				catch
				{
					job.Status = "Error";
					Interlocked.Increment(ref failed);
				}
				finally
				{
					_parallelJobsSemaphore.Release();
				}
			}, token)).ToList();

			try
			{
				await Task.WhenAll(tasks);
			}
			finally
			{
				StatusMessage = $"Completed: {success} succeeded, {failed} failed.";
				_cts?.Dispose();
				_cts = null;
				IsRunning = false;
			}
		}

		private void StopBackup()
        {
            _cts?.Cancel();
            StatusMessage = "Stopping...";
        }

        private void SaveSettings()
        {
            _settings.Language = SettingsLanguage;
            _settings.LogFormat = SettingsLogFormat;
            _settings.BusinessSoftwareProcess = SettingsBusinessSoftware?.Trim() ?? string.Empty;
            _settings.EncryptionMode = SettingsEncryptionMode;

            // Parse extensions
            _settings.EncryptExtensions = string.IsNullOrWhiteSpace(SettingsEncryptExtensions)
                ? new List<string>()
                : SettingsEncryptExtensions.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToList();

            _configService.SaveSettings(_settings);
            _backupService.UpdateSettings(_settings);

            StatusMessage = "Settings saved.";
        }

        private void BrowseFolder(bool isSource)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = isSource ? "Select Source Directory" : "Select Target Directory"
            };

            if (dialog.ShowDialog() == true)
            {
                if (isSource)
                    NewJobSourceDir = dialog.FolderName;
                else
                    NewJobTargetDir = dialog.FolderName;
            }
        }

        private void LoadLogFiles()
        {
            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string logsDir = Path.Combine(baseDir, "LogsEasySave");

            LogFiles.Clear();
            LogEntries.Clear();

            if (!Directory.Exists(logsDir)) return;

            var files = Directory.GetFiles(logsDir, "*.*")
                .Where(f => (f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                         && !Path.GetFileName(f).Equals("state.json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f)
                .ToList();

            foreach (var file in files)
                LogFiles.Add(Path.GetFileName(file));

            if (LogFiles.Count > 0)
                SelectedLogFile = LogFiles[0];
        }

        private void LoadLogEntries()
        {
            LogEntries.Clear();
            if (string.IsNullOrEmpty(SelectedLogFile)) return;

            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string logsDir = Path.Combine(baseDir, "LogsEasySave");
            string filePath = Path.Combine(logsDir, SelectedLogFile);

            if (!File.Exists(filePath)) return;

            try
            {
                List<LogEntry>? entries = null;

                if (SelectedLogFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string json = File.ReadAllText(filePath);
                    entries = JsonSerializer.Deserialize<List<LogEntry>>(json);
                }
                else if (SelectedLogFile.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var serializer = new XmlSerializer(typeof(List<LogEntry>));
                    using var reader = new StreamReader(filePath);
                    entries = (List<LogEntry>?)serializer.Deserialize(reader);
                }

                if (entries != null)
                {
                    foreach (var entry in entries)
                        LogEntries.Add(entry);
                }
            }
            catch { }
        }

        private void OpenLogsFolder()
        {
            string baseDir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));
            string logsDir = Path.Combine(baseDir, "LogsEasySave");

            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsDir,
                UseShellExecute = true
            });
        }

        // Command line support (same as v1.0)
        public void ExecuteJob(int index)
        {
            if (index >= 0 && index < Jobs.Count)
            {
                _backupService.ExecuteBackup(Jobs[index]);
            }
            else
            {
                throw new IndexOutOfRangeException("Invalid backup job index.");
            }
        }

        public void ExecuteAllJobsSync()
        {
            foreach (var job in Jobs)
            {
                _backupService.ExecuteBackup(job);
            }
        }

        public List<BackupJob> GetAllJobs()
        {
            return Jobs.ToList();
        }


        private void PauseSelected()
        {
            if (SelectedJob == null) return;
            SelectedJob.IsPaused = true;
            SelectedJob.Status = "Paused";
            StatusMessage = $"Job '{SelectedJob.Name}' paused.";
        }

        private void PlaySelected()
        {
            if (SelectedJob == null) return;
            SelectedJob.IsPaused = false;
            SelectedJob.Status = "Running";
            StatusMessage = $"Job '{SelectedJob.Name}' resumed.";
        }

        private void StopSelected()
        {
            if (SelectedJob == null) return;
            SelectedJob.IsStopped = true;
            SelectedJob.Status = "Stopped";
            StatusMessage = $"Job '{SelectedJob.Name}' stopped.";
        }

        private void PlayJob(BackupJob job)
        {
            if (job == null) return;
            job.IsPaused = false;
            job.IsStopped = false;
            job.Status = "Running";
            StatusMessage = $"Job '{job.Name}' resumed.";
            // Optionnel : démarrer l'exécution si besoin
            // _ = Task.Run(() => _backupService.ExecuteBackup(job));
        }

        private void PauseJob(BackupJob job)
        {
            if (job == null) return;
            job.IsPaused = true;
            job.Status = "Paused";
            StatusMessage = $"Job '{job.Name}' paused.";
        }

        private void StopJob(BackupJob job)
        {
            if (job == null) return;
            job.IsStopped = true;
            job.Status = "Stopped";
            StatusMessage = $"Job '{job.Name}' stopped.";
        }
    }
}
