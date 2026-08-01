# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Un sélecteur local et non officiel de comptes et de sites API Windows pour Codex, Claude Code et OpenCode.

Coding Agent Account Switcher conserve des instantanés chiffrés des identifiants locaux avec uniquement les champs de fournisseur API nécessaires. Les autres paramètres, MCP, compétences, extensions et historiques restent à leur emplacement d’origine.

> [!IMPORTANT]
> Ce projet n’est ni affilié, ni approuvé, ni sponsorisé par OpenAI ou Anthropic. Il ne transfère pas les abonnements, ne contourne pas l’authentification, ne partage pas de comptes et ne permet pas de contourner les règles d’un fournisseur ou d’une organisation.

La mention « inspiré d’iOS 18 » décrit uniquement l’orientation visuelle générale. Apple n’est pas affiliée à ce projet, qui n’intègre aucune police, aucun symbole, aucune illustration et aucune marque d’Apple.

## Téléchargement

Téléchargez la version actuelle depuis la [release latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) :

- **Programme d’installation recommandé :** `coding-agent-account-switcher-setup-win-x64.exe` installe l’application pour l’utilisateur Windows actuel sans privilèges d’administrateur et crée des entrées dans le menu Démarrer et pour la désinstallation.
- **Exécutable portable :** téléchargez `coding-agent-account-switcher-portable-win-x64.exe` et exécutez-le directement, sans installation.
- La release contient un fichier de somme de contrôle SHA-256 correspondant pour chaque exécutable.

Le programme d’installation n’active pas automatiquement **Démarrer avec Windows** et ne modifie aucun fichier d’authentification ou de configuration de Codex, Claude Code ou OpenCode. La désinstallation conserve les instantanés de comptes chiffrés et les paramètres de l’application afin qu’ils restent disponibles après une réinstallation. Le programme d’installation et l’exécutable portable ne sont actuellement pas signés ; Windows SmartScreen peut donc afficher un avertissement de réputation.

## Fonctionnalités

- Interface WPF native pour Windows avec des cartes en verre inspirées d’iOS 18.
- Langues intégrées : anglais, chinois simplifié, chinois traditionnel, espagnol, français, allemand, japonais, coréen, portugais brésilien, russe, arabe et hindi.
- Paramètres permettant de choisir la langue d’affichage et, facultativement, de lancer l’application avec Windows pour l’utilisateur actuel.
- Profils personnels, professionnels et de sites API nommés pour Codex, Claude Code et OpenCode.
- Protection par détection des processus, qui bloque le changement jusqu’à la fermeture des applications concernées.
- Les fichiers d’authentification restent des octets opaques. Seuls les champs gérés indiqués ci-dessous sont analysés et fusionnés ; les secrets ne sont jamais affichés ni journalisés.
- Instantanés de profils chiffrés avec Windows DPAPI pour l’utilisateur Windows actuel.
- Remplacement atomique des fichiers gérés dans le même répertoire, avec prise en charge de la restauration.
- Confirmation explicite avant qu’une session active modifiée puisse écraser l’instantané du dernier profil sélectionné.
- Action sûre **Restaurer l’instantané** pour le dernier profil sélectionné, avec confirmation liée aux octets d’authentification exacts qui seront remplacés.
- Fonctionnement uniquement local, sans analyse ni télémétrie.
- Compilation Windows glissante `latest` produite automatiquement depuis `main`.

## Configuration de compte et d’API prise en charge

| Fournisseur | Fichiers gérés | Configuration gérée sélectivement |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` et `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, la table active sélectionnée de `model_providers` et `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` et `settings.json` | Uniquement `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC` et le champ de compatibilité `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | `%USERPROFILE%\.local\share\opencode\auth.json` facultatif ; couches globales `config.json`, `opencode.json`, `opencode.jsonc` ; puis `OPENCODE_CONFIG` s’il est défini | Identifiants opaques `/connect` et profil API effectif `provider`, `model`, `small_model` |

L’application fusionne ces champs sans remplacer le fichier entier. Pour Codex, `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`, `cli_auth_credentials_store`, MCP, les compétences, les sessions et toutes les autres valeurs sont préservés. Pour Claude Code, toutes les autres entrées `env`, `.claude.json`, les extensions, MCP, les paramètres de projet et l’historique sont préservés. Pour OpenCode, les valeurs sémantiques sans rapport sont préservées dans chaque fichier global ou personnalisé participant.

La racine de configuration par défaut d’OpenCode est `%USERPROFILE%\.config` et sa racine de données par défaut est `%USERPROFILE%\.local\share`. Une valeur absolue de `XDG_CONFIG_HOME` ou `XDG_DATA_HOME` remplace la racine par défaut correspondante ; une valeur vide ne la remplace pas. Tous les remplacements de chemin OpenCode non vides (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG` et `OPENCODE_CONFIG_DIR`) doivent être absolus ; une valeur relative bloque la capture et le changement avant toute écriture. La configuration globale charge, dans cet ordre, `opencode\config.json`, `opencode.json` et `opencode.jsonc` sous la racine de configuration ; chaque couche ultérieure prévaut. `OPENCODE_CONFIG` est chargé en dernier. L’application capture le résultat effectif de `provider`, `model` et `small_model` comme un seul profil API. Les identifiants `/connect` sont lus depuis `opencode\auth.json` sous la racine de données.

OpenCode accepte les identifiants de `/connect` dans son `auth.json` et les clés API intégrées à l’objet `provider` ; l’instantané conserve le style présent, ou les deux.

À l’application, `provider`, `model` et `small_model` sont retirés des autres couches globales afin qu’un ancien point d’accès ne prévale pas. Avec `OPENCODE_CONFIG`, la cible y est écrite et les trois couches globales sont nettoyées. Sans lui, les valeurs non vides sont normalisées dans `opencode.jsonc` global et retirées de `config.json` et `opencode.json`, même si seul JSON ou l’ancien fichier existait. Un instantané limité à l’authentification ou avec configuration gérée vide reste valide : il nettoie les valeurs existantes sans créer de `opencode.jsonc` vide.

La fusion sélective préserve les valeurs sémantiques sans rapport, mais ne garantit pas la conservation octet pour octet de la mise en forme ou des commentaires si JSON ou TOML est resérialisé.

Avant de capturer ou de changer OpenCode, l’application vérifie en lecture seule les variables d’environnement connues de priorité supérieure. Une valeur non vide de `OPENCODE_AUTH_CONTENT` bloque l’opération. `OPENCODE_CONFIG_CONTENT` n’est autorisé que s’il s’agit de JSON ou JSONC valide sans les clés de premier niveau gérées `provider`, `model` ou `small_model` ; un contenu non valide ou l’une de ces clés bloque l’opération. Si `OPENCODE_CONFIG_DIR` est défini, ses fichiers `opencode.json` et `opencode.jsonc` sont vérifiés ; un fichier illisible ou non valide, ou une clé gérée dans l’un d’eux, bloque l’opération. Une configuration en ligne ou de répertoire contenant uniquement des clés sans rapport est autorisée. L’application ne modifie aucune de ces sources fournies par l’environnement.

`CODEX_HOME` et `CLAUDE_CONFIG_DIR` changent les racines correspondantes. La configuration OpenCode de projet, les sources administrées centralement et les variables d’environnement propres au fournisseur restent non gérées et peuvent remplacer le profil global sélectionné après un changement ; l’application ne les recherche ni ne les modifie. La protection des processus contrôle `opencode` et `opencode-cli`. `disable_response_storage`, `features.responses_websockets_v2` et `CLAUDE_CODE_ATTRIBUTION_HEADER` sont des champs de compatibilité, sans affirmer que toutes les versions actuelles les documentent.

Codex doit utiliser le stockage d’identifiants dans un fichier. Si votre installation utilise le gestionnaire d’identifiants du système d’exploitation, ajoutez ce paramètre à `config.toml` dans la racine Codex active (`%USERPROFILE%\.codex` par défaut, ou `CODEX_HOME` lorsqu’il est défini) :

```toml
cli_auth_credentials_store = "file"
```

Consultez la documentation officielle sur [l’authentification Codex](https://developers.openai.com/codex/auth) et [l’authentification Claude Code](https://code.claude.com/docs/en/authentication) pour connaître les contrats de stockage actuels.

## Fonctionnement

1. Connectez-vous avec le flux officiel ou configurez un site API pris en charge dans les fichiers habituels du fournisseur.
2. Fermez complètement Codex, Claude Code, OpenCode et tout client ou extension associé.
3. Enregistrez le compte et les paramètres API gérés sous un nom tel que `Personal`.
4. Connectez-vous à un autre compte ou configurez un autre site API, puis enregistrez-le comme `Work`.
5. Sélectionnez un profil. Si un processus associé est actif, le changement est bloqué et aucun fichier géré n’est modifié.
6. Après une confirmation liée au contenu, l’instantané géré actuel est enregistré dans le dernier profil chiffré afin de conserver les jetons actualisés et les changements API voulus. L’instantané exact antérieur est aussi conservé dans un bloc de récupération DPAPI propre à la transaction.

L’application indique **Dernier profil sélectionné**, et non « profil actuel vérifié ». Si le fichier actif ne correspond plus à l’instantané enregistré, le changement s’interrompt avant toute écriture. Ne confirmez que si la modification correspond à l’actualisation du même compte. Si vous vous êtes connecté à un autre compte en dehors de l’application, choisissez d’abord **Enregistrer comme nouveau** (ou remplacez explicitement le profil existant portant le bon nom).

Le bouton **Restaurer l’instantané** de la carte du dernier profil sélectionné vérifie si le fichier actif correspond encore à l’instantané. S’il diffère, l’application avertit que la session actuelle non enregistrée sera remplacée et lie l’autorisation à ces octets précis. Une autre session active ne peut pas réutiliser une confirmation antérieure. Pendant la restauration, un identifiant de récupération propre à la transaction et chiffré avec DPAPI conserve les octets antérieurs jusqu’à la validation ou l’annulation de l’opération.

Chaque transaction composite ou multifichier stocke l’instantané géré actuel exact dans le bloc de récupération chiffré avant le journal, même s’il correspond à la source enregistrée. Cela permet d’annuler une écriture partielle et de mettre à niveau sans risque un ancien profil limité aux identifiants. Après une interruption, la récupération privilégie cet instantané antérieur et synchronise le profil source si elle le restaure. Les anciens journaux sans bloc se replient toujours sur la source enregistrée. Si l’état actif ne correspond ni à la source conservée ni à la cible, le journal et le bloc chiffré restent disponibles pour une récupération manuelle.

La validation multifichier est fermée par défaut : elle supprime d’abord l’authentification séparée, fusionne ensuite la configuration atomiquement, puis installe l’authentification cible en dernier. Une interruption peut laisser l’authentification absente, mais ne peut pas associer des identifiants au point d’accès du profil opposé ; la récupération termine ou annule depuis l’instantané chiffré antérieur.

Les anciens profils bruts Codex et Claude Code sont interprétés comme des identifiants avec une configuration API gérée vide. Leur activation efface les champs gérés de routage API et de modèle afin de ne pas réutiliser le point d’accès tiers du profil précédent. Configurez ensuite le modèle/API voulu et recapturez le profil. Un ancien profil source actif passe au format composite lorsqu’on le quitte.

L’application ne garantit pas une session permanente. Une révocation côté fournisseur, une règle d’organisation, le SSO, la MFA ou l’expiration d’un jeton peuvent toujours imposer une connexion normale avec le client officiel.

## Paramètres

Ouvrez **Paramètres** dans la fenêtre de l’application pour choisir la langue d’affichage ou décider si l’application démarre avec Windows. La langue choisie est enregistrée localement pour l’utilisateur Windows actuel et peut être modifiée à tout moment.

**Démarrer avec Windows** ajoute une entrée pour cette application sous `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Cette option ne concerne que l’utilisateur Windows actuel et ne nécessite aucun droit d’administrateur. Sa désactivation supprime uniquement l’entrée de démarrage appartenant à Coding Agent Account Switcher et ne modifie aucune autre application au démarrage.

## Modèle de sécurité

- Les profils chiffrés sont stockés sous `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Chaque ensemble normalisé de fichiers gérés possède son coffre, son état, son journal et son mutex indépendants. Modifier `CODEX_HOME`, `CLAUDE_CONFIG_DIR` ou `OPENCODE_CONFIG` crée un autre ensemble de profils.
- DPAPI `CurrentUser` empêche un autre compte Windows de déchiffrer directement le profil, mais ne protège pas contre un logiciel malveillant déjà exécuté sous le même utilisateur Windows.
- En dehors du fichier d’authentification actif normal du fournisseur, les octets déchiffrés n’existent que brièvement en mémoire et pendant le remplacement atomique dans le même répertoire lors de la capture ou du changement.
- Les fichiers temporaires et de sauvegarde du remplacement utilisent l’identifiant de la transaction de récupération. Une opération normale supprime ces deux fichiers précis. Après une interruption, la récupération restaure un fichier actif manquant depuis l’instantané source chiffré ou l’identifiant de récupération propre à la transaction, puis supprime tous les fichiers de préparation appartenant exactement à la transaction avant d’effacer le journal. Le bloc de récupération chiffré n’est supprimé qu’après le journal, selon le principe du meilleur effort.
- L’application ne téléverse aucun identifiant. Ceux-ci ne doivent jamais apparaître dans un journal, un ticket, un rapport d’incident, une donnée de test ou un commit du dépôt.
- La détection des processus est défensive et sans garantie absolue. N’ouvrez pas Codex, Claude Code ou OpenCode avant la fin de l’opération.
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

Les contributions sont les bienvenues. Lisez [CONTRIBUTING.md](CONTRIBUTING.md). Les tests doivent utiliser des identifiants et configurations synthétiques temporaires sans jamais accéder aux vrais fichiers Codex, Claude Code ou OpenCode.

## Licence

[MIT](LICENSE)
