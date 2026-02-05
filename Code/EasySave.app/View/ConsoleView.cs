using System;
using System.Diagnostics;
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

        // --- Helpers d'affichage ---

        private string Fr(string fr, string en) => _language == "fr" ? fr : en;

        private void WriteColor(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prev;
        }

        private void WriteLineColor(string text, ConsoleColor color)
        {
            WriteColor(text + Environment.NewLine, color);
        }

        private void PrintSeparator()
        {
            WriteLineColor("+-----------------------------------------+", ConsoleColor.DarkGray);
        }

        /// <summary>
        /// Propose à l'utilisateur de choisir la langue d'affichage (FR/EN).
        /// </summary>
        private void ChooseLanguage()
        {
            PrintSeparator();
            WriteLineColor("  Choose language / Choisir la langue", ConsoleColor.Cyan);
            PrintSeparator();
            Console.WriteLine("  1 - Francais");
            Console.WriteLine("  2 - English");
            PrintSeparator();
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
            var jobs = _viewModel.GetAllJobs();

            Console.WriteLine();
            WriteLineColor("+-----------------------------------------+", ConsoleColor.Cyan);
            WriteLineColor("|             EasySave v1.0               |", ConsoleColor.Cyan);
            WriteLineColor("+-----------------------------------------+", ConsoleColor.Cyan);

            WriteColor($"  {Fr("Jobs configures", "Configured jobs")}: ", ConsoleColor.White);
            var countColor = jobs.Count >= 5 ? ConsoleColor.Red : ConsoleColor.Green;
            WriteLineColor($"{jobs.Count}/5", countColor);

            Console.WriteLine();
            WriteLineColor($"  {Fr("Commandes disponibles", "Available commands")}:", ConsoleColor.Yellow);
            Console.WriteLine();

            WriteColor("  help           ", ConsoleColor.Green);
            Console.WriteLine(Fr("Afficher ce menu", "Show this menu"));

            WriteColor("  run <index>    ", ConsoleColor.Green);
            Console.WriteLine(Fr("Executer un job (ex: run 0)", "Run a job (ex: run 0)"));

            WriteColor("  runall         ", ConsoleColor.Green);
            Console.WriteLine(Fr("Executer tous les jobs", "Run all jobs sequentially"));

            WriteColor("  create         ", ConsoleColor.Green);
            Console.WriteLine(Fr("Creer un nouveau job", "Create a new backup job"));

            WriteColor("  list           ", ConsoleColor.Green);
            Console.WriteLine(Fr("Lister les jobs de sauvegarde", "List all backup jobs"));

            WriteColor("  delete <index> ", ConsoleColor.Green);
            Console.WriteLine(Fr("Supprimer un job (ex: delete 0)", "Delete a job (ex: delete 0)"));

            WriteColor("  exit           ", ConsoleColor.Green);
            Console.WriteLine(Fr("Quitter l'application", "Exit the application"));

            Console.WriteLine();
            WriteLineColor("+-----------------------------------------+", ConsoleColor.Cyan);
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
                    HandleRun(parts);
                    break;

                case "runall":
                    HandleRunAll();
                    break;

                case "create":
                    CreateNewJob();
                    break;

                case "list":
                    ListAllJobs();
                    break;

                case "delete":
                    HandleDelete(parts);
                    break;

                case "exit":
                    WriteLineColor(Fr("Fermeture demandee...", "Exit requested..."), ConsoleColor.Yellow);
                    break;

                default:
                    WriteLineColor(Fr($"Commande inconnue : {command}. Tapez 'help' pour l'aide.",
                        $"Unknown command: {command}. Type 'help' for help."), ConsoleColor.Red);
                    break;
            }
        }

        private void HandleRun(string[] parts)
        {
            if (parts.Length < 2)
            {
                WriteLineColor(Fr("Usage : run <index> (ex: run 0)", "Usage: run <index> (ex: run 0)"), ConsoleColor.Yellow);
                return;
            }

            if (!int.TryParse(parts[1], out int index))
            {
                WriteLineColor(Fr("Index invalide.", "Invalid index."), ConsoleColor.Red);
                return;
            }

            var jobs = _viewModel.GetAllJobs();
            if (index < 0 || index >= jobs.Count)
            {
                WriteLineColor(Fr($"Aucun job a l'index {index}. Utilisez 'list' pour voir les jobs.",
                    $"No job at index {index}. Use 'list' to see jobs."), ConsoleColor.Red);
                return;
            }

            var job = jobs[index];
            PrintSeparator();
            WriteColor(Fr("  Lancement : ", "  Starting:  "), ConsoleColor.Cyan);
            Console.WriteLine(job.Name);
            WriteColor(Fr("  Source:    ", "  Source:    "), ConsoleColor.DarkGray);
            Console.WriteLine(job.SourceDir);
            WriteColor(Fr("  Cible:     ", "  Target:    "), ConsoleColor.DarkGray);
            Console.WriteLine(job.TargetDir);
            WriteColor(Fr("  Type:      ", "  Type:      "), ConsoleColor.DarkGray);
            WriteLineColor(job.Type.ToString(), job.Type == JobType.Full ? ConsoleColor.Magenta : ConsoleColor.Blue);
            PrintSeparator();

            var sw = Stopwatch.StartNew();
            try
            {
                _viewModel.ExecuteJob(index);
                sw.Stop();
                WriteLineColor(Fr($"  Job '{job.Name}' termine avec succes en {sw.ElapsedMilliseconds} ms.",
                    $"  Job '{job.Name}' completed successfully in {sw.ElapsedMilliseconds} ms."), ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                sw.Stop();
                WriteLineColor(Fr($"  Erreur sur le job '{job.Name}': {ex.Message}",
                    $"  Error on job '{job.Name}': {ex.Message}"), ConsoleColor.Red);
            }
            PrintSeparator();
        }

        private void HandleRunAll()
        {
            var jobs = _viewModel.GetAllJobs();
            if (jobs.Count == 0)
            {
                WriteLineColor(Fr("Aucun job configure.", "No jobs configured."), ConsoleColor.Yellow);
                return;
            }

            PrintSeparator();
            WriteLineColor(Fr($"  Execution sequentielle de {jobs.Count} job(s)...",
                $"  Running {jobs.Count} job(s) sequentially..."), ConsoleColor.Cyan);
            PrintSeparator();

            int success = 0;
            int failed = 0;
            var totalSw = Stopwatch.StartNew();

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                WriteColor($"  [{i}] {job.Name} ", ConsoleColor.White);

                var sw = Stopwatch.StartNew();
                try
                {
                    _viewModel.ExecuteJob(i);
                    sw.Stop();
                    WriteLineColor($"OK ({sw.ElapsedMilliseconds} ms)", ConsoleColor.Green);
                    success++;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    WriteLineColor($"ERREUR: {ex.Message}", ConsoleColor.Red);
                    failed++;
                }
            }

            totalSw.Stop();
            PrintSeparator();
            WriteColor(Fr("  Resultat: ", "  Result:   "), ConsoleColor.Cyan);
            WriteColor($"{success} ", ConsoleColor.Green);
            WriteColor(Fr("reussi(s)", "succeeded"), ConsoleColor.Green);
            if (failed > 0)
            {
                WriteColor($", {failed} ", ConsoleColor.Red);
                WriteColor(Fr("echoue(s)", "failed"), ConsoleColor.Red);
            }
            Console.WriteLine();
            WriteColor(Fr("  Duree totale: ", "  Total time:   "), ConsoleColor.DarkGray);
            Console.WriteLine($"{totalSw.ElapsedMilliseconds} ms");
            PrintSeparator();
        }

        private void HandleDelete(string[] parts)
        {
            if (parts.Length < 2)
            {
                WriteLineColor(Fr("Usage : delete <index> (ex: delete 0)", "Usage: delete <index> (ex: delete 0)"), ConsoleColor.Yellow);
                return;
            }

            if (!int.TryParse(parts[1], out int deleteIndex))
            {
                WriteLineColor(Fr("Index invalide.", "Invalid index."), ConsoleColor.Red);
                return;
            }

            var jobs = _viewModel.GetAllJobs();
            if (deleteIndex >= 0 && deleteIndex < jobs.Count)
            {
                string jobName = jobs[deleteIndex].Name;
                bool success = _viewModel.DeleteJob(deleteIndex);
                if (success)
                    WriteLineColor(Fr($"Job [{deleteIndex}] '{jobName}' supprime.", $"Job [{deleteIndex}] '{jobName}' deleted."), ConsoleColor.Green);
                else
                    WriteLineColor(Fr($"Impossible de supprimer le job {deleteIndex}.", $"Cannot delete job {deleteIndex}."), ConsoleColor.Red);
            }
            else
            {
                WriteLineColor(Fr($"Aucun job a l'index {deleteIndex}.", $"No job at index {deleteIndex}."), ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Guide l'utilisateur pour créer un nouveau job de sauvegarde
        /// </summary>
        private void CreateNewJob()
        {
            var jobs = _viewModel.GetAllJobs();
            if (jobs.Count >= 5)
            {
                WriteLineColor(Fr("Limite de 5 jobs atteinte. Supprimez un job d'abord.",
                    "5 job limit reached. Delete a job first."), ConsoleColor.Red);
                return;
            }

            PrintSeparator();
            WriteLineColor(Fr("  Nouveau job de sauvegarde", "  New backup job"), ConsoleColor.Cyan);
            WriteLineColor(Fr($"  Slots disponibles: {5 - jobs.Count}/5", $"  Available slots: {5 - jobs.Count}/5"), ConsoleColor.DarkGray);
            PrintSeparator();

            // Nom du job
            Console.Write(Fr("  Nom du job: ", "  Job name: "));
            string name = Console.ReadLine();

            // Répertoire source
            Console.Write(Fr("  Repertoire source: ", "  Source directory: "));
            string sourceDir = Console.ReadLine();

            // Répertoire cible
            Console.Write(Fr("  Repertoire cible: ", "  Target directory: "));
            string targetDir = Console.ReadLine();

            // Type de sauvegarde
            Console.WriteLine(Fr("  Type de sauvegarde:", "  Backup type:"));
            WriteColor("    1 ", ConsoleColor.Magenta); Console.WriteLine("- Full");
            WriteColor("    2 ", ConsoleColor.Blue); Console.WriteLine("- Differential");
            Console.Write(Fr("  Choix (1-2): ", "  Choice (1-2): "));

            JobType type = JobType.Full;
            string typeChoice = Console.ReadLine();
            if (typeChoice == "2")
                type = JobType.Differential;

            // Création du job via le ViewModel
            bool success = _viewModel.CreateJob(name, sourceDir, targetDir, type);

            PrintSeparator();
            if (success)
            {
                WriteLineColor(Fr($"  Job '{name}' cree avec succes!", $"  Job '{name}' created successfully!"), ConsoleColor.Green);
            }
            else
            {
                WriteLineColor(Fr("  Erreur lors de la creation du job. Verifiez les informations.",
                    "  Error creating job. Check the provided information."), ConsoleColor.Red);
            }
            PrintSeparator();
        }

        /// <summary>
        /// Affiche tous les travaux de sauvegarde existants
        /// </summary>
        private void ListAllJobs()
        {
            var jobs = _viewModel.GetAllJobs();

            PrintSeparator();
            WriteColor(Fr("  Travaux de sauvegarde", "  Backup jobs"), ConsoleColor.Cyan);
            var countColor = jobs.Count >= 5 ? ConsoleColor.Red : ConsoleColor.Green;
            WriteLineColor($" ({jobs.Count}/5)", countColor);
            PrintSeparator();

            if (jobs.Count == 0)
            {
                WriteLineColor(Fr("  Aucun job configure. Utilisez 'create' pour en ajouter.",
                    "  No jobs configured. Use 'create' to add one."), ConsoleColor.DarkGray);
                PrintSeparator();
                return;
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var typeColor = job.Type == JobType.Full ? ConsoleColor.Magenta : ConsoleColor.Blue;

                WriteColor($"  [{i}] ", ConsoleColor.Yellow);
                WriteColor(job.Name, ConsoleColor.White);
                WriteColor(" (", ConsoleColor.DarkGray);
                WriteColor(job.Type.ToString(), typeColor);
                WriteLineColor(")", ConsoleColor.DarkGray);

                WriteColor($"      {Fr("Source", "Source")}: ", ConsoleColor.DarkGray);
                Console.WriteLine(job.SourceDir);
                WriteColor($"      {Fr("Cible", "Target")}:  ", ConsoleColor.DarkGray);
                Console.WriteLine(job.TargetDir);

                if (i < jobs.Count - 1)
                    Console.WriteLine();
            }

            PrintSeparator();
        }
    }
}
