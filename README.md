# EasySave v1.0

Application console de sauvegarde de fichiers developpee en C# (.NET).
Projet realise dans le cadre du module Genie Logiciel - CESI 3eme annee.

---

## Fonctionnalites

- Creation et gestion de jusqu'a 5 travaux de sauvegarde
- Sauvegarde complete (Full) et differentielle (Differential)
- Copie recursive de tous les fichiers et sous-repertoires
- Interface bilingue (Francais / English)
- Execution interactive ou via ligne de commande
- Fichier log journalier (un fichier JSON par jour)
- Fichier d'etat temps reel (state.json)

## Architecture

Le projet suit le pattern MVVM :

```
Code/
  EasySave.app/            <- Application console
    Model/                 <- Modeles de donnees (BackupJob, RealTimeState)
    View/                  <- Interface console (ConsoleView)
    ViewModel/             <- Logique de coordination (MainViewModel)
    Services/              <- Services metier (BackupService, ConfigService, RealTimeStateService)
    Program.cs             <- Point d'entree

  EasySave.dll/            <- Librairie EasyLog (DLL)
    Models/                <- LogEntry
    Services/              <- LoggerService
```

La librairie EasyLog.dll est un projet separe (Dynamic Link Library) qui gere l'ecriture des logs journaliers. Elle est referencee par l'application principale et peut etre reutilisee dans d'autres projets.

## Prerequis

- .NET 10.0 SDK (application)
- .NET 8.0 SDK (librairie EasyLog)

## Compilation

```bash
dotnet build Code/EasySave.app/EasySave.csproj
```

## Utilisation

### Mode interactif

```bash
dotnet run --project Code/EasySave.app/EasySave.csproj
```

Commandes disponibles :

| Commande         | Description                          |
|------------------|--------------------------------------|
| `help`           | Afficher le menu                     |
| `run <index>`    | Executer un job (ex: run 0)          |
| `runall`         | Executer tous les jobs               |
| `create`         | Creer un nouveau job de sauvegarde   |
| `list`           | Lister les jobs configures           |
| `delete <index>` | Supprimer un job (ex: delete 0)      |
| `exit`           | Quitter l'application                |

### Mode ligne de commande

```bash
# Executer les jobs 1 a 3
EasySave.exe 1-3

# Executer les jobs 1 et 3
EasySave.exe "1;3"
```

## Fichiers generes

Tous les fichiers sont au format JSON avec indentation pour lisibilite.

### Configuration

- `jobs.json` : liste des travaux de sauvegarde (dans le repertoire de l'executable)

### Logs et etat

Emplacement : `LogsEasySave/` (a la racine du projet)

- `LogsEasySave/Logs/YYYY-MM-DD.json` : log journalier (une entree par fichier copie)
- `LogsEasySave/state.json` : etat temps reel du dernier travail en cours

### Contenu du log journalier

Chaque entree contient :
- Horodatage
- Nom du travail de sauvegarde
- Chemin complet du fichier source
- Chemin complet du fichier de destination
- Taille du fichier (en octets)
- Temps de transfert en ms (negatif si erreur)

### Contenu du fichier d'etat

- Nom du travail
- Horodatage de la derniere action
- Etat (Active, End, Inactive)
- Nombre total de fichiers
- Taille totale des fichiers
- Progression (%)
- Fichiers et taille restants
- Fichier source et destination en cours
