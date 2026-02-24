using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EasySave.Models
{
    public enum JobType
    {
        Full,
        Differential
    }

    public class BackupJob : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string SourceDir { get; set; } = string.Empty;
        public string TargetDir { get; set; } = string.Empty;
        public JobType Type { get; set; }

        public volatile bool IsPaused;
        public volatile bool IsStopped;

        public string Status { get; set; } = "Idle"; // Idle / Running / Paused / Stopped / End

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

