# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher-Symbol">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Eine einfache Windows-App zum Wechseln zwischen gespeicherten Codex-, Claude
Code- und OpenCode-Konten oder API-Dienst-Konfigurationen.

Sie ändert nur die im Profil gespeicherten Kontodaten und unterstützten
API-Einstellungen. MCP-Server, Skills, Plugins, Projekteinstellungen und Verlauf
bleiben an ihrem bisherigen Ort.

> [!IMPORTANT]
> Dies ist ein inoffizielles Community-Projekt. Es ist nicht mit OpenAI,
> Anthropic, Apple oder dem OpenCode-Projekt verbunden. Es überträgt keine
> Abonnements, umgeht keine Anmeldung und setzt keine Organisationsrichtlinien außer Kraft.

## Download

Die aktuelle Version finden Sie in der
[neuesten Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Installationsprogramm:** <code>coding-agent-account-switcher-setup-win-x64.exe</code>
- **Portable App:** <code>coding-agent-account-switcher-portable-win-x64.exe</code>
- **Prüfsummen:** Für jede ausführbare Datei gibt es eine passende SHA-256-Datei

Die Installation gilt nur für den aktuellen Benutzer, benötigt keine
Administratorrechte und aktiviert **Mit Windows starten** nicht ohne Ihre
Zustimmung. Die aktuellen Builds sind nicht signiert; Windows SmartScreen kann
daher eine Warnung anzeigen.

## Hauptfunktionen

- Speichern Sie persönliche, geschäftliche und API-Dienstprofile mit klaren Namen.
- Wechseln Sie Codex-, Claude Code- und OpenCode-Konten mit wenigen Klicks.
- Behalten Sie unterstützte API-Adressen, Schlüssel und Anbieter-Routing beim richtigen Profil.
- Benennen Sie ein Profil per Doppelklick um oder löschen Sie eine lokale Momentaufnahme.
- Sehen Sie nach dem Wechsel eine klare Bestätigung des aktiven Profils.
- Verhindern Sie den Wechsel, solange zugehörige Apps geöffnet sind.
- Verwenden Sie eine von 12 integrierten Sprachen.
- Behalten Sie helles/dunkles Design und Sprache zwischen den Starts.
- Wenn Sie die App in derselben Windows-Sitzung erneut öffnen, wird das vorhandene
  Fenster wiederhergestellt und nach vorn geholt, statt ein zweites zu öffnen.
- Starten Sie optional mit Windows und suchen Sie manuell nach Updates.

Alle Daten bleiben auf Ihrem Computer. Die App enthält keine Analyse oder Telemetrie.

## Unterstützte Apps

| App | Was gewechselt wird | Was unverändert bleibt |
| --- | --- | --- |
| Codex | Anmeldung und Verbindungseinstellungen des gewählten API-Anbieters | Modelle, Prüf-/Reasoning-Optionen, Features, MCP, Skills, Sitzungen, Verlauf und andere Einstellungen |
| Claude Code | Anmeldung und unterstützte API-Adress-/Schlüsseleinstellungen | Plugins, MCP, Projekte, Verlauf und andere Einstellungen |
| OpenCode | Gespeicherte Anmeldung und unterstützte Anbieter-/Modelleinstellungen | Projektkonfiguration und andere Einstellungen |

Nur die vom Projekt unterstützten Kontofelder werden geändert. Die genaue Liste
finden Sie in der [Architektur](docs/ARCHITECTURE.md).

## Schnellstart

1. Melden Sie sich normal an oder richten Sie den gewünschten API-Dienst ein.
2. Öffnen Sie die App und wählen Sie den passenden Anbieter.
3. Wählen Sie **Aktuelles Konto speichern** und vergeben Sie einen Namen wie <code>Personal</code>.
4. Melden Sie sich beim zweiten Konto an oder richten Sie einen anderen API-Dienst ein.
5. Speichern Sie es beispielsweise als <code>Work</code>.
6. Schließen Sie vor jedem Wechsel die zugehörige App vollständig und wählen Sie dann ein gespeichertes Profil aus.

Ein Konto kann gespeichert werden, während die zugehörige App geöffnet ist. Vor
einem Wechsel prüft die App laufende Prozesse. Ist noch etwas geöffnet, fordert
sie zum Schließen auf und ändert keine Datei.

Hat sich das aktuelle Konto seit dem Speichern geändert, bittet die App um
Bestätigung. Handelt es sich tatsächlich um ein anderes Konto, speichern Sie es
zuerst als neues Profil.

## Profile und Einstellungen

- **Umbenennen:** Doppelklicken Sie auf den Profilnamen.
- **Löschen:** Verwenden Sie den Papierkorb. Nur die lokale verschlüsselte
  Momentaufnahme wird entfernt; das Konto wird nicht gelöscht und nicht abgemeldet.
- **Zuletzt ausgewählt:** zeigt das zuletzt durch diese App aktivierte Profil.
- **Beschädigte Momentaufnahme:** Eine fehlende oder unlesbare Momentaufnahme wird
  ausgeblendet; gültige Profile bleiben verfügbar.
- **Design und Sprache:** werden für den aktuellen Windows-Benutzer gespeichert.
- **Mit Windows starten:** optional, nur für den aktuellen Benutzer und ohne Administratorrechte.
- **Nach Updates suchen:** nur nach einem Klick, ohne automatischen Download oder Installation.

## Datenschutz und Sicherheit

- Profile werden mit Windows DPAPI für den aktuellen Benutzer verschlüsselt.
- Anmeldedaten und API-Schlüssel werden nicht angezeigt oder protokolliert.
- Profile liegen unter <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>.
- Geschützter Dateiaustausch und Wiederherstellung verringern das Risiko unvollständiger Änderungen.
- Die App sendet keine Anmeldedaten, Profilnamen, Gerätekennungen oder Telemetrie.
- Geschäftskonten können Organisationsrichtlinien unterliegen; holen Sie vor
  einer zusätzlichen lokalen Anmeldekopie eine Genehmigung ein.

Lesen Sie [SECURITY.md](SECURITY.md), bevor Sie ein Sicherheitsproblem melden.

## Einschränkungen

- Abmeldung beim Anbieter, Token-Ablauf, SSO, MFA oder Organisationsrichtlinien
  können eine normale Anmeldung erforderlich machen.
- Bestehende Codex-Unterhaltungen bleiben an den Anbieter gebunden, mit dem sie
  erstellt wurden. Starten Sie nach einem Anbieterwechsel eine neue Unterhaltung
  oder wechseln Sie zum ursprünglichen Anbieter zurück, um sie fortzusetzen.
- Die App überträgt keine Abonnements und garantiert keine dauerhafte Anmeldung.
- Derzeit werden nur Windows 10 und Windows 11 x64 unterstützt.

## Aus dem Quellcode erstellen

Erfordert Windows und .NET SDK 8.0.400 oder ein neueres .NET-8-Feature-Band.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

Jeder Push auf <code>main</code> veröffentlicht über GitHub Actions ein neues,
versioniertes Release. Nur das neueste Release behält Installer und portable App.

## Mitwirken

Beiträge sind willkommen. Lesen Sie [CONTRIBUTING.md](CONTRIBUTING.md).
Fügen Sie niemals echte Anmeldedaten oder API-Schlüssel in Issues, Protokolle,
Tests oder Commits ein.

## Lizenz

[MIT](LICENSE)
