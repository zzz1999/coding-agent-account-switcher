# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Ein inoffizieller, lokal arbeitender Windows-Kontenwechsler für Codex und Claude Code.

Coding Agent Account Switcher speichert benannte, verschlüsselte Momentaufnahmen der lokalen Authentifizierungsdateien von Codex und Claude Code. Es wird ausschließlich die Authentifizierungs-Momentaufnahme gewechselt; normale Einstellungen, MCP-Konfigurationen, Skills, Plug-ins und der Projektverlauf verbleiben an ihren ursprünglichen Speicherorten.

> [!IMPORTANT]
> Dieses Projekt ist weder mit OpenAI oder Anthropic verbunden noch von diesen unterstützt oder finanziert. Es überträgt keine Abonnements, umgeht keine Anmeldeanforderungen, teilt keine Konten und umgeht keine Richtlinien von Anbietern oder Organisationen.

„Von iOS 18 inspiriert“ beschreibt lediglich die allgemeine visuelle Richtung. Apple steht in keiner Verbindung zu diesem Projekt; Apple-Schriften, -Symbole, -Grafiken oder -Marken werden nicht mitgeliefert.

## Funktionen

- Native Windows-WPF-Oberfläche mit einem von iOS 18 inspirierten Glaskarten-Design.
- Integrierte Oberflächensprachen: Englisch, vereinfachtes Chinesisch, traditionelles Chinesisch, Spanisch, Französisch, Deutsch, Japanisch, Koreanisch, brasilianisches Portugiesisch, Russisch, Arabisch und Hindi.
- App-Einstellungen für die Anzeigesprache und den optionalen Autostart mit Windows für den aktuellen Benutzer.
- Benannte persönliche und geschäftliche Profile für Codex und Claude Code.
- Prozessschutz, der einen Wechsel blockiert, bis zugehörige Anwendungen geschlossen sind.
- Anmeldedaten werden als undurchsichtige Bytefolgen behandelt: Tokens werden nicht analysiert, E-Mail-Adressen nicht extrahiert und Anmeldedaten nicht protokolliert.
- Profil-Momentaufnahmen werden mit Windows DPAPI für den aktuellen Windows-Benutzer verschlüsselt.
- Atomarer Austausch der Anmeldedaten im selben Verzeichnis mit Rollback-Unterstützung.
- Ausdrückliche Bestätigung, bevor eine veränderte aktive Anmeldung die Momentaufnahme des zuletzt gewählten Profils überschreiben kann.
- Sichere Aktion **Momentaufnahme wiederherstellen** für das zuletzt gewählte Profil; die Bestätigung ist an die exakten zu ersetzenden Authentifizierungsbytes gebunden.
- Rein lokaler Betrieb ohne Analyse oder Telemetrie.
- Automatisch aus `main` erstellter fortlaufender `latest`-Build für Windows.

## Unterstützte Authentifizierungsdateien

| Anbieter | Standardmäßig gewechselte Authentifizierungsdatei | Unveränderte Konfiguration |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, Skills, MCP, Sitzungen und sonstiger Zustand |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, Plug-ins, MCP, Projekteinstellungen und Sitzungsverlauf |

Wenn `CODEX_HOME` oder `CLAUDE_CONFIG_DIR` gesetzt ist, folgt die Anwendung dem anbieterspezifischen Authentifizierungsstamm. Weiterhin wird nur `auth.json` beziehungsweise `.credentials.json` gewechselt; benachbarte Konfigurationsdateien bleiben unverändert.

Codex muss dateibasierte Anmeldedatenspeicherung verwenden. Falls Ihre Installation den Anmeldedatenspeicher des Betriebssystems nutzt, fügen Sie in der aktiven Codex-Wurzel (`%USERPROFILE%\.codex` als Standard oder `CODEX_HOME`, falls gesetzt) in `config.toml` Folgendes hinzu:

```toml
cli_auth_credentials_store = "file"
```

Die aktuellen Speicherverträge sind in der offiziellen Dokumentation zur [Codex-Authentifizierung](https://developers.openai.com/codex/auth) und [Claude-Code-Authentifizierung](https://code.claude.com/docs/en/authentication) beschrieben.

## Funktionsweise

1. Melden Sie sich über den offiziellen Anmeldevorgang des Anbieters beim ersten Konto an.
2. Schließen Sie Codex/Claude Code und alle zugehörigen lokalen Clients oder Erweiterungen vollständig.
3. Speichern Sie die aktuelle Anmeldung unter einem selbst gewählten Namen wie `Personal`.
4. Melden Sie sich beim zweiten Konto an und speichern Sie es unter einem anderen Namen wie `Work`.
5. Wählen Sie ein gespeichertes Profil aus. Vor jeder Änderung prüft die App auf zugehörige Prozesse. Läuft einer davon, wird der Wechsel blockiert und keine Anmeldedatei verändert.
6. Beim Wechsel zu einem anderen Profil wird die aktuelle Authentifizierungsdatei nach einer bytegebundenen Bestätigung in das zuletzt gewählte verschlüsselte Profil zurückgespeichert, sodass aktualisierte Tokens erhalten bleiben.

Die App bezeichnet dieses Profil als **Zuletzt gewählt**, nicht als „verifiziert aktuell“. Stimmt die aktive Datei nicht mehr mit der gespeicherten Momentaufnahme überein, wird der Wechsel vor jedem Schreibvorgang angehalten. Bestätigen Sie nur, wenn es sich um eine Aktualisierung desselben Kontos handelt. Haben Sie sich außerhalb der App bei einem anderen Konto angemeldet, wählen Sie zuerst **Als neu speichern** (oder ersetzen Sie ausdrücklich das vorhandene Profil mit dem richtigen Namen).

Die Schaltfläche **Momentaufnahme wiederherstellen** auf der Karte des zuletzt gewählten Profils prüft, ob die aktive Datei noch mit der Momentaufnahme übereinstimmt. Bei einer Abweichung warnt die App, dass die aktuelle ungespeicherte Anmeldung ersetzt wird, und bindet die Zustimmung an genau diese Bytes. Eine andere aktive Anmeldung kann eine frühere Bestätigung nicht wiederverwenden. Während der Wiederherstellung bewahrt ein transaktionsgebundener, DPAPI-verschlüsselter Wiederherstellungsnachweis die vorherigen Bytes auf, bis der Vorgang abgeschlossen oder zurückgesetzt wird.

Die App verspricht keine dauerhafte Anmeldung. Anbieterseitiger Widerruf, Organisationsrichtlinien, SSO, MFA oder Token-Ablauf können weiterhin eine normale Anmeldung im offiziellen Client erfordern.

## Einstellungen

Öffnen Sie im Anwendungsfenster **Einstellungen**, um eine Anzeigesprache zu wählen oder festzulegen, ob die App mit Windows startet. Die Sprache wird lokal für den aktuellen Windows-Benutzer gespeichert und kann jederzeit geändert werden.

**Mit Windows starten** fügt unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` einen Eintrag für diese Anwendung hinzu. Dies gilt nur für den aktuellen Windows-Benutzer und erfordert keine Administratorrechte. Beim Ausschalten wird ausschließlich der Autostarteintrag von Coding Agent Account Switcher entfernt; andere Autostart-Anwendungen bleiben unverändert.

## Sicherheitsmodell

- Verschlüsselte Profildaten werden unter `%LOCALAPPDATA%\CodingAgentAccountSwitcher` gespeichert.
- Jeder normalisierte Speicherort einer Authentifizierungsdatei besitzt einen eigenen, per Hash abgegrenzten Tresor, aktiven Zustand, Wiederherstellungsjournal und Vorgangsmutex. Eine Änderung von `CODEX_HOME` oder `CLAUDE_CONFIG_DIR` erzeugt daher einen unabhängigen Profilsatz, statt das aktive Konto eines anderen Speicherorts zu verwenden.
- DPAPI `CurrentUser` verhindert, dass ein anderes Windows-Konto das Profil direkt entschlüsselt. Es schützt jedoch nicht vor Schadsoftware, die bereits als derselbe Windows-Benutzer ausgeführt wird.
- Abgesehen von der normalen aktiven Authentifizierungsdatei des Anbieters existieren entschlüsselte Momentaufnahme-Bytes während Erfassung oder Wechsel nur kurz im Arbeitsspeicher und im atomaren Austausch im selben Verzeichnis.
- Temporär- und Sicherungsdateien des Authentifizierungsaustauschs verwenden die ID der Wiederherstellungstransaktion. Bei normalem Abschluss werden beide exakten Dateien gelöscht. Nach einer Unterbrechung stellt die Wiederherstellung eine fehlende aktive Datei aus der verschlüsselten Quell-Momentaufnahme oder dem transaktionsgebundenen Wiederherstellungsnachweis wieder her, entfernt anschließend alle exakt dieser Transaktion gehörenden Staging-Dateien und löscht erst danach das Journal.
- Die Anwendung lädt keine Anmeldedaten hoch. Sie dürfen niemals in Protokollen, Issues, Absturzberichten, Testdaten oder Repository-Commits enthalten sein.
- Die Prozesserkennung ist eine defensive Best-Effort-Maßnahme. Ein neu gestarteter Prozess kann mit einem Wechsel konkurrieren; starten Sie Codex oder Claude Code deshalb erst nach Abschluss des Vorgangs.
- Wird ein Geschäftskonto von einer Organisation verwaltet, holen Sie deren Zustimmung ein, bevor Sie eine zusätzliche verschlüsselte lokale Authentifizierungs-Momentaufnahme aufbewahren.

Lesen Sie [SECURITY.md](SECURITY.md) und [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), bevor Sie Code zur Verarbeitung von Anmeldedaten ändern.

## Aus dem Quellcode erstellen

Voraussetzungen:

- Windows 10 oder Windows 11
- .NET SDK 8.0.400 oder ein neueres .NET-8-Feature-Band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

Eigenständigen Windows-x64-Build erstellen:

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## Fortlaufendes latest-Release

`.github/workflows/latest-release.yml` wird bei jedem Push nach `main` ausgeführt:

1. Solution wiederherstellen und testen.
2. Eigenständigen Windows-x64-Build veröffentlichen.
3. ZIP-Archiv und SHA-256-Prüfsumme erstellen.
4. Ausschließlich das vorherige Release und Tag namens `latest` löschen.
5. Ein neues `latest`-Release für den aktuellen Commit erstellen.

Versionierte Releases werden von diesem Workflow nie gelöscht. GitHubs Option **immutable releases** muss für das fortlaufende `latest`-Tag deaktiviert bleiben, und Branch- oder Tag-Regeln müssen dem Workflow erlauben, `latest` zu löschen. Repositorys, die unveränderliche Releases verlangen, sollten den Workflow auf eindeutige Build-Tags umstellen.

Die fortlaufende ausführbare Datei ist derzeit nicht signiert. Windows SmartScreen kann deshalb eine Reputationswarnung anzeigen. Prüfen Sie vor der Ausführung den Quellcode und die veröffentlichte SHA-256-Prüfsumme.

## Mitwirken

Beiträge sind willkommen. Lesen Sie [CONTRIBUTING.md](CONTRIBUTING.md). Tests müssen temporäre, fingierte Anmeldedateien verwenden und dürfen niemals auf die echten Codex- oder Claude-Code-Authentifizierungsdateien eines Entwicklers zugreifen.

## Lizenz

[MIT](LICENSE)
