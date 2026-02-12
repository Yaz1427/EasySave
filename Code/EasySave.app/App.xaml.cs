using System;
using System.Windows;
using EasySave.Services;
using EasySave.Models;

namespace EasySave
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load saved language preference
            var configService = new ConfigService();
            var settings = configService.LoadSettings();
            SetLanguage(settings.Language);

            // Command line support (same as v1.0)
            if (e.Args.Length > 0)
            {
                ExecuteCommandLine(e.Args[0]);
                Shutdown();
                return;
            }
        }

        public static void SetLanguage(string lang)
        {
            var dict = new ResourceDictionary();
            string source = lang switch
            {
                "fr" => "Resources/Lang_fr.xaml",
                "kab" => "Resources/Lang_kab.xaml",
                _ => "Resources/Lang_en.xaml"
            };

            dict.Source = new Uri(source, UriKind.Relative);

            // Replace the merged dictionary
            if (Current.Resources.MergedDictionaries.Count > 0)
                Current.Resources.MergedDictionaries[0] = dict;
            else
                Current.Resources.MergedDictionaries.Add(dict);
        }

        private void ExecuteCommandLine(string arg)
        {
            try
            {
                var vm = new ViewModel.MainViewModel();

                if (arg.Contains('-'))
                {
                    string[] range = arg.Split('-');
                    int start = int.Parse(range[0]);
                    int end = int.Parse(range[1]);
                    for (int i = start; i <= end; i++) vm.ExecuteJob(i);
                }
                else if (arg.Contains(';'))
                {
                    string[] list = arg.Split(';');
                    foreach (var item in list) vm.ExecuteJob(int.Parse(item));
                }
                else
                {
                    vm.ExecuteJob(int.Parse(arg));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing arguments: {ex.Message}", "EasySave",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
