# Installation und Releases

## Windows 11 x64

[GitHub-Releases](https://github.com/JustUndef/secret-handoff/releases) enthalten nach erfolgreichem Tag-Build den Installer, ein portables ZIP und `SHA256SUMS.txt`. Die Pakete sind derzeit nicht codesigniert. Installer und Paketbau ersetzen keine vollständige MVP-Abnahme; offene Prüfungen stehen in [TESTING.md](TESTING.md).

Der Installer installiert für den aktuellen Benutzer unter `%LOCALAPPDATA%\Programs\Secret Handoff`, legt einen Startmenüeintrag an und bietet die normale Windows-Deinstallation. Er benötigt keine Administratorrechte, ändert keinen PATH, registriert keine Dateizuordnung und startet keinen Auftrag automatisch. Das ZIP kann alternativ in einen eigenen Ordner entpackt werden. Beide Varianten enthalten .NET; eine separate Runtime-Installation ist nicht erforderlich.

Über den Startmenüeintrag öffnet sich die Auftragsauswahl. Für Aufträge aus Codex liegt das zentrale Aufrufskript direkt neben der EXE:

```powershell
& "$env:LOCALAPPDATA\Programs\Secret Handoff\Invoke-Handoff.ps1" -RequestPath 'D:\Projects\Example\task.handoff.json'
```

Das Skript verwendet die benachbarte EXE. Im Quellbaum verwendet es ersatzweise die Entwicklungsdemo unter `artifacts/demo`. Die Aufruferbindung und alle Sicherheitsgrenzen gelten unverändert. Eine angepasste PowerShell-Ausführungsrichtlinie wird weder gesetzt noch vorausgesetzt; wenn Skriptausführung blockiert ist, bleibt der direkte EXE-Aufruf mit den in [ARCHITECTURE.md](ARCHITECTURE.md) beschriebenen Lebenszyklusgrenzen möglich.

## Automatischer Release

Ein Push eines Tags wie `v0.1.0` baut und veröffentlicht das Release automatisch. `v0.1.0-rc.1`, `v0.1.0-alpha.1` und `v0.1.0-beta.1` erzeugen Vorabversionen. Der Workflow führt zuerst die synthetischen Integrationstests aus, baut dann Installer/ZIP, prüft die gepackte EXE und veröffentlicht erst bei Erfolg. Ungültige `v*`-Tags schlagen fehl. Bestehende Releases werden bei Wiederholungen nicht überschrieben. Der Auftrag zur Einrichtung setzt selbst keinen Versions-Tag.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Der Build-Job besitzt nur Leserechte am Repository. Erst der nachgelagerte Veröffentlichungsjob erhält `contents: write`. Er verwendet den automatisch bereitgestellten `GITHUB_TOKEN`; eigene Zugangsdaten sind nicht nötig. Die Integrationstests verwenden ausschließlich synthetische lokale Prozesse. Der Paketbau kontaktiert die offiziellen Werkzeug-/Paketquellen, keine Secret-Provider.

## Lokaler Paketbau

Unter GitHub Actions kann der Workflow auch manuell über **Run workflow** mit einer Testversion gestartet werden. Dieser Weg führt dieselben Build-, Paket- und Installer-Prüfungen aus und lädt die Pakete als Workflow-Artefakt hoch, veröffentlicht aber kein Release.

Voraussetzungen: .NET-SDK laut `global.json` und vorhandenes Inno Setup 6. Keine globale Installation durch die Build-Skripte.

```powershell
.\scripts\Build-Prototype.ps1
.\scripts\Build-Release.ps1 -Version 0.1.0
# RELEASE_READY zeigt den neuen Ausgabeordner an.
.\scripts\Test-Package.ps1 -AppDirectory '<Ausgabe-Basisordner>\app'
```

Optional `-DotnetPath` bzw. `-IsccPath` für explizite Werkzeugpfade verwenden. `Build-Prototype.ps1` unterstützt ebenfalls `-DotnetPath`. Das Release-Skript lädt ausschließlich für den Release-Bau die offiziellen Runtime-Pakete von NuGet; die übrigen Builds bleiben ohne externe Paketquellen. Jeder Paketbau verwendet einen neuen Ordner unter `artifacts/release-builds`, damit keine alten Dateien mitgeliefert werden. Nur das Programm, der zentrale Aufrufer und öffentliche Dokumentation werden eingepackt; keine Demoaufträge, Testprogramme oder Testberichte aus `artifacts`.

Die gebündelte Runtime ist in `release-runtime.json` auf **10.0.14** festgelegt, geprüft am 28. Oktober 2026 anhand der [offiziellen .NET-10-Release-Metadaten](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Dadurch bestimmt nicht das ältere Entwicklungs-SDK die ausgelieferte Runtime. Der Pakettest prüft beide enthaltenen Framework-Versionen. Bei .NET-Sicherheitsupdates diese Datei aktualisieren und neue Pakete bauen und installieren. Die enthaltene Runtime erhält keine unabhängigen Updates durch den Launcher; es gibt keinen Autoupdater. Grundlage: [.NET-Publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/) und [Inno Setup: Benutzerinstallation](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm).
