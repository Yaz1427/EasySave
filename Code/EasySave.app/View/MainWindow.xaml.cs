using System.Windows;
using System.Windows.Controls;
using EasySave.Models;
using EasySave.ViewModel;

namespace EasySave.View
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // Set language combos (header + settings)
            int langIdx = vm.SettingsLanguage switch { "fr" => 1, "kab" => 2, _ => 0 };
            LanguageComboBox.SelectedIndex = langIdx;
            SettingsLanguageComboBox.SelectedIndex = langIdx;

            // Set log format combo
            if (vm.SettingsLogFormat == "XML")
                LogFormatComboBox.SelectedIndex = 1;
            else
                LogFormatComboBox.SelectedIndex = 0;

            // Set encryption mode combo
            EncryptionModeComboBox.SelectedIndex = vm.SettingsEncryptionMode == "XOR" ? 1 : 0;
        }

        private void BackupTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var combo = sender as ComboBox;
            if (combo?.SelectedIndex == 1)
                vm.NewJobType = JobType.Differential;
            else
                vm.NewJobType = JobType.Full;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var combo = sender as ComboBox;
            var selected = combo?.SelectedItem as ComboBoxItem;
            if (selected?.Tag is string lang)
            {
                vm.SettingsLanguage = lang;
                App.SetLanguage(lang);

                // Sync both language combos
                int idx = lang switch { "fr" => 1, "kab" => 2, _ => 0 };
                if (LanguageComboBox.SelectedIndex != idx)
                    LanguageComboBox.SelectedIndex = idx;
                if (SettingsLanguageComboBox.SelectedIndex != idx)
                    SettingsLanguageComboBox.SelectedIndex = idx;
            }
        }

        private void LogFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var combo = sender as ComboBox;
            var selected = combo?.SelectedItem as ComboBoxItem;
            if (selected?.Tag is string format)
            {
                vm.SettingsLogFormat = format;
            }
        }

        private void EncryptionModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var combo = sender as ComboBox;
            var selected = combo?.SelectedItem as ComboBoxItem;
            if (selected?.Tag is string mode)
            {
                vm.SettingsEncryptionMode = mode;
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.SelectedJob == null) return;

            // Switch to the Create/Edit tab (index 1)
            MainTabControl.SelectedIndex = 1;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to the Create/Edit tab (index 1)
            MainTabControl.SelectedIndex = 1;
        }
    }
}
