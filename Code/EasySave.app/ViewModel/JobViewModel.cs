using System;
using System.Threading;
using System.Windows.Input;
using EasySave.Models;

namespace EasySave.ViewModel
{
    public enum JobState
    {
        Pending,
        Running,
        Paused,
        Stopped,
        Completed,
        Error
    }

    public class JobViewModel : ViewModelBase
    {
        private readonly BackupJob _job;
        private JobState _state = JobState.Pending;
        private double _progress;
        private string _currentFile = string.Empty;
        private string _statusText = string.Empty;
        private int _totalFiles;
        private int _remainingFiles;
        private long _totalSize;
        private long _remainingSize;

        // Per-job pause control
        private readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);
        private CancellationTokenSource? _cts;

        public JobViewModel(BackupJob job)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));
            _statusText = "Pending";

            PauseCommand = new RelayCommand(_ => Pause(), _ => State == JobState.Running);
            ResumeCommand = new RelayCommand(_ => Resume(), _ => State == JobState.Paused);
            StopCommand = new RelayCommand(_ => Stop(), _ => State == JobState.Running || State == JobState.Paused);
        }

        public BackupJob Job => _job;
        public string Name => _job.Name;
        public string SourceDir => _job.SourceDir;
        public string TargetDir => _job.TargetDir;
        public JobType Type => _job.Type;

        public ManualResetEventSlim PauseEvent => _pauseEvent;

        public CancellationTokenSource? CancellationSource
        {
            get => _cts;
            set => _cts = value;
        }

        public JobState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    StatusText = value.ToString();
                    OnPropertyChanged(nameof(IsRunningOrPaused));
                    OnPropertyChanged(nameof(CanStart));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set => SetProperty(ref _totalFiles, value);
        }

        public int RemainingFiles
        {
            get => _remainingFiles;
            set => SetProperty(ref _remainingFiles, value);
        }

        public long TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        public long RemainingSize
        {
            get => _remainingSize;
            set => SetProperty(ref _remainingSize, value);
        }

        public bool IsRunningOrPaused => State == JobState.Running || State == JobState.Paused;
        public bool CanStart => State == JobState.Pending || State == JobState.Stopped || State == JobState.Completed || State == JobState.Error;

        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand StopCommand { get; }

        public void Pause()
        {
            if (State == JobState.Running)
            {
                _pauseEvent.Reset();
                State = JobState.Paused;
            }
        }

        public void Resume()
        {
            if (State == JobState.Paused)
            {
                State = JobState.Running;
                _pauseEvent.Set();
            }
        }

        public void Stop()
        {
            if (State == JobState.Running || State == JobState.Paused)
            {
                _cts?.Cancel();
                State = JobState.Stopped;
                // Unblock pause so the thread can exit
                _pauseEvent.Set();
            }
        }

        public void Reset()
        {
            State = JobState.Pending;
            Progress = 0;
            CurrentFile = string.Empty;
            TotalFiles = 0;
            RemainingFiles = 0;
            TotalSize = 0;
            RemainingSize = 0;
            _pauseEvent.Set();
        }
    }
}
