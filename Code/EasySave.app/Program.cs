//Program.cs

using System;
using EasySave.ViewModel;
using EasySave.View;
using EasySave.Models;
using EasySave.Services;
using EasyLog.Services;

namespace EasySave.CLI
{
    // Note : Le point d'entrée principal est App.xaml.cs (WPF).
    // Cette classe est conservée pour un éventuel mode console autonome.
    class Program
    {
        // Le Main est désactivé car WPF génère son propre point d'entrée via App.xaml.
        // Pour réactiver le mode console, changer OutputType en Exe et décommenter.
        /*
        static void Main(string[] args)
        {
            MainViewModel viewModel = new MainViewModel();

            if (args.Length > 0)
            {
                ExecuteCommandLineArgs(args[0], viewModel);
                return;
            }

            ConsoleView view = new ConsoleView(viewModel);

            bool running = true;
            view.ShowMenu();

            while (running)
            {
                Console.Write("\n> ");
                string input = Console.ReadLine();

                if (!string.IsNullOrEmpty(input))
                {
                    if (input.ToLower() == "exit")
                    {
                        running = false;
                    }
                    else
                    {
                        view.ProcessCommand(input);
                    }
                }
            }
        }
        */

        /// <summary>
        /// Analyse et exécute les commandes passées directement au .exe
        /// </summary>
        public static void ExecuteCommandLineArgs(string arg, MainViewModel vm)
        {
            try
            {
                // Gestion du format 1-3 (plage)
                if (arg.Contains('-'))
                {
                    string[] range = arg.Split('-');
                    int start = int.Parse(range[0]);
                    int end = int.Parse(range[1]);
                    for (int i = start; i <= end; i++) vm.ExecuteJob(i);
                }
                // Gestion du format 1;3 (liste)
                else if (arg.Contains(';'))
                {
                    string[] list = arg.Split(';');
                    foreach (var item in list) vm.ExecuteJob(int.Parse(item));
                }
                // Gestion d'un index unique
                else
                {
                    vm.ExecuteJob(int.Parse(arg));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing arguments: {ex.Message}");
            }
        }
    }
}
