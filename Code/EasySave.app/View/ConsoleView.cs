using System;
using EasySave.ViewModel;

namespace EasySave.View
{
    /// <summary>
    /// ConsoleView : gère l'affichage console et la saisie utilisateur.
    /// Elle appelle le ViewModel, sans logique métier.
    /// </summary>
    public class ConsoleView
    {
        // Conforme au diagramme UML : attribut _viewModel
        private MainViewModel _viewModel;

        // Langue choisie : "fr" ou "en"
        private string _language = "fr";

        /// <summary>
        /// Constructeur : reçoit le ViewModel depuis Program.cs
        /// </summary>
        public ConsoleView(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            ChooseLanguage(); // Choix de la langue au démarrage de la vue
        }

        /// <summary>
        /// Propose à l'utilisateur de choisir la langue d'affichage (FR/EN).
        /// </summary>
        private void ChooseLanguage()
        {
            Console.WriteLine("Choose language / Choisir la langue :");
            Console.WriteLine("1 - Français");
            Console.WriteLine("2 - English");
            Console.Write("> ");

            string choice = Console.ReadLine();

            if (choice == "2")
                _language = "en";
            else
                _language = "fr";

            Console.Clear();
        }

        /// <summary>
        /// Affiche le menu principal
        /// </summary>
        public void ShowMenu()
        {
            Console.WriteLine("====================================");
            Console.WriteLine("              EasySave              ");
            Console.WriteLine("====================================");

            if (_language == "fr")
            {
                Console.WriteLine("Commandes disponibles :");
                Console.WriteLine("  help                -> Afficher le menu");
                Console.WriteLine("  run <index>          -> Exécuter un job (ex: run 0)");
                Console.WriteLine("  runall              -> Exécuter tous les jobs");
                Console.WriteLine("  exit                -> Quitter l'application");
            }
            else
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  help                -> Show the menu");
                Console.WriteLine("  run <index>          -> Run a job (ex: run 0)");
                Console.WriteLine("  runall              -> Run all jobs");
                Console.WriteLine("  exit                -> Exit the application");
            }

            Console.WriteLine("====================================");
        }

        /// <summary>
        /// Traite la commande saisie par l'utilisateur
        /// </summary>
        public void ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "help":
                    ShowMenu();
                    break;

                case "run":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine(_language == "fr"
                            ? "Usage : run <index> (ex: run 0)"
                            : "Usage: run <index> (ex: run 0)");
                        return;
                    }

                    if (int.TryParse(parts[1], out int index))
                    {
                        _viewModel.ExecuteJob(index);

                        Console.WriteLine(_language == "fr"
                            ? $"Job {index} exécuté."
                            : $"Job {index} executed.");
                    }
                    else
                    {
                        Console.WriteLine(_language == "fr"
                            ? "Index invalide."
                            : "Invalid index.");
                    }
                    break;

                case "runall":
                    _viewModel.ExecuteAllJobs();

                    Console.WriteLine(_language == "fr"
                        ? "Tous les jobs ont été exécutés."
                        : "All jobs have been executed.");
                    break;

                case "exit":
                    // L'arrêt de la boucle se fait dans Program.cs, ici on informe seulement.
                    Console.WriteLine(_language == "fr"
                        ? "Fermeture demandée..."
                        : "Exit requested...");
                    break;

                default:
                    Console.WriteLine(_language == "fr"
                        ? "Commande inconnue. Tape 'help' pour voir les commandes."
                        : "Unknown command. Type 'help' to see the commands.");
                    break;
            }
        }
    }
}
