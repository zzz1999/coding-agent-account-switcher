# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Un sélecteur de comptes Windows non officiel et local pour Codex et Claude Code.

Coding Agent Account Switcher conserve des instantanés nommés et chiffrés des fichiers d’authentification locaux utilisés par Codex et Claude Code. Il ne remplace que l’instantané d’authentification ; vos paramètres habituels, votre configuration MCP, vos compétences, vos extensions et votre historique de projets restent à leur emplacement d’origine.

> [!IMPORTANT]
> Ce projet n’est ni affilié, ni approuvé, ni sponsorisé par OpenAI ou Anthropic. Il ne transfère pas les abonnements, ne contourne pas l’authentification, ne partage pas de comptes et ne permet pas de contourner les règles d’un fournisseur ou d’une organisation.

La mention « inspiré d’iOS 18 » décrit uniquement l’orientation visuelle générale. Apple n’est pas affiliée à ce projet, qui n’intègre aucune police, aucun symbole, aucune illustration et aucune marque d’Apple.

## Téléchargement

Téléchargez la version actuelle depuis la [release latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) :

- **Programme d’installation recommandé :** `coding-agent-account-switcher-setup-win-x64.exe` installe l’application pour l’utilisateur Windows actuel sans privilèges d’administrateur et crée des entrées dans le menu Démarrer et pour la désinstallation.
- **Exécutable portable :** téléchargez `coding-agent-account-switcher-portable-win-x64.exe` et exécutez-le directement, sans installation.
- La release contient un fichier de somme de contrôle SHA-256 correspondant pour chaque exécutable.

Le programme d’installation n’active pas automatiquement **Démarrer avec Windows** et ne modifie aucun fichier d’authentification ou de configuration de Codex ou Claude Code. La désinstallation conserve les instantanés de comptes chiffrés et les paramètres de l’application afin qu’ils restent disponibles après une réinstallation. Le programme d’installation et l’exécutable portable ne sont actuellement pas signés ; Windows SmartScreen peut donc afficher un avertissement de réputation.

## Fonctionnalités

- Interface WPF native pour Windows avec des cartes en verre inspirées d’iOS 18.
- Langues intégrées : anglais, chinois simplifié, chinois traditionnel, espagnol, français, allemand, japonais, coréen, portugais brésilien, russe, arabe et hindi.
- Paramètres permettant de choisir la langue d’affichage et, facultativement, de lancer l’application avec Windows pour l’utilisateur actuel.
- Profils personnels et professionnels nommés pour Codex et Claude Code.
- Protection par détection des processus, qui bloque le changement jusqu’à la fermeture des applications concernées.
- Les identifiants sont traités comme des octets opaques : aucun jeton n’est analysé, aucune adresse e-mail n’est extraite et aucun identifiant n’est journalisé.
- Instantanés de profils chiffrés avec Windows DPAPI pour l’utilisateur Windows actuel.
- Remplacement atomique des identifiants dans le même répertoire, avec prise en charge de la restauration.
- Confirmation explicite avant qu’une session active modifiée puisse écraser l’instantané du dernier profil sélectionné.
- Action sûre **Restaurer l’instantané** pour le dernier profil sélectionné, avec confirmation liée aux octets d’authentification exacts qui seront remplacés.
- Fonctionnement uniquement local, sans analyse ni télémétrie.
- Compilation Windows glissante `latest` produite automatiquement depuis `main`.

## Fichiers d’authentification pris en charge

| Fournisseur | Fichier d’authentification remplacé par défaut | Configuration laissée intacte |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, compétences, MCP, sessions et autres données |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, extensions, MCP, paramètres de projets et historique des sessions |

Si `CODEX_HOME` ou `CLAUDE_CONFIG_DIR` est défini, l’application utilise la racine d’authentification correspondante. Elle ne remplace toujours que `auth.json` ou `.credentials.json` ; les fichiers de configuration voisins restent intacts.

Codex doit utiliser le stockage d’identifiants dans un fichier. Si votre installation utilise le gestionnaire d’identifiants du système d’exploitation, ajoutez ce paramètre à `config.toml` dans la racine Codex active (`%USERPROFILE%\.codex` par défaut, ou `CODEX_HOME` lorsqu’il est défini) :

```toml
cli_auth_credentials_store = "file"
```

Consultez la documentation officielle sur [l’authentification Codex](https://developers.openai.com/codex/auth) et [l’authentification Claude Code](https://code.claude.com/docs/en/authentication) pour connaître les contrats de stockage actuels.

## Fonctionnement

1. Connectez-vous au premier compte avec le flux officiel du fournisseur.
2. Fermez complètement Codex/Claude Code ainsi que tout client local ou toute extension associée.
3. Enregistrez la session actuelle sous un nom choisi, par exemple `Personal`.
4. Connectez-vous au deuxième compte et enregistrez-le sous un autre nom, par exemple `Work`.
5. Sélectionnez un profil enregistré. L’application vérifie les processus associés avant toute modification. Si l’un d’eux est actif, le changement est bloqué et aucun fichier d’identifiants n’est modifié.
6. Lors du passage à un autre profil, le fichier d’authentification actuel est enregistré dans le dernier profil chiffré sélectionné après une confirmation liée aux octets, afin de conserver les jetons actualisés. Avant que le journal ne soit rendu persistant, ces octets exacts confirmés avant le changement sont également conservés dans un bloc de récupération propre à la transaction et chiffré avec DPAPI.

L’application indique **Dernier profil sélectionné**, et non « profil actuel vérifié ». Si le fichier actif ne correspond plus à l’instantané enregistré, le changement s’interrompt avant toute écriture. Ne confirmez que si la modification correspond à l’actualisation du même compte. Si vous vous êtes connecté à un autre compte en dehors de l’application, choisissez d’abord **Enregistrer comme nouveau** (ou remplacez explicitement le profil existant portant le bon nom).

Le bouton **Restaurer l’instantané** de la carte du dernier profil sélectionné vérifie si le fichier actif correspond encore à l’instantané. S’il diffère, l’application avertit que la session actuelle non enregistrée sera remplacée et lie l’autorisation à ces octets précis. Une autre session active ne peut pas réutiliser une confirmation antérieure. Pendant la restauration, un identifiant de récupération propre à la transaction et chiffré avec DPAPI conserve les octets antérieurs jusqu’à la validation ou l’annulation de l’opération.

Les changements de profil ordinaires utilisent la même preuve de récupération chiffrée lorsque la session active confirmée diffère de son instantané source enregistré. Après une interruption, la récupération privilégie ces octets exacts antérieurs au changement et synchronise l’instantané source si elle les restaure. Les anciens journaux sans bloc de récupération restent compatibles grâce à un repli sur l’instantané source enregistré. Si le fichier actif ne correspond ni à la source conservée ni à la cible, le journal et le bloc de récupération chiffré restent disponibles pour une récupération manuelle.

L’application ne garantit pas une session permanente. Une révocation côté fournisseur, une règle d’organisation, le SSO, la MFA ou l’expiration d’un jeton peuvent toujours imposer une connexion normale avec le client officiel.

## Paramètres

Ouvrez **Paramètres** dans la fenêtre de l’application pour choisir la langue d’affichage ou décider si l’application démarre avec Windows. La langue choisie est enregistrée localement pour l’utilisateur Windows actuel et peut être modifiée à tout moment.

**Démarrer avec Windows** ajoute une entrée pour cette application sous `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Cette option ne concerne que l’utilisateur Windows actuel et ne nécessite aucun droit d’administrateur. Sa désactivation supprime uniquement l’entrée de démarrage appartenant à Coding Agent Account Switcher et ne modifie aucune autre application au démarrage.

## Modèle de sécurité

- Les profils chiffrés sont stockés sous `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Chaque emplacement normalisé de fichier d’authentification possède son coffre, son état actif, son journal de récupération et son mutex, isolés par un hachage. Modifier `CODEX_HOME` ou `CLAUDE_CONFIG_DIR` crée donc un ensemble de profils indépendant au lieu de réutiliser le compte actif d’un autre emplacement.
- DPAPI `CurrentUser` empêche un autre compte Windows de déchiffrer directement le profil, mais ne protège pas contre un logiciel malveillant déjà exécuté sous le même utilisateur Windows.
- En dehors du fichier d’authentification actif normal du fournisseur, les octets déchiffrés n’existent que brièvement en mémoire et pendant le remplacement atomique dans le même répertoire lors de la capture ou du changement.
- Les fichiers temporaires et de sauvegarde du remplacement utilisent l’identifiant de la transaction de récupération. Une opération normale supprime ces deux fichiers précis. Après une interruption, la récupération restaure un fichier actif manquant depuis l’instantané source chiffré ou l’identifiant de récupération propre à la transaction, puis supprime tous les fichiers de préparation appartenant exactement à la transaction avant d’effacer le journal. Le bloc de récupération chiffré n’est supprimé qu’après le journal, selon le principe du meilleur effort.
- L’application ne téléverse aucun identifiant. Ceux-ci ne doivent jamais apparaître dans un journal, un ticket, un rapport d’incident, une donnée de test ou un commit du dépôt.
- La détection des processus est défensive et sans garantie absolue. Un processus nouvellement lancé peut entrer en concurrence avec un changement ; n’ouvrez pas Codex ou Claude Code avant la fin de l’opération.
- Si votre compte professionnel est géré par une organisation, obtenez son autorisation avant de conserver un autre instantané d’authentification local chiffré.

Lisez [SECURITY.md](SECURITY.md) et [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) avant de modifier le code qui manipule les identifiants.

## Compiler depuis les sources

Prérequis :

- Windows 10 ou Windows 11
- .NET SDK 8.0.400 ou une feature band .NET 8 plus récente

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

Créer une compilation Windows x64 autonome :

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## Version glissante latest

`.github/workflows/latest-release.yml` s’exécute à chaque push vers `main` :

1. Restauration et test de la solution.
2. Publication d’une compilation portable Windows x64 autonome.
3. Création du programme d’installation Windows x64 par utilisateur.
4. Préparation de l’exécutable portable et des sommes de contrôle SHA-256 des deux exécutables.
5. Suppression de la seule release précédente et du seul tag précédent nommés `latest`.
6. Publication d’une nouvelle release `latest` avec le programme d’installation, l’exécutable portable et les sommes de contrôle pour le commit actuel.

Ce workflow ne supprime jamais les releases versionnées. L’option **immutable releases** de GitHub doit rester désactivée pour le tag glissant `latest`, et les règles de branche ou de tag doivent autoriser le workflow à supprimer `latest`. Un dépôt exigeant des releases immuables doit employer des tags de compilation uniques.

Le programme d’installation et l’exécutable portable glissants ne sont actuellement pas signés ; Windows SmartScreen peut donc afficher un avertissement de réputation. Examinez le code source et vérifiez la somme SHA-256 publiée correspondante avant d’exécuter l’un ou l’autre exécutable.

## Contribuer

Les contributions sont les bienvenues. Lisez [CONTRIBUTING.md](CONTRIBUTING.md). Les tests doivent utiliser des fichiers d’identifiants factices et temporaires, et ne doivent jamais accéder aux véritables fichiers d’authentification Codex ou Claude Code d’un développeur.

## Licence

[MIT](LICENSE)
