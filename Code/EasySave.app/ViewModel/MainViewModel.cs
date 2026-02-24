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
        private BackupEngine _backupEngine;
        private AppSettings _settings;

        private ObservableCollection<BackupJob> _jobs;
        private BackupJob? _selectedJob;
        private string _statusMessage = string.Empty;
        private bool _isRunning;
        private double _currentProgress;
        private string _currentJobName = string.Empty;
        private bool _businessSoftwareDetected;

        // v3.0: Running job view models
        private ObservableCollection<JobViewModel> _runningJobs = new();

        // Form fields for creating a new job
        private string _newJobName = string.Empty;
        private string _newJobSourceDir = string.Empty;
        private string _newJobTargetDir = string.Empty;
        private JobType _newJobType = JobType.Full;
        private bool _isEditing;
        private BackupJob? _editingJob;

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
        private string _settingsPriorityExtensions = string.Empty;
        private string _settingsMaxLargeFileSizeKB = "1024";
        private string _settingsLogDestination = "Local";
        private string _settingsLogServerUrl = string.Empty;

        public MainViewModel()
        {
            _configService = new ConfigService();
            _settings = _configService.LoadSettings();
            _backupEngine = new BackupEngine(_settings);

            _backupEngine.StatusChanged += msg =>
                Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
            _backupEngine.BusinessSoftwareStateChanged += detected =>
                Application.Current.Dispatcher.Invoke(() => BusinessSoftwareDetected = detected);

            var jobsList = _configService.LoadJobs();
            _jobs = new ObservableCollection<BackupJob>(jobsList);

            // Load settings into fields
            _settingsLanguage = _settings.Language;
            _settingsLogFormat = _settings.LogFormat;
            _settingsEncryptExtensions = string.Join("; ", _settings.EncryptExtensions);
            _settingsBusinessSoftware = _settings.BusinessSoftwareProcess;
            _settingsEncryptionMode = _settings.EncryptionMode;
            _settingsPriorityExtensions = string.Join("; ", _settings.PriorityExtensions);
            _settingsMaxLargeFileSizeKB = _settings.MaxLargeFileSizeKB.ToString();
            _settingsLogDestination = _settings.LogDestination;
            _settingsLogServerUrl = _settings.LogServerUrl;

            // Commands
            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => CanCreateJob());
            DeleteJobCommand = new RelayCommand(_ => DeleteJob(), _ => SelectedJob != null && !IsRunning);
            ExecuteJobCommand = new RelayCommand(_ => ExecuteSelectedJob(), _ => SelectedJob != null && !IsRunning);
            ExecuteAllJobsCommand = new RelayCommand(_ => ExecuteAllJobs(), _ => Jobs.Count > 0 && !IsRunning);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            BrowseSourceCommand = new RelayCommand(_ => BrowseFolder(isSource: true));
            BrowseTargetCommand = new RelayCommand(_ => BrowseFolder(isSource: false));
            EditJobCommand = new RelayCommand(_ => StartEditJob(), _ => SelectedJob != null && !IsRunning);
            SaveEditCommand = new RelayCommand(_ => SaveEditJob(), _ => IsEditing && CanCreateJob());
            CancelEditCommand = new RelayCommand(_ => CancelEditJob(), _ => IsEditing);
            RefreshLogsCommand = new RelayCommand(_ => LoadLogFiles());
            OpenLogsFolderCommand = new RelayCommand(_ => OpenLogsFolder());
            StopBackupCommand = new RelayCommand(_ => StopAllBackups(), _ => IsRunning);
            PauseAllCommand = new RelayCommand(_ => PauseAll(), _ => IsRunning);
            ResumeAllCommand = new RelayCommand(_ => ResumeAll(), _ => IsRunning);

            LoadLogFiles();
        }

        // --- Properties ---

        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set => SetProperty(ref _jobs, value);
        }

        public BackupJob? SelectedJob
        {
            get => _selectedJob;
            set => SetProperty(ref _selectedJob, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
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

        public bool BusinessSoftwareDetected
        {
            get => _businessSoftwareDetected;
            set => SetProperty(ref _businessSoftwareDetected, value);
        }

        // v3.0: Running jobs collection for per-job monitoring
        public ObservableCollection<JobViewModel> RunningJobs
        {
            get => _runningJobs;
            set => SetProperty(ref _runningJobs, value);
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

        public string SettingsPriorityExtensions
        {
            get => _settingsPriorityExtensions;
            set => SetProperty(ref _settingsPriorityExtensions, value);
        }

        public string SettingsMaxLargeFileSizeKB
        {
            get => _settingsMaxLargeFileSizeKB;
            set => SetProperty(ref _settingsMaxLargeFileSizeKB, value);
        }

        public string SettingsLogDestination
        {
            get => _settingsLogDestination;
            set => SetProperty(ref _settingsLogDestination, value);
        }

        public string SettingsLogServerUrl
        {
            get => _settingsLogServerUrl;
            set => SetProperty(ref _settingsLogServerUrl, value);
        }

        // --- Commands ---

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
        public ICommand PauseAllCommand { get; }
        public ICommand ResumeAllCommand { get; }

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

        // --- Methods ---

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

        private async void ExecuteSelectedJob()
        {
            if (SelectedJob == null) return;

            IsRunning = true;
            CurrentJobName = SelectedJob.Name;
            CurrentProgress = 0;
            StatusMessage = $"Running '{SelectedJob.Name}'...";

            var jvm = new JobViewModel(SelectedJob);
            RunningJobs.Clear();
            RunningJobs.Add(jvm);

            // Track progress from job VM
            jvm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JobViewModel.Progress))
                    Application.Current.Dispatcher.Invoke(() => CurrentProgress = jvm.Progress);
            };

            try
            {
                await _backupEngine.RunSingleJobAsync(jvm);

                int completed = RunningJobs.Count(j => j.State == JobState.Completed);
                int failed = RunningJobs.Count(j => j.State == JobState.Error || j.State == JobState.Stopped);
                StatusMessage = $"Completed: {completed} succeeded, {failed} failed.";
                CurrentProgress = 100;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async void ExecuteAllJobs()
        {
            if (Jobs.Count == 0) return;

            IsRunning = true;
            CurrentProgress = 0;
            StatusMessage = $"Running {Jobs.Count} jobs in parallel...";

            RunningJobs.Clear();
            var jobViewModels = new List<JobViewModel>();
            foreach (var job in Jobs)
            {
                var jvm = new JobViewModel(job);
                jobViewModels.Add(jvm);
                RunningJobs.Add(jvm);
            }

            // Update global progress from all jobs
            foreach (var jvm in jobViewModels)
            {
                jvm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(JobViewModel.Progress))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (RunningJobs.Count > 0)
                                CurrentProgress = RunningJobs.Average(j => j.Progress);
                        });
                    }
                };
            }

            try
            {
                await _backupEngine.RunJobsInParallel(jobViewModels);

                int completed = RunningJobs.Count(j => j.State == JobState.Completed);
                int failed = RunningJobs.Count(j => j.State == JobState.Error || j.State == JobState.Stopped);
                StatusMessage = $"Completed: {completed} succeeded, {failed} failed.";
                CurrentProgress = 100;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void StopAllBackups()
        {
            _backupEngine.StopAll();
            StatusMessage = "Stopping all jobs...";
        }

        private void PauseAll()
        {
            _backupEngine.PauseAll();
            StatusMessage = "All jobs paused.";
        }

        private void ResumeAll()
        {
            _backupEngine.ResumeAll();
            StatusMessage = "All jobs resumed.";
        }

        private void SaveSettings()
        {
            _settings.Language = SettingsLanguage;
            _settings.LogFormat = SettingsLogFormat;
            _settings.BusinessSoftwareProcess = SettingsBusinessSoftware?.Trim() ?? string.Empty;
            _settings.EncryptionMode = SettingsEncryptionMode;

            // Parse encrypt extensions
            _settings.EncryptExtensions = string.IsNullOrWhiteSpace(SettingsEncryptExtensions)
                ? new List<string>()
                : SettingsEncryptExtensions.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToList();

            // v3.0: Parse priority extensions
            _settings.PriorityExtensions = string.IsNullOrWhiteSpace(SettingsPriorityExtensions)
                ? new List<string>()
                : SettingsPriorityExtensions.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToList();

            // v3.0: Max large file size
            if (long.TryParse(SettingsMaxLargeFileSizeKB, out long maxSize) && maxSize > 0)
                _settings.MaxLargeFileSizeKB = maxSize;

            // v3.0: Log destination
            _settings.LogDestination = SettingsLogDestination;
            _settings.LogServerUrl = SettingsLogServerUrl?.Trim() ?? string.Empty;

            _configService.SaveSettings(_settings);
            _backupEngine.UpdateSettings(_settings);

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
                var jvm = new JobViewModel(Jobs[index]);
                _backupEngine.RunSingleJobAsync(jvm).Wait();
            }
            else
            {
                throw new IndexOutOfRangeException("Invalid backup job index.");
            }
        }

        public void ExecuteAllJobsSync()
        {
            var jvms = Jobs.Select(j => new JobViewModel(j)).ToList();
            _backupEngine.RunJobsInParallel(jvms).Wait();
        }

        public List<BackupJob> GetAllJobs()
        {
            return Jobs.ToList();
        }
    }
}
