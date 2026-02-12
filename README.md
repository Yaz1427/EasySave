# EasySave v2.0

Application graphique (WPF) de sauvegarde de fichiers developpee en C# (.NET 8).
Projet realise dans le cadre du module Genie Logiciel - CESI 3eme annee.

---

## Nouveautes v2.0

- Interface graphique WPF (remplacement de la console)
- Cryptage des fichiers integre (AES-256 ou XOR, au choix dans les parametres)
- Detection de logiciel metier (blocage de la sauvegarde si le processus est actif)
- Arret de sauvegarde en cours (bouton Stop)
- Logs triables par colonne (clic sur l'en-tete avec indicateur fleche)
- Support de 3 langues : English, Francais, Taqbaylit (Kabyle)
- Format de log configurable (JSON ou XML)
- Sauvegarde du dossier entier (le dossier source est conserve tel quel dans la cible)

## Fonctionnalites

- Creation, edition et suppression de travaux de sauvegarde
- Sauvegarde complete (Full) et differentielle (Differential)
- Copie recursive de tous les fichiers et sous-repertoires
- Cryptage automatique des fichiers selon les extensions configurees (AES-256 / XOR)
- Temps de cryptage mesure et logue
- Detection de logiciel metier (arret automatique si le processus est en cours)
- Visualisation et tri des logs dans l'application
- Fichier d'etat temps reel (state.json)
- Parametres persistants (langue, format de log, extensions, mode de cryptage, logiciel metier)

## Architecture

Le projet suit le pattern MVVM :

```
Code/
  EasySave.app/            <- Application WPF
    Model/                 <- Modeles de donnees (BackupJob, RealTimeState, AppSettings)
    View/                  <- Interface graphique (MainWindow.xaml)
    ViewModel/             <- Logique de coordination (MainViewModel)
    Services/              <- Services metier (BackupService, ConfigService, CryptoSoftService, etc.)
    Resources/             <- Dictionnaires de langues (Lang_en, Lang_fr, Lang_kab)

  EasySave.dll/            <- Librairie EasyLog (DLL)
    Models/                <- LogEntry
    Services/              <- LoggerService

  CryptoSoft/              <- Module de cryptage (console, AES-256)
```

La librairie EasyLog.dll est un projet separe (Dynamic Link Library) qui gere l'ecriture des logs. Elle est referencee par l'application principale et peut etre reutilisee dans d'autres projets.

## Prerequis

- .NET 8.0 SDK

## Compilation

```bash
dotnet build EasySave.sln
```

## Utilisation

Lancer l'application :

```bash
dotnet run --project Code/EasySave.app/EasySave.csproj
```

L'interface graphique comporte 4 onglets :

| Onglet       | Description                                      |
|--------------|--------------------------------------------------|
| **Jobs**     | Liste des travaux, execution, arret, suppression  |
| **Create/Edit** | Creation et modification de travaux            |
| **Logs**     | Visualisation des logs avec tri par colonne       |
| **Settings** | Langue, format de log, mode de cryptage, extensions, logiciel metier |

### Parametres disponibles

| Parametre              | Description                                           |
|------------------------|-------------------------------------------------------|
| Langue                 | English / Francais / Taqbaylit                        |
| Format de log          | JSON ou XML                                           |
| Mode de cryptage       | AES-256 (chiffrement fort) ou XOR (chiffrement leger) |
| Extensions a crypter   | Extensions de fichiers a chiffrer (ex: .txt; .docx)   |
| Logiciel metier        | Nom du processus qui bloque la sauvegarde             |

## Fichiers generes

### Configuration

- `jobs.json` : liste des travaux de sauvegarde (dans le repertoire de l'executable)
- `settings.json` : parametres de l'application

### Logs et etat

Emplacement : `LogsEasySave/` (a la racine du projet)

- `LogsEasySave/YYYY-MM-DD.json` (ou `.xml`) : log journalier (une entree par fichier copie)
- `LogsEasySave/state.json` : etat temps reel du dernier travail en cours

### Contenu du log journalier

Chaque entree contient :
- Horodatage
- Nom du travail de sauvegarde
- Chemin complet du fichier source
- Chemin complet du fichier de destination
- Taille du fichier (en octets)
- Temps de transfert en ms (negatif si erreur)
- Temps de cryptage en ms (0 si non crypte)

### Contenu du fichier d'etat

- Nom du travail
- Horodatage de la derniere action
- Etat (Active, End, Inactive)
- Nombre total de fichiers
- Taille totale des fichiers
- Progression (%)
- Fichiers et taille restants
- Fichier source et destination en cours
