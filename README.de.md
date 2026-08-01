# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Ein inoffizieller, lokal arbeitender Windows-Wechsler für Konten und API-Dienste von Codex, Claude Code und OpenCode.

Coding Agent Account Switcher speichert verschlüsselte Momentaufnahmen lokaler Anmeldedaten zusammen mit nur den benötigten API-Anbieterfeldern. Alle anderen Einstellungen, MCP, Skills, Plug-ins und Projektverläufe bleiben an ihren ursprünglichen Orten.

> [!IMPORTANT]
> Dieses Projekt ist weder mit OpenAI oder Anthropic verbunden noch von diesen unterstützt oder finanziert. Es überträgt keine Abonnements, umgeht keine Anmeldeanforderungen, teilt keine Konten und umgeht keine Richtlinien von Anbietern oder Organisationen.

„Von iOS 18 inspiriert“ beschreibt lediglich die allgemeine visuelle Richtung. Apple steht in keiner Verbindung zu diesem Projekt; Apple-Schriften, -Symbole, -Grafiken oder -Marken werden nicht mitgeliefert.

## Download

Laden Sie den aktuellen Build aus dem [latest-Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) herunter:

- **Empfohlenes Installationsprogramm:** `coding-agent-account-switcher-setup-win-x64.exe` installiert die Anwendung ohne Administratorrechte für den aktuellen Windows-Benutzer und erstellt Einträge im Startmenü und zur Deinstallation.
- **Portable ausführbare Datei:** `coding-agent-account-switcher-portable-win-x64.exe` direkt herunterladen und ohne Installation ausführen.
- Das Release enthält für jede ausführbare Datei eine passende SHA-256-Prüfsummendatei.

Das Installationsprogramm aktiviert **Mit Windows starten** nicht automatisch und verändert keine Authentifizierungs- oder Konfigurationsdateien von Codex, Claude Code oder OpenCode. Bei der Deinstallation bleiben verschlüsselte Kontoschnappschüsse und Anwendungseinstellungen erhalten, sodass sie nach einer Neuinstallation weiterhin verfügbar sind. Das Installationsprogramm und die portable ausführbare Datei sind derzeit nicht signiert; Windows SmartScreen kann deshalb eine Reputationswarnung anzeigen.

## Funktionen

- Native Windows-WPF-Oberfläche mit einem von iOS 18 inspirierten Glaskarten-Design.
- Integrierte Oberflächensprachen: Englisch, vereinfachtes Chinesisch, traditionelles Chinesisch, Spanisch, Französisch, Deutsch, Japanisch, Koreanisch, brasilianisches Portugiesisch, Russisch, Arabisch und Hindi.
- App-Einstellungen für die Anzeigesprache und den optionalen Autostart mit Windows für den aktuellen Benutzer.
- Benannte persönliche, geschäftliche und API-Dienstprofile für Codex, Claude Code und OpenCode.
- Prozessschutz, der einen Wechsel blockiert, bis zugehörige Anwendungen geschlossen sind.
- Authentifizierungsdateien bleiben undurchsichtige Bytefolgen. Nur die unten aufgeführten verwalteten Felder werden gelesen und zusammengeführt; Geheimnisse werden nie angezeigt oder protokolliert.
- Profil-Momentaufnahmen werden mit Windows DPAPI für den aktuellen Windows-Benutzer verschlüsselt.
- Atomarer Austausch verwalteter Dateien im selben Verzeichnis mit Rollback-Unterstützung.
- Ausdrückliche Bestätigung, bevor eine veränderte aktive Anmeldung die Momentaufnahme des zuletzt gewählten Profils überschreiben kann.
- Sichere Aktion **Momentaufnahme wiederherstellen** für das zuletzt gewählte Profil; die Bestätigung ist an die exakten zu ersetzenden Authentifizierungsbytes gebunden.
- Rein lokaler Betrieb ohne Analyse oder Telemetrie.
- Automatisch aus `main` erstellter fortlaufender `latest`-Build für Windows.

## Unterstützte Konto- und API-Konfiguration

| Anbieter | Verwaltete Dateien | Selektiv verwaltete Konfiguration |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` und `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, die ausgewählte aktive `model_providers`-Tabelle und `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` und `settings.json` | Nur `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC` und das Kompatibilitätsfeld `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | Optionales `%USERPROFILE%\.local\share\opencode\auth.json`; globale Ebenen `config.json`, `opencode.json`, `opencode.jsonc`; danach `OPENCODE_CONFIG`, falls gesetzt | Undurchsichtige `/connect`-Daten und das effektive API-Profil aus `provider`, `model`, `small_model` |

Die Anwendung führt diese Felder zusammen, statt die gesamte Datei zu ersetzen. Bei Codex bleiben `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`, `cli_auth_credentials_store`, MCP, Skills, Sitzungen und alle anderen Werte erhalten. Bei Claude Code bleiben alle anderen `env`-Einträge, `.claude.json`, Plug-ins, MCP, Projekteinstellungen und der Verlauf erhalten. Bei OpenCode bleiben nicht betroffene semantische Werte in jeder beteiligten globalen oder benutzerdefinierten Datei erhalten.

Das standardmäßige Konfigurationsstammverzeichnis von OpenCode ist `%USERPROFILE%\.config`, das standardmäßige Datenstammverzeichnis `%USERPROFILE%\.local\share`. Ein absoluter Wert für `XDG_CONFIG_HOME` oder `XDG_DATA_HOME` ersetzt das jeweilige Standardverzeichnis; ein leerer Wert tut dies nicht. Alle nicht leeren OpenCode-Pfadüberschreibungen (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG` und `OPENCODE_CONFIG_DIR`) müssen absolute Pfade sein; ein relativer Wert blockiert Erfassen und Wechseln vor jedem Schreibvorgang. Die globale Konfiguration lädt unter dem Konfigurationsstammverzeichnis der Reihe nach `opencode\config.json`, `opencode.json` und `opencode.jsonc`; spätere Ebenen überschreiben frühere. `OPENCODE_CONFIG` wird zuletzt geladen. Die Anwendung erfasst das wirksame Ergebnis aus `provider`, `model` und `small_model` als ein API-Profil. `/connect`-Anmeldedaten werden unter dem Datenstammverzeichnis aus `opencode\auth.json` gelesen.

OpenCode unterstützt sowohl `/connect`-Anmeldedaten in `auth.json` als auch API-Schlüssel im `provider`-Objekt; die Momentaufnahme enthält den vorhandenen Stil oder beide.

Beim Anwenden werden `provider`, `model` und `small_model` aus den anderen globalen Ebenen entfernt, damit kein veralteter Endpunkt überschreibt. Mit `OPENCODE_CONFIG` wird das Ziel dort geschrieben und alle drei globalen Ebenen werden bereinigt. Ohne diese Variable werden nicht leere Werte in globales `opencode.jsonc` kanonisiert und aus `config.json` sowie `opencode.json` entfernt, auch wenn zuvor nur JSON oder die alte Datei existierte. Eine reine Authentifizierungs- oder leere verwaltete Momentaufnahme bleibt gültig: Sie bereinigt vorhandene Werte, erstellt aber keine leere `opencode.jsonc`.

Die selektive Zusammenführung erhält nicht betroffene semantische Werte, garantiert bei einer erneuten JSON- oder TOML-Serialisierung aber keine bytegenaue Formatierung oder Kommentare.

Vor dem Erfassen oder Wechseln von OpenCode prüft die Anwendung bekannte Umgebungsüberschreibungen mit höherer Priorität ausschließlich lesend. Ein nicht leerer Wert von `OPENCODE_AUTH_CONTENT` blockiert den Vorgang. `OPENCODE_CONFIG_CONTENT` ist nur zulässig, wenn es gültiges JSON oder JSONC ist und keine der verwalteten Schlüssel `provider`, `model` oder `small_model` auf oberster Ebene enthält; ungültiger Inhalt oder einer dieser Schlüssel blockiert den Vorgang. Wenn `OPENCODE_CONFIG_DIR` gesetzt ist, werden dessen `opencode.json` und `opencode.jsonc` geprüft; eine nicht lesbare oder ungültige Datei oder ein verwalteter Schlüssel in einer dieser Dateien blockiert den Vorgang. Inline- oder Verzeichniskonfiguration mit ausschließlich nicht betroffenen Schlüsseln ist zulässig. Die Anwendung ändert keine dieser von der Umgebung bereitgestellten Quellen.

`CODEX_HOME` und `CLAUDE_CONFIG_DIR` ändern die jeweiligen Wurzeln. Projektbezogene OpenCode-Konfiguration, zentral verwaltete Quellen und anbieterspezifische Umgebungsvariablen bleiben unverwaltet und können das ausgewählte globale Profil nach einem Wechsel weiterhin überschreiben; die Anwendung sucht oder ändert sie nicht. Der Prozessschutz prüft `opencode` und `opencode-cli`. `disable_response_storage`, `features.responses_websockets_v2` und `CLAUDE_CODE_ATTRIBUTION_HEADER` sind Kompatibilitätsfelder, ohne zu behaupten, dass jede aktuelle Version sie dokumentiert.

Codex muss dateibasierte Anmeldedatenspeicherung verwenden. Falls Ihre Installation den Anmeldedatenspeicher des Betriebssystems nutzt, fügen Sie in der aktiven Codex-Wurzel (`%USERPROFILE%\.codex` als Standard oder `CODEX_HOME`, falls gesetzt) in `config.toml` Folgendes hinzu:

```toml
cli_auth_credentials_store = "file"
```

Die aktuellen Speicherverträge sind in der offiziellen Dokumentation zur [Codex-Authentifizierung](https://developers.openai.com/codex/auth) und [Claude-Code-Authentifizierung](https://code.claude.com/docs/en/authentication) beschrieben.

## Funktionsweise

1. Melden Sie sich über den offiziellen Ablauf an oder konfigurieren Sie einen unterstützten API-Dienst in den normalen Anbieterdateien.
2. Schließen Sie Codex, Claude Code, OpenCode und alle zugehörigen Clients oder Erweiterungen vollständig.
3. Speichern Sie Konto und verwaltete API-Einstellungen unter einem Namen wie `Personal`.
4. Melden Sie sich bei einem anderen Konto an oder konfigurieren Sie einen anderen API-Dienst und speichern Sie ihn als `Work`.
5. Wählen Sie ein Profil. Läuft ein zugehöriger Prozess, wird der Wechsel blockiert und keine verwaltete Datei geändert.
6. Nach einer inhaltsgebundenen Bestätigung wird die aktuelle verwaltete Momentaufnahme im zuletzt gewählten verschlüsselten Profil gespeichert, damit aktualisierte Tokens und beabsichtigte API-Änderungen erhalten bleiben. Die exakte vorherige Momentaufnahme wird zusätzlich in einem transaktionsgebundenen DPAPI-Wiederherstellungsblob gesichert.

Die App bezeichnet dieses Profil als **Zuletzt gewählt**, nicht als „verifiziert aktuell“. Stimmt die aktive Datei nicht mehr mit der gespeicherten Momentaufnahme überein, wird der Wechsel vor jedem Schreibvorgang angehalten. Bestätigen Sie nur, wenn es sich um eine Aktualisierung desselben Kontos handelt. Haben Sie sich außerhalb der App bei einem anderen Konto angemeldet, wählen Sie zuerst **Als neu speichern** (oder ersetzen Sie ausdrücklich das vorhandene Profil mit dem richtigen Namen).

Die Schaltfläche **Momentaufnahme wiederherstellen** auf der Karte des zuletzt gewählten Profils prüft, ob die aktive Datei noch mit der Momentaufnahme übereinstimmt. Bei einer Abweichung warnt die App, dass die aktuelle ungespeicherte Anmeldung ersetzt wird, und bindet die Zustimmung an genau diese Bytes. Eine andere aktive Anmeldung kann eine frühere Bestätigung nicht wiederverwenden. Während der Wiederherstellung bewahrt ein transaktionsgebundener, DPAPI-verschlüsselter Wiederherstellungsnachweis die vorherigen Bytes auf, bis der Vorgang abgeschlossen oder zurückgesetzt wird.

Jede zusammengesetzte oder mehrteilige Dateitransaktion speichert die exakte aktuelle verwaltete Momentaufnahme vor dem Journal im verschlüsselten Wiederherstellungsblob, auch wenn sie der gespeicherten Quelle entspricht. So lassen sich Teilschreibvorgänge exakt zurücksetzen und ältere reine Anmeldedatenprofile sicher aktualisieren. Nach einer Unterbrechung bevorzugt die Wiederherstellung diese Momentaufnahme vor dem Wechsel und synchronisiert das Quellprofil, wenn sie daraus wiederherstellt. Ältere Journale ohne Blob greifen weiterhin auf die gespeicherte Quelle zurück. Stimmt der aktive Zustand weder mit Quelle noch Ziel überein, bleiben Journal und Blob für die manuelle Wiederherstellung erhalten.

Mehrdatei-Commits sind ausfallsicher geschlossen: Zuerst wird die separate Authentifizierung entfernt, dann die Konfiguration atomar zusammengeführt und zuletzt die Zielauthentifizierung installiert. Eine Unterbrechung kann Authentifizierung vorübergehend fehlen lassen, aber nie Anmeldedaten mit dem Endpunkt des anderen Profils verbinden; die Wiederherstellung schließt sicher ab oder setzt zurück.

Ältere rohe Codex- und Claude-Code-Profile werden als Anmeldedaten plus leere verwaltete API-Konfiguration interpretiert. Beim Aktivieren werden verwaltete API-Routen- und Modellfelder gelöscht, damit kein Drittanbieter-Endpunkt des vorherigen Profils weiterverwendet wird. Konfigurieren Sie danach das gewünschte Modell/API und erfassen Sie das Profil erneut. Ein aktives altes Quellprofil wird beim Verlassen auf das zusammengesetzte Format aktualisiert.

Die App verspricht keine dauerhafte Anmeldung. Anbieterseitiger Widerruf, Organisationsrichtlinien, SSO, MFA oder Token-Ablauf können weiterhin eine normale Anmeldung im offiziellen Client erfordern.

Umbenennen und Löschen eines Profils wirken sich nur auf den lokalen verschlüsselten Momentaufnahme-Tresor aus. Beim Umbenennen ändern sich ausschließlich gespeicherte Bezeichnung und Metadaten, nicht der Inhalt der Momentaufnahme; beim Löschen wird nur die ausgewählte lokale verschlüsselte Momentaufnahme entfernt. Wird das zuletzt gewählte Profil gelöscht, löscht die App außerdem dessen aktive Zuordnung, meldet das Konto jedoch nicht ab und ändert keine aktiven Authentifizierungs- oder Konfigurationsdateien des Anbieters. Speichern Sie vor dem nächsten Wechsel das aktuelle Konto. Beide Aktionen werden abgewiesen, solange eine unterbrochene Wechseltransaktion auf Wiederherstellung wartet.

## Einstellungen

Öffnen Sie im Anwendungsfenster **Einstellungen**, um eine Anzeigesprache zu wählen oder festzulegen, ob die App mit Windows startet. Die Sprache wird lokal für den aktuellen Windows-Benutzer gespeichert und kann jederzeit geändert werden.

**Mit Windows starten** fügt unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` einen Eintrag für diese Anwendung hinzu. Dies gilt nur für den aktuellen Windows-Benutzer und erfordert keine Administratorrechte. Beim Ausschalten wird ausschließlich der Autostarteintrag von Coding Agent Account Switcher entfernt; andere Autostart-Anwendungen bleiben unverändert.

**Nach Updates suchen** wird ausschließlich vom Benutzer ausgelöst. Erst nach einem Klick sendet die App eine einzelne anonyme HTTPS-`GET`-Anfrage an die offizielle GitHub-API dieses Repositorys. Beim Start, im Hintergrund oder regelmäßig wird nicht geprüft; die Anfrage überträgt keine Anmeldedaten, Einstellungen, Profilnamen, Gerätekennungen oder Telemetrie. Die App vergleicht nur Release-Metadaten und lädt niemals automatisch ein Installationsprogramm oder einen portablen Build herunter oder führt ihn aus.

## Sicherheitsmodell

- Verschlüsselte Profildaten werden unter `%LOCALAPPDATA%\CodingAgentAccountSwitcher` gespeichert.
- Jeder normalisierte Satz verwalteter Dateien besitzt eigenen Tresor, Zustand, Journal und Mutex. Eine Änderung von `CODEX_HOME`, `CLAUDE_CONFIG_DIR` oder `OPENCODE_CONFIG` erzeugt einen anderen Profilsatz.
- DPAPI `CurrentUser` verhindert, dass ein anderes Windows-Konto das Profil direkt entschlüsselt. Es schützt jedoch nicht vor Schadsoftware, die bereits als derselbe Windows-Benutzer ausgeführt wird.
- Abgesehen von der normalen aktiven Authentifizierungsdatei des Anbieters existieren entschlüsselte Momentaufnahme-Bytes während Erfassung oder Wechsel nur kurz im Arbeitsspeicher und im atomaren Austausch im selben Verzeichnis.
- Temporär- und Sicherungsdateien des Authentifizierungsaustauschs verwenden die ID der Wiederherstellungstransaktion. Bei normalem Abschluss werden beide exakten Dateien gelöscht. Nach einer Unterbrechung stellt die Wiederherstellung eine fehlende aktive Datei aus der verschlüsselten Quell-Momentaufnahme oder dem transaktionsgebundenen Wiederherstellungsnachweis wieder her, entfernt anschließend alle exakt dieser Transaktion gehörenden Staging-Dateien und löscht erst danach das Journal. Der verschlüsselte Wiederherstellungsblob wird erst nach dem Journal und nach bestem Bemühen gelöscht.
- Die Anwendung lädt keine Anmeldedaten hoch. Sie dürfen niemals in Protokollen, Issues, Absturzberichten, Testdaten oder Repository-Commits enthalten sein.
- Die Prozesserkennung ist eine defensive Best-Effort-Maßnahme. Starten Sie Codex, Claude Code oder OpenCode erst nach Abschluss des Vorgangs.
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
2. Eigenständigen portablen Windows-x64-Build veröffentlichen.
3. Das benutzerspezifische Windows-x64-Installationsprogramm erstellen.
4. Die portable ausführbare Datei und SHA-256-Prüfsummen für beide ausführbaren Dateien vorbereiten.
5. Ausschließlich das vorherige Release und Tag namens `latest` löschen.
6. Ein neues `latest`-Release mit Installationsprogramm, portabler ausführbarer Datei und Prüfsummen für den aktuellen Commit veröffentlichen. Ein einziger Workflow-Wert `APP_VERSION` versioniert die ausführbare Datei und schreibt diese exakte maschinenlesbare Markierung in die Hinweise: `<!-- coding-agent-account-switcher-version: 0.1.N -->`.

Da das Tag `latest` fortlaufend ersetzt wird, liest die App diese Markierung nur bei einer ausdrücklich vom Benutzer gestarteten Updateprüfung aus der Antwort der offiziellen GitHub-Releases-API. Sie prüft nicht im Hintergrund und lädt keine Release-Datei automatisch herunter oder führt sie aus.

Versionierte Releases werden von diesem Workflow nie gelöscht. GitHubs Option **immutable releases** muss für das fortlaufende `latest`-Tag deaktiviert bleiben, und Branch- oder Tag-Regeln müssen dem Workflow erlauben, `latest` zu löschen. Repositorys, die unveränderliche Releases verlangen, sollten den Workflow auf eindeutige Build-Tags umstellen.

Das fortlaufende Installationsprogramm und die portable ausführbare Datei sind derzeit nicht signiert. Windows SmartScreen kann deshalb eine Reputationswarnung anzeigen. Prüfen Sie vor der Ausführung den Quellcode und die jeweils veröffentlichte SHA-256-Prüfsumme.

## Mitwirken

Beiträge sind willkommen. Lesen Sie [CONTRIBUTING.md](CONTRIBUTING.md). Tests müssen temporäre synthetische Anmelde- und Konfigurationsdateien verwenden und dürfen nie auf echte Codex-, Claude-Code- oder OpenCode-Dateien zugreifen.

## Lizenz

[MIT](LICENSE)
