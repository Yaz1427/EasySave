using System;
using EasySave.ViewModel;
using EasySave.Models;

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
            Console.WriteLine("================================");
            Console.WriteLine("          EasySave              ");
            Console.WriteLine("================================");

            if (_language == "fr")
            {
                Console.WriteLine("Commandes disponibles :");
                Console.WriteLine("  help                -> Afficher le menu");
                Console.WriteLine("  run <index>          -> Exécuter un job (ex: run 0)");
                Console.WriteLine("  runall              -> Exécuter tous les jobs");
                Console.WriteLine("  create              -> Créer un nouveau job de sauvegarde");
                Console.WriteLine("  list                -> Lister tous les jobs de sauvegarde");
                Console.WriteLine("  delete <index>      -> Supprimer un job (ex: delete 0)");
                Console.WriteLine("  exit                -> Quitter l'application");
            }
            else
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  help                -> Show the menu");
                Console.WriteLine("  run <index>          -> Run a job (ex: run 0)");
                Console.WriteLine("  runall              -> Run all jobs");
                Console.WriteLine("  create              -> Create a new backup job");
                Console.WriteLine("  list                -> List all backup jobs");
                Console.WriteLine("  delete <index>      -> Delete a job (ex: delete 0)");
                Console.WriteLine("  exit                -> Exit the application");
            }

            Console.WriteLine("================================");
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

                case "create":
                    CreateNewJob();
                    break;

                case "list":
                    ListAllJobs();
                    break;

                case "delete":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine(_language == "fr"
                            ? "Usage : delete <index> (ex: delete 0)"
                            : "Usage: delete <index> (ex: delete 0)");
                        return;
                    }

                    if (int.TryParse(parts[1], out int deleteIndex))
                    {
                        bool success = _viewModel.DeleteJob(deleteIndex);
                        Console.WriteLine(_language == "fr"
                            ? success ? $"Job {deleteIndex} supprimé avec succès." : $"Impossible de supprimer le job {deleteIndex}."
                            : success ? $"Job {deleteIndex} deleted successfully." : $"Cannot delete job {deleteIndex}.");
                    }
                    else
                    {
                        Console.WriteLine(_language == "fr"
                            ? "Index invalide."
                            : "Invalid index.");
                    }
                    break;

                case "exit":
                    // L'arrêt de la boucle se fait dans Program.cs, ici on informe seulement.
                    Console.WriteLine(_language == "fr"
                        ? "Fermeture demandée..."
                        : "Exit requested...");
                    break;

                default:
                    Console.WriteLine(_language == "fr"
                        ? $"Commande inconnue : {command}. Tapez 'help' pour voir les commandes disponibles."
                        : $"Unknown command: {command}. Type 'help' to see available commands.");
                    break;
            }
        }

        /// <summary>
        /// Guide l'utilisateur pour créer un nouveau job de sauvegarde
        /// </summary>
        private void CreateNewJob()
        {
            Console.WriteLine(_language == "fr"
                ? "=== Création d'un nouveau job de sauvegarde ==="
                : "=== Create a new backup job ===");

            // Nom du job
            Console.Write(_language == "fr"
                ? "Nom du job : "
                : "Job name: ");
            string name = Console.ReadLine();

            // Répertoire source
            Console.Write(_language == "fr"
                ? "Répertoire source : "
                : "Source directory: ");
            string sourceDir = Console.ReadLine();

            // Répertoire cible
            Console.Write(_language == "fr"
                ? "Répertoire cible : "
                : "Target directory: ");
            string targetDir = Console.ReadLine();

            // Type de sauvegarde
            Console.WriteLine(_language == "fr"
                ? "Type de sauvegarde :"
                : "Backup type:");
            Console.WriteLine("1 - Full");
            Console.WriteLine("2 - Differential");
            Console.Write(_language == "fr"
                ? "Choix (1-2) : "
                : "Choice (1-2): ");

            JobType type = JobType.Full;
            string typeChoice = Console.ReadLine();
            if (typeChoice == "2")
                type = JobType.Differential;

            // Création du job via le ViewModel
            bool success = _viewModel.CreateJob(name, sourceDir, targetDir, type);

            if (success)
            {
                Console.WriteLine(_language == "fr"
                    ? $"Job '{name}' créé avec succès !"
                    : $"Job '{name}' created successfully!");
            }
            else
            {
                Console.WriteLine(_language == "fr"
                    ? "Erreur lors de la création du job. Vérifiez les informations et que vous n'avez pas dépassé la limite de 5 jobs."
                    : "Error creating job. Check the information and ensure you haven't exceeded the 5 job limit.");
            }
        }

        /// <summary>
        /// Affiche tous les travaux de sauvegarde existants
        /// </summary>
        private void ListAllJobs()
        {
            var jobs = _viewModel.GetAllJobs();
            
            Console.WriteLine(_language == "fr"
                ? "=== Liste des travaux de sauvegarde ==="
                : "=== List of backup jobs ===");

            if (jobs.Count == 0)
            {
                Console.WriteLine(_language == "fr"
                    ? "Aucun travail de sauvegarde configuré."
                    : "No backup jobs configured.");
                return;
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                Console.WriteLine($"[{i}] {job.Name}");
                Console.WriteLine($"    {(_language == "fr" ? "Source" : "Source")}: {job.SourceDir}");
                Console.WriteLine($"    {(_language == "fr" ? "Cible" : "Target")}: {job.TargetDir}");
                Console.WriteLine($"    {(_language == "fr" ? "Type" : "Type")}: {job.Type}");
                Console.WriteLine();
            }
        }
    }
}
