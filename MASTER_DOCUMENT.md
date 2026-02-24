# EasySave v3.0 — Documentation Technique de Reference (Master Document)

> **Projet** : EasySave v3.0  
> **Contexte** : Module Genie Logiciel — CESI 3eme annee  
> **Proprietaire** : @Yaz1427  
> **Date de redaction** : 24 fevrier 2026  

---

## Table des matieres

1. [Vue d'ensemble et Mission](#1-vue-densemble-et-mission)
2. [Architecture Globale](#2-architecture-globale)
3. [Stack Technique detaillee](#3-stack-technique-detaillee)
4. [Analyse du Code](#4-analyse-du-code)
5. [Design Patterns](#5-design-patterns)
6. [Gestion de l'Etat et Persistance](#6-gestion-de-letat-et-persistance)
7. [Securite et Performance](#7-securite-et-performance)
8. [Points de Friction et Ameliorations](#8-points-de-friction-et-ameliorations)

---

## 1. Vue d'ensemble et Mission

### 1.1 Utilite principale

EasySave est une **application de sauvegarde de fichiers** destinee a un environnement professionnel (fictif : ProSoft). Elle permet de :

- **Creer des travaux de sauvegarde** (completes ou differentielles) entre un repertoire source et un repertoire cible
- **Executer ces travaux en parallele** avec suivi temps reel
- **Chiffrer** les fichiers sensibles via un outil externe (CryptoSoft)
- **Centraliser les logs** sur un serveur Docker distant

### 1.2 Flux de valeur

```
Utilisateur
    |
    v
[Interface WPF] --- cree/configure ---> [jobs.json + settings.json]
    |
    v
[BackupEngine] --- copie fichiers ---> [Repertoire cible]
    |                                       |
    |--- log local -------> [LogsEasySave/YYYY-MM-DD.json|xml]
    |--- log distant -----> [LogServer Docker :5080]
    |--- etat temps reel -> [LogsEasySave/state.json]
    |
    v
[CryptoSoft.exe] --- chiffre ---> [Fichier cible chiffre AES-256]
```

**Resume du cycle** :
1. L'utilisateur configure des travaux via l'interface WPF ou la ligne de commande
2. Le `BackupEngine` orchestre la copie de chaque fichier (priorite, pause, semaphore)
3. Apres copie, si l'extension correspond, `CryptoSoft.exe` est appele pour chiffrer le fichier
4. Chaque operation est loguee (localement et/ou sur le serveur Docker)
5. L'etat temps reel est ecrit dans `state.json` apres chaque fichier

---

## 2. Architecture Globale

### 2.1 Structure des dossiers

```
V3.0/
├── Code/
│   ├── EasySave.app/           # Application WPF principale (projet C# WinExe)
│   │   ├── Model/              # Modeles de donnees (BackupJob, AppSettings, RealTimeState)
│   │   ├── View/               # Vue XAML (MainWindow)
│   │   ├── ViewModel/          # ViewModels MVVM (MainViewModel, JobViewModel, RelayCommand)
│   │   ├── Services/           # Logique metier (BackupEngine, ConfigService, CryptoSoftService...)
│   │   ├── Resources/          # Dictionnaires de langues (en, fr, kab)
│   │   ├── App.xaml(.cs)       # Point d'entree WPF + changement de langue
│   │   └── EasySave.csproj     # Projet .NET 8.0-windows (WPF), ref EasyLog.dll
│   │
│   ├── EasySave.dll/           # Bibliotheque partagee "EasyLog" (projet Class Library)
│   │   ├── Models/LogEntry.cs  # Structure d'une entree de log
│   │   ├── Services/           # LoggerService (JSON/XML + envoi HTTP Docker)
│   │   └── EasyLog.csproj      # Projet .NET 8.0 (net8.0, pas windows-specific)
│   │
│   ├── CryptoSoft/             # Outil de chiffrement autonome (Console App)
│   │   ├── Program.cs          # AES-256-CBC, mono-instance via Mutex
│   │   └── CryptoSoft.csproj   # Projet .NET 8.0 (Exe)
│   │
│   └── LogServer/              # Serveur de centralisation des logs (ASP.NET Core Minimal API)
│       ├── Program.cs          # 4 endpoints REST + dashboard HTML embarque
│       └── LogServer.csproj    # Projet .NET 8.0 (Web)
│
├── Properties/
│   ├── Program.cs              # Point d'entree console legacy (v1.0 compat)
│   └── launchSettings.json     # Profils de lancement (Project + Docker)
│
├── LogsEasySave/               # Repertoire de logs generes (JSON, XML, state)
├── RepertoireTESTS/            # Donnees de test (Source/ et Cible/)
├── Dockerfile                  # Build multi-stage du LogServer
├── docker-compose.yml          # Orchestration du LogServer sur port 5080
├── EasySave.sln                # Solution Visual Studio (4 projets)
└── README.md                   # Documentation utilisateur
```

### 2.2 Graphe de dependances entre projets

```
EasySave.app (WPF)
    └──> EasyLog.dll (ProjectReference)

CryptoSoft (Console)        # Aucune dependance inter-projet
                             # Appele par EasySave.app via Process.Start()

LogServer (ASP.NET Core)    # Aucune dependance inter-projet
                             # Communique avec EasyLog.dll via HTTP POST
```

### 2.3 Flux de donnees (Data Flow)

```
┌──────────────────────────────────────────────────────────────────┐
│                        EasySave.app                              │
│                                                                  │
│  [MainWindow.xaml] <──binding──> [MainViewModel]                 │
│                                       │                          │
│                              ┌────────┴────────┐                 │
│                              │   BackupEngine   │                │
│                              └────────┬────────┘                 │
│                    ┌─────────────┬─────┴──────┬──────────┐       │
│                    │             │            │          │       │
│           [ConfigService] [CryptoSoftService] [RTSS]  [Logger]  │
│             (JSON I/O)    (Process.Start)   (state)  (EasyLog)  │
│                    │             │            │     ┌────┴────┐  │
│             ┌──────┴──────┐     │            │     │  Local  │  │
│             │ jobs.json   │     │            │     │  JSON/  │  │
│             │settings.json│     │            │     │  XML    │  │
│             └─────────────┘     │            │     └─────────┘  │
│                                 │            │          │       │
│                          ┌──────┴──────┐     │     ┌────┴────┐  │
│                          │ CryptoSoft  │     │     │ HTTP    │  │
│                          │   .exe      │     │     │ POST    │  │
│                          └─────────────┘     │     └────┬────┘  │
│                                              │          │       │
└──────────────────────────────────────────────┼──────────┼───────┘
                                               │          │
                                    ┌──────────┴┐    ┌────┴──────────┐
                                    │state.json │    │  LogServer    │
                                    │(temps reel)│    │  (Docker)    │
                                    └───────────┘    │  :5080       │
                                                     └───────────────┘
```

---

## 3. Stack Technique detaillee

### 3.1 Langages et Frameworks

| Composant | Technologie | Version | Justification |
|---|---|---|---|
| **Application principale** | C# / WPF | .NET 8.0-windows | Framework desktop natif Microsoft, riche en data binding MVVM |
| **Bibliotheque de logs** | C# / Class Library | .NET 8.0 | DLL portable (pas de dependance Windows), reutilisable |
| **Outil de chiffrement** | C# / Console App | .NET 8.0 | Executable autonome, isolation du processus de chiffrement |
| **Serveur de logs** | C# / ASP.NET Core Minimal API | .NET 8.0 | API REST legere, deployable en container Docker |
| **Conteneurisation** | Docker + Docker Compose | — | Deploiement isole du serveur de logs |

### 3.2 Bibliotheques et packages NuGet

| Package | Projet | Version | Role |
|---|---|---|---|
| `System.Text.Json` | EasyLog.dll | 8.0.5 | Serialisation/deserialisation JSON haute performance |
| `System.Security.Cryptography` | CryptoSoft | (BCL) | Chiffrement AES-256-CBC natif |
| WPF (UseWPF=true) | EasySave.app | (SDK) | Interface graphique XAML |
| ASP.NET Core (Sdk.Web) | LogServer | (SDK) | Serveur HTTP Minimal API |

### 3.3 Outils de build et deploiement

- **Visual Studio 2022** (v17+) — Solution `.sln` format 12.00
- **Docker multi-stage build** : `mcr.microsoft.com/dotnet/sdk:8.0` (build) + `aspnet:8.0` (runtime)
- **Docker Compose** : service `logserver` sur port 5080, volume persistant `logserver-data:/app/Logs`

---

## 4. Analyse du Code

### 4.1 Point d'entree — `App.xaml.cs`

```
Code/EasySave.app/App.xaml.cs
```

- **`OnStartup`** : charge les parametres via `ConfigService`, applique la langue, gere le mode ligne de commande
- **`SetLanguage(string lang)`** : swap le `ResourceDictionary` (en/fr/kab) pour l'i18n a chaud
- **`ExecuteCommandLine(string arg)`** : parse les formats `1-3` (plage) ou `1;3` (liste) pour execution CLI

> **Point cle** : L'application supporte deux modes — **GUI** (WPF standard) et **CLI** (via arguments, compatible v1.0). En mode CLI, l'application s'arrete apres execution (`Shutdown()`).

### 4.2 Modeles de donnees

#### `BackupJob` (`Model/BackupJob.cs`)
- **4 proprietes** : `Name`, `SourceDir`, `TargetDir`, `Type` (Full/Differential)
- Modele POCO pur, pas de logique metier

#### `AppSettings` (`Model/AppSettings.cs`)
- **10 proprietes** configurables : langue, format de log, extensions a chiffrer, logiciel metier, mode de chiffrement (AES/XOR), extensions prioritaires, taille max fichier volumineux, destination des logs, URL serveur Docker
- Methode utilitaire `GetLogFormat()` pour convertir le string en enum `LogFormat`

#### `RealTimeState` (`Model/RealTimeState.cs`)
- **9 proprietes** de suivi temps reel par travail : nom, timestamp, statut, total fichiers, taille totale, progression %, fichiers restants, fichier source/cible en cours

#### `LogEntry` (`EasySave.dll/Models/LogEntry.cs`)
- **7 proprietes** : Timestamp, JobName, SourcePath, TargetPath, FileSize, TransferTime, EncryptionTime

### 4.3 Couche Services — Le coeur metier

#### `BackupEngine` (`Services/BackupEngine.cs`) — **592 lignes, fichier le plus critique**

C'est l'**orchestrateur central** de toute la logique de sauvegarde v3.0. Il gere :

1. **Execution parallele** — `RunJobsInParallel()` lance chaque job dans un `Task.Run()` distinct
2. **Fichiers prioritaires** — Systeme global `_globalPriorityFilesRemaining` + `ManualResetEventSlim` :
   - Pre-scan de tous les jobs pour compter les fichiers prioritaires
   - Les fichiers non-prioritaires attendent via `WaitForPriorityFiles()` que le compteur global tombe a 0
3. **Fichiers volumineux** — `SemaphoreSlim(1,1)` : un seul fichier > seuil peut etre transfere simultanement
4. **Pause/Resume par job** — Chaque `JobViewModel` possede son propre `ManualResetEventSlim` (`PauseEvent`)
5. **Detection logiciel metier** — `Timer` toutes les 1.5s qui scanne les processus ; met en pause/reprend tous les jobs automatiquement
6. **Logging** — Apres chaque copie, cree un `LogEntry` et l'envoie au `LoggerService`
7. **Chiffrement conditionnel** — Si l'extension du fichier correspond, appelle `CryptoSoftService.EncryptFile()`

**Methodes maitresses** :
- `RunJobsInParallel(IEnumerable<JobViewModel>)` — Point d'entree pour l'execution de tous les jobs
- `RunSingleJob(JobViewModel, List<string>)` — Boucle fichier par fichier (prioritaires d'abord, puis normaux)
- `ProcessSingleFile(...)` — Copie un fichier avec gestion pause, priorite, semaphore, logging
- `CopyFileWithLogging(...)` — Copie physique + chiffrement + creation LogEntry
- `CheckBusinessSoftware(object?)` — Callback du Timer de surveillance

#### `BackupService` (`Services/BackupService.cs`) — **268 lignes**

Version **v2.0 legacy** (sequentielle) du moteur de sauvegarde. Conservee pour compatibilite :
- Utilise `BusinessSoftwareService` en mode bloquant (throw si detecte)
- Copie sequentielle (pas de parallelisme)
- Sert de reference pour comprendre l'evolution v2 → v3

#### `ConfigService` (`Services/ConfigService.cs`) — **145 lignes**

Gestion de la persistance de configuration :
- Stockage dans `%AppData%/EasySave/` (par defaut)
- **Ecritures atomiques** : pattern `write temp → copy → delete temp`
- **Thread-safe** : `static readonly object _lock` partage
- Methodes : `LoadJobs()`, `SaveJobs()`, `LoadSettings()`, `SaveSettings()`

#### `CryptoSoftService` (`Services/CryptoSoftService.cs`) — **114 lignes**

Pont entre l'application et l'executable externe `CryptoSoft.exe` :
- Lance `CryptoSoft.exe` via `Process.Start()` avec le chemin du fichier en argument
- Attend jusqu'a 60s (CryptoSoft peut etre en attente du Mutex)
- Retourne le temps de chiffrement en ms (>0 = succes, <0 = code erreur)
- Recherche automatique de `CryptoSoft.exe` dans plusieurs chemins relatifs

#### `BusinessSoftwareService` (`Services/BusinessSoftwareService.cs`) — **42 lignes**

Detection d'un processus metier par nom :
- `Process.GetProcessesByName()` — retourne `true` si au moins un processus correspond

#### `RealTimeStateService` (`Services/RealTimeStateService.cs`) — **142 lignes**

Gestion du fichier d'etat temps reel `state.json` :
- Stocke un tableau JSON de `RealTimeState` (un par job actif)
- **Ecritures atomiques** + **thread-safe** (lock + temp file)
- Compatible v2.0 en lecture (fallback objet unique)

### 4.4 Couche ViewModel

#### `MainViewModel` (`ViewModel/MainViewModel.cs`) — **646 lignes**

**Cerveau de l'application**. Fait le lien entre la View (XAML) et les Services :

- **15 ICommand** exposes : CreateJob, DeleteJob, ExecuteJob, ExecuteAllJobs, SaveSettings, BrowseSource/Target, Edit/SaveEdit/CancelEdit, RefreshLogs, OpenLogsFolder, StopBackup, PauseAll, ResumeAll
- **Proprietes bindees** : Jobs (ObservableCollection), SelectedJob, RunningJobs, StatusMessage, CurrentProgress, tous les champs Settings
- **Orchestration** : `ExecuteAllJobs()` cree un `JobViewModel` par job, les passe au `BackupEngine.RunJobsInParallel()`, et agrege la progression moyenne
- **Logs** : charge les fichiers JSON/XML du repertoire `LogsEasySave/` dans un DataGrid

#### `JobViewModel` (`ViewModel/JobViewModel.cs`) — **163 lignes**

Representation temps reel d'un job en cours d'execution :
- **Etats** : Pending → Running → Paused/Stopped/Completed/Error
- **Controles** : `PauseCommand`, `ResumeCommand`, `StopCommand`
- **Mecanisme de pause** : `ManualResetEventSlim` (`PauseEvent`) — le thread de copie attend quand reset
- **Mecanisme d'arret** : `CancellationTokenSource` — provoque `OperationCanceledException`

#### `ViewModelBase` (`ViewModel/ViewModelBase.cs`) — **24 lignes**

Classe abstraite implementant `INotifyPropertyChanged` avec helper `SetProperty<T>()`.

#### `RelayCommand` (`ViewModel/RelayCommand.cs`) — **33 lignes**

Implementation classique de `ICommand` avec `Action<object?>` et `Func<object?, bool>` optionnel. Utilise `CommandManager.RequerySuggested` pour la reevaluation automatique de `CanExecute`.

### 4.5 Couche View

#### `MainWindow.xaml` — **686 lignes**

Interface WPF structuree en **5 onglets** :

| Onglet | Contenu |
|---|---|
| **Backup Jobs** | DataGrid des jobs + boutons Run/RunAll/PauseAll/ResumeAll/StopAll/Delete/Edit |
| **Running Jobs** | ItemsControl avec template : nom, statut, barre de progression, fichier en cours, boutons Pause/Play/Stop par job |
| **Create/Edit Job** | Formulaire : nom, source (avec Browse), cible (avec Browse), type (Full/Differential) |
| **Logs** | Selecteur de fichier log + DataGrid des entrees (Timestamp, Job, Source, Target, Size, Transfer, Encrypt) |
| **Settings** | Langue, format log, mode chiffrement, extensions a chiffrer, logiciel metier, extensions prioritaires, taille max, destination logs, URL serveur |

**Theme visuel** : palette beige/gris neutre avec boutons arrondis (pill buttons, CornerRadius=14), style moderne.

#### `MainWindow.xaml.cs` — **132 lignes**

Code-behind minimal :
- Synchronisation des ComboBox de langue (header + settings)
- Delegation des `SelectionChanged` vers le ViewModel
- Navigation entre onglets (Edit → onglet Create/Edit)

### 4.6 CryptoSoft — `Code/CryptoSoft/Program.cs` — **139 lignes**

Executable autonome de chiffrement :

- **AES-256-CBC** : cle derivee via SHA-256, IV aleatoire prepend au fichier
- **Mono-instance** : `Mutex` global nomme `"Global\\CryptoSoft_SingleInstance_Mutex"`, attente 30s max
- **Codes de sortie** : 0=succes, 1=args invalides, 2=fichier introuvable, 3=erreur chiffrement, 4=instance deja en cours
- **Cle par defaut** : `"EasySave2025ProSoftCESISecureKey!"` (32 caracteres = 256 bits)

### 4.7 LogServer — `Code/LogServer/Program.cs` — **579 lignes**

Serveur ASP.NET Core Minimal API avec **4 endpoints + dashboard HTML** :

| Endpoint | Methode | Description |
|---|---|---|
| `POST /api/logs` | POST | Recoit un log JSON, l'ajoute au fichier du jour |
| `GET /api/logs` | GET | Liste les fichiers de log disponibles |
| `GET /api/logs/{date}` | GET | Retourne le contenu d'un fichier de log |
| `GET /api/stats` | GET | Statistiques agregees (entries, machines, users, taille) |
| `GET /` | GET | Dashboard HTML interactif avec stats + tableau triable/filtrable |

**Dashboard embarque** : ~420 lignes de HTML/CSS/JS inline avec :
- Grille de statistiques (entries, fichiers, taille, jours, machines, utilisateurs)
- Tableau des logs avec tri par colonne, recherche, auto-refresh toutes les 10s
- Theme professionnel (Segoe UI, palette gris/bleu)

### 4.8 EasyLog.dll — `LoggerService` — **200 lignes**

Bibliotheque partagee de logging :
- **Dual format** : JSON (`System.Text.Json`) et XML (`XmlSerializer`)
- **Dual destination** : Local (fichier) et/ou Centralized (HTTP POST vers LogServer)
- **Thread-safe** : `static readonly object _fileLock` pour les ecritures fichier
- **Ecritures atomiques** : pattern temp file → copy → delete
- **Enrichissement** : ajoute `MachineName` et `UserName` automatiquement pour les logs centralises
- **Resilient** : `SendLogToServer()` catch silencieusement les erreurs reseau (timeout 5s)

---

## 5. Design Patterns

### 5.1 Pattern MVVM (Model-View-ViewModel)

**Pattern architectural principal** de l'application WPF.

| Couche | Composants | Responsabilite |
|---|---|---|
| **Model** | `BackupJob`, `AppSettings`, `RealTimeState`, `LogEntry` | Donnees pures, pas de logique UI |
| **View** | `MainWindow.xaml` | Presentation XAML, data binding declaratif |
| **ViewModel** | `MainViewModel`, `JobViewModel` | Logique de presentation, commandes, etat UI |

**Implementation** :
- `ViewModelBase` fournit `INotifyPropertyChanged` + `SetProperty<T>()`
- `RelayCommand` implemente `ICommand` pour le pattern Command
- Binding bidirectionnel via `{Binding ...}` et `DynamicResource` pour l'i18n
- Le code-behind (`MainWindow.xaml.cs`) est minimal — uniquement pour les interactions impossibles en pur XAML (ComboBox SelectionChanged, navigation d'onglets)

### 5.2 Pattern Command

`RelayCommand` encapsule chaque action utilisateur en objet `ICommand` :
- **Execute** : `Action<object?>` — l'action a effectuer
- **CanExecute** : `Func<object?, bool>` — condition d'activation
- Utilise `CommandManager.RequerySuggested` pour reevaluer automatiquement les boutons

### 5.3 Pattern Observer

Plusieurs mecanismes d'observation :
- **`INotifyPropertyChanged`** : les ViewModels notifient la View de tout changement de propriete
- **`ObservableCollection<T>`** : notification automatique des ajouts/suppressions de jobs
- **Evenements** : `BackupEngine.StatusChanged` et `BusinessSoftwareStateChanged` notifient le ViewModel

### 5.4 Pattern Strategy (implicite)

Le mode de sauvegarde (Full vs Differential) determine la strategie de copie :
- **Full** : copie tous les fichiers inconditionnellement
- **Differential** : copie uniquement les fichiers modifies (comparaison date + taille)

### 5.5 Pattern Facade

`BackupEngine` agit comme facade unifiee pour :
- Le `CryptoSoftService` (chiffrement)
- Le `LoggerService` (logging)
- Le `RealTimeStateService` (etat temps reel)
- La surveillance du logiciel metier
- La gestion des priorites et du semaphore

### 5.6 Pattern Atomic Write (Crash-Safety)

Utilise systematiquement dans `ConfigService`, `LoggerService`, `RealTimeStateService` et `LogServer` :
```
1. Ecrire dans fichier.tmp
2. File.Copy(tmp → fichier, overwrite: true)
3. File.Delete(tmp)
```
Garantit qu'un crash pendant l'ecriture ne corrompt pas le fichier original.

### 5.7 Mutex (Mono-Instance)

`CryptoSoft` utilise un `Mutex` global nomme pour garantir qu'une seule instance s'execute a la fois :
- Attente de 30s pour acquirir le mutex
- Gestion de `AbandonedMutexException` (crash de l'instance precedente)

### 5.8 Semaphore (Controle de concurrence)

`BackupEngine._largeFileSemaphore` (`SemaphoreSlim(1,1)`) : empeche le transfert simultane de fichiers volumineux (> seuil configurable).

### 5.9 ManualResetEventSlim (Signaling)

Trois usages distincts :
- **`_businessSoftwarePause`** : signal global pour suspendre tous les jobs si logiciel metier detecte
- **`_priorityDoneEvent`** : signal global pour debloquer les fichiers non-prioritaires
- **`JobViewModel.PauseEvent`** : signal par-job pour pause/resume individuel

---

## 6. Gestion de l'Etat et Persistance

### 6.1 Fichiers de configuration

| Fichier | Emplacement | Format | Contenu |
|---|---|---|---|
| `settings.json` | `%AppData%/EasySave/` | JSON | Parametres globaux (langue, format log, chiffrement, etc.) |
| `jobs.json` | `%AppData%/EasySave/` | JSON | Liste des travaux de sauvegarde configures |

**Acces** : `ConfigService` avec lock statique + ecriture atomique.

### 6.2 Fichiers de logs

| Fichier | Emplacement | Format | Contenu |
|---|---|---|---|
| `YYYY-MM-DD.json` | `LogsEasySave/` | JSON | Log journalier (tableau de LogEntry) |
| `YYYY-MM-DD.xml` | `LogsEasySave/` | XML | Log journalier alternatif |
| `state.json` | `LogsEasySave/` | JSON | Etat temps reel de tous les jobs actifs |

### 6.3 Logs centralises (Docker)

| Fichier | Emplacement | Format | Contenu |
|---|---|---|---|
| `YYYY-MM-DD.json` | `/app/Logs/` (container) | JSON | Logs enrichis (MachineName, UserName) |

**Volume Docker** : `logserver-data:/app/Logs` — persiste les donnees entre redemarrages.

### 6.4 State Management (UI)

- **`ObservableCollection<BackupJob>`** : liste reactive des jobs dans le DataGrid
- **`ObservableCollection<JobViewModel>`** : jobs en cours d'execution avec progression temps reel
- **Binding WPF** : toute modification de propriete dans un ViewModel se propage automatiquement a la View via `INotifyPropertyChanged`
- **Dispatcher** : les mises a jour UI depuis les threads de copie passent par `Application.Current.Dispatcher.Invoke()` pour respecter le thread affinity de WPF

### 6.5 Pas de base de donnees

Le projet n'utilise **aucune base de donnees**. Toute la persistance est basee sur des fichiers JSON/XML sur disque. C'est un choix coherent pour une application desktop sans serveur permanent.

---

## 7. Securite et Performance

### 7.1 Securite

#### Chiffrement
- **AES-256-CBC** via `System.Security.Cryptography.Aes` — algorithme reconnu, standard industriel
- Cle derivee via **SHA-256** a partir d'une chaine
- **IV aleatoire** genere pour chaque fichier (`aes.GenerateIV()`)
- Format du fichier chiffre : `[16 octets IV][donnees chiffrees PKCS7]`

#### Faiblesses identifiees
- **Cle par defaut en dur** : `"EasySave2025ProSoftCESISecureKey!"` est hardcodee dans `CryptoSoft/Program.cs` ligne 28. En production, cela constituerait une vulnerabilite majeure.
- **Pas de dechiffrement** : CryptoSoft chiffre uniquement — il n'y a pas de mecanisme de restauration des fichiers chiffres dans le projet.
- **Mode XOR declare mais non implemente** : `EncryptionMode.XOR` est defini dans l'enum mais `CryptoSoftService` passe toujours `CryptoSoft.exe` qui fait uniquement de l'AES. Le mode XOR n'est pas reellement utilise.
- **Pas d'authentification** sur le `LogServer` : n'importe qui peut envoyer/lire des logs sur le port 5080.
- **Pas de HTTPS** : le LogServer ecoute en HTTP clair (`http://+:5080`).
- **Pas de validation d'entree stricte** : le LogServer deserialise le body JSON sans schema validation.

### 7.2 Performance

#### Points forts
- **Parallelisme** : tous les jobs s'executent en parallele via `Task.Run()` + `Task.WhenAll()`
- **SemaphoreSlim** pour les fichiers volumineux : evite la saturation du disque/reseau
- **Fichiers prioritaires** : mecanisme global de compteur + signal pour traiter les fichiers critiques en premier
- **Ecritures atomiques** : empechent la corruption de donnees en cas de crash
- **HttpClient statique** : instance unique reutilisee pour les appels HTTP (bonne pratique .NET)
- **Timer-based monitoring** : surveillance du logiciel metier par polling toutes les 1.5s (faible cout CPU)

#### Points faibles
- **`SendLogToServer()` synchrone** : `_httpClient.PostAsync(...).GetAwaiter().GetResult()` bloque le thread appelant. Pour un systeme haute performance, cela devrait etre totalement asynchrone.
- **Relecture complete du fichier de log a chaque ajout** : `LoggerService.SaveLogJson()` deserialise tout le fichier, ajoute une entree, puis re-serialise tout. Avec des milliers d'entrees, cela devient couteux.
- **`state.json` ecrit apres chaque fichier** : pour des sauvegardes de milliers de petits fichiers, cela genere un I/O disque intense.
- **`allFiles.Skip(processedFiles).Sum(f => new FileInfo(f).Length)`** dans `BackupService` : recalcule la taille restante a chaque iteration — complexite O(n²).

---

## 8. Points de Friction et Ameliorations

### 8.1 Dettes techniques

| # | Probleme | Fichier(s) | Impact | Priorite |
|---|---|---|---|---|
| 1 | **Duplication BackupService / BackupEngine** | `BackupService.cs`, `BackupEngine.cs` | Deux moteurs de sauvegarde coexistent avec une logique dupliquee (copie, logging, differential). `BackupService` est un vestige v2.0 qui devrait etre supprime ou refactorise. | Haute |
| 2 | **Chemin relatif fragile** | Multiples services | `Path.Combine(BaseDirectory, "..", "..", "..", "..", "..")` repete dans 4 fichiers pour remonter a la racine du projet. Cassera si la structure de build change. | Haute |
| 3 | **Cle de chiffrement hardcodee** | `CryptoSoft/Program.cs:28` | Faille de securite. Devrait etre passee en parametre ou lue depuis un vault. | Haute |
| 4 | **Mode XOR non implemente** | `CryptoSoftService.cs`, `CryptoSoft/Program.cs` | L'UI propose XOR mais CryptoSoft ne fait que de l'AES. Code mort / feature fantome. | Moyenne |
| 5 | **HTTP synchrone dans thread de copie** | `LoggerService.cs:191` | `.GetAwaiter().GetResult()` peut bloquer et causer des deadlocks. | Moyenne |
| 6 | **Pas de tests unitaires** | Projet entier | Aucun projet de test dans la solution. | Haute |
| 7 | **Code-behind pour ComboBox** | `MainWindow.xaml.cs` | Les `SelectionChanged` pourraient etre geres en pur MVVM avec des `IValueConverter` ou des bindings plus sophistiques. | Basse |
| 8 | **LogServer sans authentification** | `LogServer/Program.cs` | Endpoint ouvert, pas d'API key ni de JWT. | Moyenne |
| 9 | **Lock statique dans ConfigService** | `ConfigService.cs:15` | Le lock est `static` mais les instances de `ConfigService` ne le sont pas. Si deux instances sont creees avec des repertoires differents, elles se bloquent mutuellement inutilement. | Basse |
| 10 | **`Properties/Program.cs` orphelin** | `Properties/Program.cs` | Point d'entree console legacy qui reference `ConsoleView` (inexistant dans le code actuel). Code mort. | Basse |

### 8.2 Ameliorations recommandees

1. **Supprimer `BackupService.cs`** et centraliser toute la logique dans `BackupEngine`. Le service legacy est redondant et source de confusion.

2. **Centraliser la resolution du chemin de base** dans une classe utilitaire ou une constante de configuration au lieu de repeter `Path.Combine(BaseDirectory, "..", "..", "..", "..", "..")` partout.

3. **Ajouter un projet de tests** (`EasySave.Tests`) avec xUnit ou NUnit pour tester :
   - `ConfigService` (serialisation/deserialisation)
   - `BackupEngine` (logique de priorite, differential)
   - `CryptoSoft` (chiffrement/dechiffrement)

4. **Rendre `SendLogToServer` asynchrone** en utilisant `await _httpClient.PostAsync()` avec `async Task SaveLog()` propageant l'asynchronisme dans toute la chaine.

5. **Securiser le LogServer** : ajouter une API key dans le header (`X-API-Key`), activer HTTPS, valider le schema JSON en entree.

6. **Implementer le dechiffrement** dans CryptoSoft pour permettre la restauration des sauvegardes chiffrees.

7. **Implementer reellement le mode XOR** ou le retirer de l'interface pour eviter la confusion.

8. **Optimiser les logs** : utiliser un append-only pattern (ecrire en fin de fichier) plutot que de deserialiser/re-serialiser l'integralite du fichier a chaque entree.

9. **Throttler les ecritures de state.json** : bufferiser et n'ecrire que toutes les N operations ou toutes les X millisecondes.

10. **Injection de dependances** : introduire un conteneur DI (meme simple) pour faciliter les tests et reduire le couplage entre les services.

---

## Annexe — Cartographie des fichiers

| Fichier | Lignes | Role |
|---|---|---|
| `Code/EasySave.app/App.xaml.cs` | 78 | Bootstrap WPF + CLI |
| `Code/EasySave.app/Model/AppSettings.cs` | 44 | Modele de configuration |
| `Code/EasySave.app/Model/BackupJob.cs` | 19 | Modele de travail |
| `Code/EasySave.app/Model/RealTimeState.cs` | 42 | Modele d'etat temps reel |
| `Code/EasySave.app/Services/BackupEngine.cs` | 592 | Moteur de sauvegarde v3.0 |
| `Code/EasySave.app/Services/BackupService.cs` | 268 | Moteur de sauvegarde v2.0 (legacy) |
| `Code/EasySave.app/Services/ConfigService.cs` | 145 | Persistance config JSON |
| `Code/EasySave.app/Services/CryptoSoftService.cs` | 114 | Bridge vers CryptoSoft.exe |
| `Code/EasySave.app/Services/BusinessSoftwareService.cs` | 42 | Detection processus metier |
| `Code/EasySave.app/Services/RealTimeStateService.cs` | 142 | Etat temps reel (state.json) |
| `Code/EasySave.app/ViewModel/MainViewModel.cs` | 646 | ViewModel principal |
| `Code/EasySave.app/ViewModel/JobViewModel.cs` | 163 | ViewModel par-job (temps reel) |
| `Code/EasySave.app/ViewModel/ViewModelBase.cs` | 24 | Base INPC |
| `Code/EasySave.app/ViewModel/RelayCommand.cs` | 33 | Implementation ICommand |
| `Code/EasySave.app/View/MainWindow.xaml` | 686 | Interface XAML |
| `Code/EasySave.app/View/MainWindow.xaml.cs` | 132 | Code-behind |
| `Code/EasySave.app/Resources/Lang_en.xaml` | 89 | Dictionnaire anglais |
| `Code/EasySave.app/Resources/Lang_fr.xaml` | 89 | Dictionnaire francais |
| `Code/EasySave.app/Resources/Lang_kab.xaml` | 89 | Dictionnaire kabyle |
| `Code/EasySave.dll/Models/LogEntry.cs` | 17 | Modele de log |
| `Code/EasySave.dll/Services/LoggerService.cs` | 200 | Service de logging dual (local+docker) |
| `Code/CryptoSoft/Program.cs` | 139 | Chiffrement AES-256 mono-instance |
| `Code/LogServer/Program.cs` | 579 | Serveur REST + dashboard HTML |
| **Total** | **~4392** | |

---

*Document genere par analyse statique exhaustive du code source. Aucune zone d'ombre restante.*
