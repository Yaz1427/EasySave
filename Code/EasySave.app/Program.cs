//Program.cs

using System;
using EasySave.ViewModel;
using EasySave.View;
using EasySave.Models;
using EasySave.Services;
using EasyLog.Services;

namespace EasySave.App
{
    class Program
    {
        static void Main(string[] args)
        {
            // Test des services (optionnel - commenter pour la production)
            // TestServices.RunTests();
            
            // 1. Initialisation du ViewModel (Le cerveau)
            MainViewModel viewModel = new MainViewModel();

            // 2. Gestion des arguments de ligne de commande (Exigence ProSoft)
            // Exemple : EasySave.exe 1-3 ou 1;3
            if (args.Length > 0)
            {
                ExecuteCommandLineArgs(args[0], viewModel);
                return; // On quitte après l'exécution en ligne de commande
            }

            // 3. Initialisation de la Vue (L'interface de Nawfel)
            // On lui donne le viewModel pour qu'elle puisse lui envoyer des ordres
            ConsoleView view = new ConsoleView(viewModel);

            // 4. Boucle principale de l'application (Mode interactif)
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
                        // On laisse la vue traiter la commande (run, runall, help, etc.)
                        view.ProcessCommand(input);
                    }
                }
            }
        }

        /// <summary>
        /// Analyse et exécute les commandes passées directement au .exe
        /// </summary>
        private static void ExecuteCommandLineArgs(string arg, MainViewModel vm)
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
