# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Icône Coding Agent Account Switcher">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Une application Windows simple pour passer d’un compte ou d’une configuration de
site API enregistré à un autre dans Codex, Claude Code et OpenCode.

Elle ne change que les informations de compte et les réglages API compatibles
enregistrés dans le profil. Vos serveurs MCP, compétences, extensions, projets et
historiques restent à leur emplacement habituel.

> [!IMPORTANT]
> Ce projet communautaire est non officiel. Il n’est affilié ni à OpenAI, ni à
> Anthropic, Apple ou au projet OpenCode. Il ne transfère pas d’abonnement, ne
> contourne pas la connexion et ne remplace pas les règles d’une organisation.

## Téléchargement

Téléchargez la version actuelle depuis la
[dernière release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) :

- **Programme d’installation :** <code>CAAS-vX.Y.Z-Setup-x64.exe</code>
- **Application portable :** <code>CAAS-vX.Y.Z-Portable-x64.exe</code>
- **Sommes de contrôle :** chaque exécutable possède un fichier SHA-256 correspondant

L’installation concerne uniquement l’utilisateur actuel, ne nécessite aucun droit
d’administrateur et n’active pas **Démarrer avec Windows** sans votre accord. Les
versions actuelles ne sont pas signées ; Windows SmartScreen peut donc afficher
un avertissement.

## Fonctions principales

- Enregistrez des profils personnels, professionnels et de sites API clairement nommés.
- Changez de compte Codex, Claude Code ou OpenCode en quelques clics.
- Conservez les adresses, clés et routages de fournisseur API compatibles avec le bon profil.
- Renommez un profil par double-clic ou supprimez un instantané local.
- Obtenez une confirmation claire du profil actif après le changement.
- Bloquez le changement tant qu’une application associée est ouverte.
- Utilisez l’une des 12 langues intégrées.
- Conservez le thème clair ou sombre et la langue entre les lancements.
- Dans une même session Windows, rouvrir l’application restaure et remet au
  premier plan la fenêtre existante au lieu d’en créer une autre.
- Démarrez facultativement avec Windows et recherchez les mises à jour manuellement.

Tout reste sur votre ordinateur. L’application ne contient ni analyse ni télémétrie.

## Applications prises en charge

| Application | Éléments changés | Éléments conservés |
| --- | --- | --- |
| Codex | Connexion et configuration du fournisseur API sélectionné | Modèles, options de revue/raisonnement, fonctionnalités, MCP, compétences, sessions, historique et autres réglages |
| Claude Code | Connexion et réglages compatibles d’adresse/clé API | Extensions, MCP, projets, historique et autres réglages |
| OpenCode | Connexion enregistrée et réglages compatibles de fournisseur/modèle | Configuration de projet et autres réglages |

Seuls les champs de compte pris en charge par le projet sont modifiés. Consultez
[l’architecture](docs/ARCHITECTURE.md) pour la liste exacte.

## Démarrage rapide

1. Connectez-vous normalement ou configurez le site API souhaité.
2. Ouvrez l’application et choisissez le fournisseur correspondant.
3. Sélectionnez **Enregistrer le compte actuel** et nommez-le, par exemple <code>Personal</code>.
4. Connectez-vous au second compte ou configurez un autre site API.
5. Enregistrez-le, par exemple sous <code>Work</code>.
6. Avant de changer de compte, fermez complètement l’application concernée, puis sélectionnez un profil enregistré.

Vous pouvez enregistrer un compte pendant que son application est ouverte. Avant
un changement, l’application recherche les processus encore actifs. Si quelque
chose est ouvert, elle vous demande de le fermer et ne change rien.

Si le compte actuel a changé depuis son enregistrement, une confirmation est
demandée. S’il s’agit réellement d’un autre compte, enregistrez-le d’abord comme
nouveau profil.

## Profils et réglages

- **Renommer :** double-cliquez sur le nom du profil.
- **Supprimer :** utilisez la corbeille. Seul l’instantané local chiffré est
  supprimé ; le compte n’est pas effacé et la session reste ouverte.
- **Dernier sélectionné :** indique le profil activé le plus récemment par l’application.
- **Instantané endommagé :** un instantané absent ou illisible est masqué, tandis
  que les profils valides restent disponibles.
- **Thème et langue :** ils sont mémorisés pour l’utilisateur Windows actuel.
- **Démarrer avec Windows :** facultatif, pour l’utilisateur actuel et sans droits d’administrateur.
- **Rechercher des mises à jour :** uniquement sur clic, sans téléchargement ni installation automatique.

## Confidentialité et sécurité

- Les profils sont chiffrés avec Windows DPAPI pour l’utilisateur actuel.
- Les identifiants et clés API ne sont ni affichés ni écrits dans les journaux.
- Les profils sont stockés dans <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>.
- Le changement utilise un remplacement protégé et une récupération pour limiter les modifications partielles.
- L’application n’envoie ni identifiants, ni noms de profils, ni identifiant
  d’appareil, ni télémétrie.
- Les comptes professionnels peuvent être soumis aux règles de l’organisation ;
  obtenez une autorisation avant de conserver une autre copie locale de connexion.

Lisez [SECURITY.md](SECURITY.md) avant de signaler un problème de sécurité.

## Limites

- Une déconnexion côté fournisseur, l’expiration du jeton, SSO, MFA ou les règles
  de l’organisation peuvent imposer une connexion normale.
- Les conversations Codex existantes restent liées au fournisseur utilisé lors
  de leur création. Après un changement de fournisseur, démarrez une nouvelle
  conversation ou revenez au fournisseur d’origine pour poursuivre l’ancienne.
- L’application ne transfère pas les abonnements et ne garantit pas une connexion permanente.
- Seuls Windows 10 et Windows 11 x64 sont actuellement pris en charge.

## Compiler depuis les sources

Nécessite Windows et le SDK .NET 8.0.400 ou une version ultérieure de .NET 8.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

Chaque envoi vers <code>main</code> publie une nouvelle release versionnée avec GitHub
Actions. Seule la release la plus récente conserve l’installateur et l’application portable.

## Contribution

Les contributions sont les bienvenues. Lisez [CONTRIBUTING.md](CONTRIBUTING.md).
N’incluez jamais de vrais identifiants ou clés API dans les tickets, journaux,
tests ou commits.

## Avis relatifs aux logiciels tiers

Cette application utilise [Tomlyn](https://github.com/xoofx/Tomlyn), sous
licence BSD à 2 clauses. L’avis complet de droit d’auteur et de licence figure
dans le [README anglais](README.md#third-party-notices).

## Licence

[MIT](LICENSE)
