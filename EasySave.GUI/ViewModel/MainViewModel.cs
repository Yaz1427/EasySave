using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;
using EasySave.GUI.Commands;
using EasySave.Models;
using EasySave.Services;
using System.Globalization;
using System.Threading;
using EasySave.GUI.Models;
using System.Linq;



namespace EasySave.GUI.ViewModel
{
	public class MainViewModel : INotifyPropertyChanged
	{

		private readonly BackupService _backupService;

		public ObservableCollection<BackupJob> Jobs { get; } = new();

		private BackupJob? _selectedJob;
		public BackupJob? SelectedJob
		{
			get => _selectedJob;
			set
			{
				_selectedJob = value;
				OnPropertyChanged();
				RefreshCommands();
			}
		}

		private string _statusText = EasySave.GUI.Resources.Strings.Ready;
		public string StatusText
		{
			get => _statusText;
			set
			{
				_statusText = value;
				OnPropertyChanged();
			}
		}

		public RelayCommand AddJobCommand { get; }
		public RelayCommand DeleteJobCommand { get; }
		public RelayCommand RunSelectedCommand { get; }
		public RelayCommand RunAllCommand { get; }


		private bool _isInitializing = true;

		public MainViewModel()
		{

			_backupService = new BackupService();

			AddJobCommand = new RelayCommand(AddJob);
			DeleteJobCommand = new RelayCommand(DeleteJob, () => SelectedJob != null);
			RunSelectedCommand = new RelayCommand(RunSelected, () => SelectedJob != null);
			RunAllCommand = new RelayCommand(RunAll, () => Jobs.Count > 0);

			var currentCode = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;

			SelectedLanguage = Languages
				.FirstOrDefault(l => l.Code == currentCode)
				?? Languages.First(l => l.Code == "en");

			_isInitializing = false;


		}

		public Array JobTypes => Enum.GetValues(typeof(JobType));


		private void AddJob()
		{
			Jobs.Add(new BackupJob
			{
				Name = "NewJob",
				SourceDir = "",
				TargetDir = "",
				Type = JobType.Full
			});

			StatusText = EasySave.GUI.Resources.Strings.JobAdded;
			RefreshCommands();
		}

		private void DeleteJob()
		{
			if (SelectedJob == null) return;

			Jobs.Remove(SelectedJob);
			SelectedJob = null;
			StatusText = EasySave.GUI.Resources.Strings.JobDeleted;
			RefreshCommands();
		}

		private void RunSelected()
		{
			if (SelectedJob == null) return;

			try
			{
				StatusText = string.Format(
					EasySave.GUI.Resources.Strings.RunningJob,
					SelectedJob.Name);
				_backupService.ExecuteBackup(SelectedJob);
				StatusText = string.Format(
					EasySave.GUI.Resources.Strings.FinishedJob,
					SelectedJob.Name);
			}
			catch (Exception ex)
			{
				StatusText = EasySave.GUI.Resources.Strings.ErrorBackup;
				MessageBox.Show(ex.Message, "Backup error");
			}
		}

		private void RunAll()
		{
			try
			{
				StatusText = EasySave.GUI.Resources.Strings.RunningAll;
				foreach (var job in Jobs)
				{
					_backupService.ExecuteBackup(job);
				}

				StatusText = EasySave.GUI.Resources.Strings.AllFinished;
			}
			catch (Exception ex)
			{
				StatusText = EasySave.GUI.Resources.Strings.ErrorBackup;
				MessageBox.Show(ex.Message, "Backup error");
			}
		}

		private void RefreshCommands()
		{
			DeleteJobCommand.RaiseCanExecuteChanged();
			RunSelectedCommand.RaiseCanExecuteChanged();
			RunAllCommand.RaiseCanExecuteChanged();
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		private LanguageItem? _selectedLanguage;

		public LanguageItem? SelectedLanguage
		{
			get => _selectedLanguage;
			set
			{
				if (_selectedLanguage == value) return;
				_selectedLanguage = value;
				OnPropertyChanged();

				if (_isInitializing) return; 

				if (value != null)
					ChangeLanguage(value.Code);
			}
		}




		public ObservableCollection<LanguageItem> Languages { get; } = new()
		{
			new LanguageItem { Code = "fr", DisplayName = "Français" },
			new LanguageItem { Code = "en", DisplayName = "English" }
		};


		private void ChangeLanguage(string lang)
		{
			if (Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == lang)
				return;

			Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
			Thread.CurrentThread.CurrentCulture = new CultureInfo(lang);

			var old = Application.Current.MainWindow;

			var newWindow = new MainWindow();
			Application.Current.MainWindow = newWindow;
			newWindow.Show();

			old?.Close();
		}



	}
}
