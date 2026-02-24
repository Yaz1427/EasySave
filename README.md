# EasySave v3.0

Application graphique (WPF) de sauvegarde de fichiers developpee en C# (.NET 8.0).
Projet realise dans le cadre du module Genie Logiciel - CESI 3eme annee.

---

## Fonctionnalites v3.0

- **Sauvegarde en parallele** : tous les travaux s'executent simultanement
- **Gestion des fichiers prioritaires** : les fichiers non prioritaires sont bloques tant qu'il reste des fichiers prioritaires en attente
- **Interdiction de transfert simultane de fichiers volumineux** : un seul fichier > n Ko transfere a la fois (n parametrable)
- **Controle temps reel par travail** : Pause / Play / Stop pour chaque travail ou pour l'ensemble
- **Suivi en temps reel** : progression en pourcentage, fichier en cours, fichiers restants
- **Pause automatique si logiciel metier detecte** : tous les travaux se mettent en pause et reprennent automatiquement
- **CryptoSoft Mono-Instance** : le logiciel de chiffrement ne peut etre execute qu'une seule fois simultanement (Mutex)
- **Centralisation des logs Docker** : service de centralisation des logs en temps reel (Local / Centralise / Les deux)
- **Nombre illimite de travaux** de sauvegarde
- **Interface soignee** (WPF) avec onglets et theme moderne
- **Multi-langues** : Anglais, Francais, Taqbaylit
- **Chiffrement** : AES-256 ou XOR, extensions configurables
- **Logs journaliers** : JSON et XML, avec temps de chiffrement
- **Fichier d'etat** multi-travaux en temps reel
- **Accès concurrent** : ecritures atomiques pour fichiers de config, logs et etat
- **Ligne de commande** : compatible v1.0

## Architecture

Le projet suit le pattern MVVM :

```
Code/
  EasySave.app/               <- Application WPF principale
    Model/                    <- BackupJob, RealTimeState, AppSettings
    View/                     <- MainWindow (XAML + code-behind)
    ViewModel/                <- MainViewModel, JobViewModel, ViewModelBase, RelayCommand
    Services/                 <- BackupEngine, ConfigService, CryptoSoftService,
                                 BusinessSoftwareService, RealTimeStateService
    Resources/                <- Lang_en.xaml, Lang_fr.xaml, Lang_kab.xaml

  EasySave.dll/               <- Librairie EasyLog (DLL)
    Models/                   <- LogEntry
    Services/                 <- LoggerService (thread-safe, Docker support)

  CryptoSoft/                 <- Outil de chiffrement AES-256 (Mono-Instance)

  LogServer/                  <- Service Docker de centralisation des logs (ASP.NET Core)
```

## Prerequis

- .NET 8.0 SDK
- Docker (optionnel, pour la centralisation des logs)

## Compilation

```bash
dotnet build Code/EasySave.app/EasySave.csproj
dotnet build Code/CryptoSoft/CryptoSoft.csproj
dotnet build Code/LogServer/LogServer.csproj
```

## Utilisation

### Mode graphique

```bash
dotnet run --project Code/EasySave.app/EasySave.csproj
```

L'interface comporte 5 onglets :
- **Backup Jobs** : liste des travaux, boutons Run/Run All/Pause All/Resume All/Stop All
- **Running Jobs** : suivi temps reel avec progression, Pause/Play/Stop par travail
- **Create/Edit Job** : formulaire de creation/modification
- **Logs** : consultation des fichiers de log journaliers
- **Settings** : parametres (langue, format log, chiffrement, logiciel metier, extensions prioritaires, taille fichiers volumineux, destination des logs, URL serveur Docker)

### Mode ligne de commande

```bash
# Executer les jobs 1 a 3
EasySave.exe 1-3

# Executer les jobs 1 et 3
EasySave.exe "1;3"
```

## Centralisation des logs Docker

```bash
# Lancer le serveur de logs
docker-compose up -d

# Le serveur ecoute sur http://localhost:5080
# API endpoints :
#   POST /api/logs          <- Recevoir un log
#   GET  /api/logs          <- Lister les fichiers de log
#   GET  /api/logs/{date}   <- Lire un fichier de log (ex: 2025-01-15)
```

Dans les parametres d'EasySave, choisir la destination :
- **Local** : logs uniquement sur le PC de l'utilisateur
- **Centralized** : logs uniquement sur le serveur Docker
- **Both** : logs sur le PC et sur le serveur Docker

## Fichiers generes

### Configuration

- `settings.json` : parametres de l'application
- `jobs.json` : liste des travaux de sauvegarde

### Logs et etat

Emplacement : `LogsEasySave/` (a la racine du projet)

- `LogsEasySave/YYYY-MM-DD.json` ou `.xml` : log journalier
- `LogsEasySave/state.json` : etat temps reel de tous les travaux

### Contenu du log journalier

- Horodatage
- Nom du travail
- Chemin fichier source et destination
- Taille du fichier (octets)
- Temps de transfert (ms)
- Temps de chiffrement (ms)

### Contenu du fichier d'etat

Tableau JSON des travaux en cours, chacun contenant :
- Nom du travail, horodatage, etat
- Nombre total de fichiers, taille totale
- Progression (%), fichiers et taille restants
- Fichier source et destination en cours
