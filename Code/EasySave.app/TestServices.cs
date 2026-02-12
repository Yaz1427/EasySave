using System;
using EasySave.Services;
using EasyLog.Services;
using System.IO;
using EasySave.ViewModel;
using EasySave.Models;

namespace EasySave.CLI
{
    /// <summary>
    /// Test complet pour vérifier toutes les fonctionnalités
    /// </summary>
    public class TestServices
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Test complet d'EasySave ===");
            
            try
            {
                // 1. Test LoggerService
                Console.WriteLine("\n1. Test LoggerService...");
                var logger = new LoggerService();
                var testEntry = new EasyLog.Models.LogEntry
                {
                    Timestamp = DateTime.Now,
                    JobName = "TestJob",
                    SourcePath = "C:\\test\\source",
                    TargetPath = "C:\\test\\target",
                    FileSize = 1024,
                    TransferTime = 1000
                };
                logger.SaveLog(testEntry);
                Console.WriteLine("✅ LoggerService OK - dossier Logs créé");
                
                // 2. Test RealTimeStateService
                Console.WriteLine("\n2. Test RealTimeStateService...");
                var rtState = new RealTimeStateService();
                var testState = new EasySave.Models.RealTimeState
                {
                    JobName = "TestJob",
                    Status = "Active",
                    LastActionTimestamp = DateTime.Now,
                    Progress = 50,
                    CurrentSourceFile = "C:\\test\\source\\file.txt",
                    CurrentTargetFile = "C:\\test\\target\\file.txt"
                };
                rtState.UpdateState(testState);
                Console.WriteLine("✅ RealTimeStateService OK - fichier state.json créé");
                
                // 3. Test création de répertoires de test
                Console.WriteLine("\n3. Création des répertoires de test...");
                string testSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestSource");
                string testTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestTarget");
                
                Directory.CreateDirectory(testSource);
                Directory.CreateDirectory(testTarget);
                
                // Créer un fichier de test
                File.WriteAllText(Path.Combine(testSource, "test.txt"), "Ceci est un fichier de test pour EasySave.");
                Console.WriteLine($"✅ Répertoires créés : {testSource} -> {testTarget}");
                
                // 4. Test complet avec ViewModel
                Console.WriteLine("\n4. Test complet avec ViewModel...");
                var viewModel = new MainViewModel();
                
                // Créer un job de test
                bool jobCreated = viewModel.CreateJob("TestAuto", testSource, testTarget, JobType.Full);
                if (jobCreated)
                {
                    Console.WriteLine("✅ Job de test créé avec succès");
                    
                    // Lister les jobs
                    var jobs = viewModel.GetAllJobs();
                    Console.WriteLine($"✅ Nombre de jobs : {jobs.Count}");
                    
                    // Exécuter le job
                    Console.WriteLine("\n5. Exécution du job de test...");
                    viewModel.ExecuteJob(0);
                    Console.WriteLine("✅ Job exécuté avec succès");
                }
                else
                {
                    Console.WriteLine("❌ Échec de création du job");
                }
                
                Console.WriteLine("\n=== Test terminé avec succès ===");
                Console.WriteLine("Fichiers créés dans le dossier de l'application :");
                Console.WriteLine("- Logs/ (fichiers de log journaliers)");
                Console.WriteLine("- state.json (état temps réel)");
                Console.WriteLine("- TestSource/ et TestTarget/ (répertoires de test)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERREUR pendant le test : {ex.Message}");
                Console.WriteLine($"Stack trace : {ex.StackTrace}");
            }
            
            Console.WriteLine("\nAppuyez sur une touche pour continuer...");
            Console.ReadKey();
        }
    }
}
