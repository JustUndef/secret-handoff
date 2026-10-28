# Prototyp und Prüfbericht

Stand: 28. Oktober 2026. Durchgängiger Prototyp mit Aufruferüberwachung und vier Review-Nachbesserungen; **MVP-Abnahme noch nicht vollständig**. Sämtliche Werte und Zielprogramme in diesen Prüfungen sind synthetisch/lokal. Keine echten Secrets oder Dienste verwendet.

## Reproduzieren

### Ergänzung: Installer und Release-Bau

Am 28. Oktober zusätzlich ausgeführt: erneuter Entwicklungsbuild mit 0 Warnungen/Fehlern und alle 47 synthetischen Testgruppen erfolgreich außerhalb der Sandbox. Ein selbstenthaltendes `win-x64`-Paket in Version `0.1.0-rc.1` wurde lokal mit den offiziellen .NET-Runtime-Paketen gebaut; Inno Setup erzeugte den Benutzer-Installer. `Test-Package.ps1` meldete `PACKAGE_SMOKE_PASSED`. `Test-Installer.ps1` installierte in einen neuen Projekt-Testordner, prüfte die installierte EXE, installierte erneut und deinstallierte erfolgreich (`INSTALLER_ROUNDTRIP_PASSED`). Der Test änderte keine Ausführungsrichtlinie und verwendete keine echten Secrets. Ein erster Sandbox-Lauf hatte wegen blockierter Metadatenabfragen fehlgeschlagen; die Pfadpolicy wurde nicht gelockert.

Das prüft Paketinhalt, Start/Fehlerstatus der gepackten EXE, stille Installation, erneute Installation derselben Version und Deinstallation. Ein Upgrade von einer älteren veröffentlichten Version, die manuelle Installer-Oberfläche und ein System ohne vorhandene .NET-Installation sind noch nicht praktisch geprüft. SHA-256-Prüfsummen belegen Dateiintegrität bei bekanntem Vergleichswert, keine Herausgeberidentität; die Pakete sind nicht codesigniert. Build- und Release-Anleitung: [RELEASING.md](RELEASING.md).

Abschließend wurde die gebündelte Runtime unabhängig vom Entwicklungs-SDK auf **10.0.14** festgelegt. Lokaler Neubau, Prüfung beider Framework-Versionen, Installation/erneute Installation/Deinstallation und Nachprüfung der SHA-256-Datei bestanden. Der [abschließende GitHub-Actions-Lauf](https://github.com/JustUndef/secret-handoff/actions/runs/40711889604) für Commit `43fbe42` bestand auf `windows-2025`: synthetische Integrationstests, selbstenthaltender Paketbau, Runtime-/EXE-Prüfung, Installer-Rundlauf und Artefakt-Upload. Der Veröffentlichungsjob wurde bei diesem manuellen Test wie vorgesehen übersprungen. Ein tatsächlicher Tag-Push mit Veröffentlichung eines GitHub-Releases wurde nicht ausgeführt; dieser letzte Veröffentlichungsweg bleibt bis zum ersten gewollten Versions-Tag praktisch ungetestet.

In PowerShell im Projektordner:

```powershell
.\scripts\Build-Prototype.ps1
```

Das Skript wählt `C:\Users\<Benutzer>\.dotnet\dotnet.exe` mit SDK 10.0.203, baut die Lösung, erzeugt die Demo und führt den Test-Runner aus. Keine externen NuGet-Pakete; die Paketquellen sind für dieses Projekt geleert. SDK-Telemetrie, Entwicklungszertifikat-Erzeugung und globale Tool-PATH-Ergänzung sind im Build-Skript deaktiviert; dessen temporäre Prozessvariablen werden danach wiederhergestellt. Wegen Beschränkungen dieser Ausführungsumgebung werden Workload-Auflösung und der persistente Compiler-Server beim Build nicht verwendet. Keine Systemrichtlinien ändern.

Ausgeführt unter Windows 11 x64 (Build 26300), SDK 10.0.203. Der erfolgreiche letzte Build am 28. Oktober meldete **0 Warnungen, 0 Fehler**. Der anschließend außerhalb der Codex-Sandbox ausgeführte Test-Runner meldete **47 PASS und `ALL_TESTS_PASSED`**. Wertfreies Ergebnisprotokoll der Review-Nachbesserungen: `artifacts/review-build-and-test.txt`. Das ältere `artifacts/build-and-test.txt` dokumentiert den vorherigen Stand mit 32 Testgruppen.

Der erste integrierte Testlauf innerhalb der Sandbox scheiterte: `QueryDosDeviceW` wurde mit `ERROR_ACCESS_DENIED` blockiert und die neue Pfadpolicy wies die Aufträge ab. Eine gezielte lokale Prüfung außerhalb der Sandbox bestätigte ein direkt abgebildetes lokales Laufwerk. Danach wurde derselbe gebaute Test-Runner mit dem aktuellen PowerShell-Pfad in `SHANDOFF_TEST_POWERSHELL` vollständig außerhalb der Sandbox ausgeführt; die temporäre Prozessvariable wurde anschließend wiederhergestellt. Die Policy wurde dafür nicht gelockert. Reproduktion benötigt einen Ausführungskontext, der die lokalen Windows-Metadatenabfragen zulässt; es wurden keine Systemrichtlinien geändert.

Die Demo unter `artifacts/demo` ist derzeit frameworkabhängig, keine selbstenthaltende Veröffentlichung. Die erzeugte JSON-Datei enthält absolute Metadatenpfade und keine Secret-Werte. Das Beispiel nach einem Umzug des Ordners neu erzeugen.

## Ausgeführte Tests

| Testgruppe | Beleg |
| --- | --- |
| `request-contract` | JSON-Snapshot, doppelte/unbekannte Schlüssel, ungültiges Format und Übergröße, mehrere gefährliche Namen, Zeichen- und Wertregeln |
| `local-path-native-valid` | Echte lokale Probe- und Auftragsdatei, kanonische Pfade und Verzeichnis-Endslash akzeptiert |
| `local-path-reject-drive-before-attributes` | Simulierte Remote-/Unknown-/fehlende Laufwerke vor jedem Attribut- oder Inhaltszugriff abgewiesen |
| `local-path-syntax-before-probe` | UNC, Gerätepfade/-namen, Datenströme und mehrdeutige Namen vor Windows-Abfragen abgewiesen; Slash- und Punktsegment-Normalisierung |
| `local-path-components-and-types` | Simulierte Reparse Points an Wurzel, Zwischenkomponente und Datei stoppen die Prüfung vor weiteren Komponenten; Dateitypen geprüft |
| `local-path-loader-checks-all-paths` | Auftragsdatei, Ziel und Arbeitsverzeichnis unterliegen der Policy; keine Attributabfrage des simulierten Netzlaufwerks |
| `local-path-post-preview-revalidation` | Simulierter Laufwerkswechsel nach Vorschau führt ohne Start zu Kategorie 5; Werte geleert, Auftragsdatei nicht neu gelesen |
| `local-path-immediate-prestart-revalidation` | Simulierter Laufwerks-/Reparse-Wechsel zwischen erster Runnerprüfung und CreateProcess verhindert die Prozesserzeugung |
| `environment-and-arguments` | Zwei synthetische Werte exakt einschließlich Unicode/Rand-Leerzeichen; Zielumgebung enthält genau die drei Basisnamen und zwei angeforderte Namen; .NET-argv-Roundtrip mit leeren Argumenten, Quotes, Backslashes und Shell-Sonderzeichen; Arbeitsverzeichnis |
| `pre-cancel-no-start` | Bereits abgebrochener Auftrag erzeugt keinen Startmarker; Wert-Dictionary wird geleert |
| `cancel-during-suspended-start` | Abbruch nach Erzeugung des suspendierten Prozesses, vor Ausführung: bereinigt, kein Zielcode/Marker |
| `invalid-values-no-start` | Leer, Zeilenumbruch, NUL und Überlänge abgewiesen |
| `target-failure` | Zielcode 37 wird lokal als Zahl erfasst und zu Launcher-Kategorie 7 abgebildet |
| `stdout-stderr-flood-discarded` | Echter Runner in separatem Testhost; synthetische Werte, 32 MiB Ausgabe und Binärbytes erzeugen außen exakt null Bytes in beiden Kanälen; feste Drainpuffer und Zeitlimits |
| `stdout-stderr-detector-negative-control` | Derselbe Detektor erkennt absichtlich erzeugte vier synthetische Binärbytes je Kanal; keine Rohdaten in Testausgabe oder Dateien |
| `normal-end-kills-descendants` | Zielende beendet verbliebenen Enkel; bekannte Test-PIDs danach beendet |
| `cancel-kills-tree` | Laufender Auftrag samt Kind/Enkel nach Cancellation beendet |
| `timeout-kills-tree` | Verkürztes Testlimit von 2 Sekunden beendet den gesamten betreuten Baum |
| `host-crash-kills-tree` | Abruptes Töten des Runner-Hosts beendet Ziel und Enkel durch Job-Handle-Schließung |
| `host-crash-before-resume` | Abruptes Töten vor Resume hinterlässt keinen suspendierten Zielprozess |
| `cleanup-failure-status` | Synthetische Statusinjektion: Kategorie 9 überschreibt das Ergebnis; tatsächliche Kernel-Beendigungsfehler wurden nicht provoziert |
| `start-failure` | Ungültiges EXE und fehlender Zielpfad liefern feste Kategorien |
| `input-paste-and-masking` | Separate Test-Zwischenablage: beide Einfügepfade, Auswahlersatz, Rand-Leerzeichen, Überlänge, gesperrtes Clipboard, Wiederholung, Maskierung und Enter-Sperre |
| `form-empty-cancel-and-timeout` | Leere Eingaben starten nicht; Abbrechen und sofortiger injizierter Freigabe-Timeout verwerfen Werte |
| `form-explicit-once-and-clear` | Einfügeknöpfe, expliziter Start, doppelter Startversuch führt nur zu einem Runner-Aufruf; Felder und Dictionary geleert |
| `form-close-cancels-running` | Fenster bleibt während Bereinigung bestehen und schließt nach bestätigtem Abbruch |
| `ui-loop-failure-before-approval` | Ausnahme in echter Application.Run-Schleife vor Freigabe: kein Runnerstart, Fehlerbericht erst nach Dispose |
| `ui-loop-failure-cleans-before-report` | Tatsächliche UI-Ausnahme bei lebendem Ziel und Enkel; beide sind bereits vor dem Fehlerbericht beendet |
| `ui-loop-failure-cleanup-status-priority` | Derselbe UI-Fehlerpfad mit synthetischer Cleanup-Statusinjektion; Kategorie 9 bleibt auch bei fehlschlagender Fehleranzeige erhalten |
| `form-dispose-joins-independent-cleanup` | Dispose während laufendem Baum ohne UI-Pump; kontextfreie Runner-Bereinigung bestätigt und Eingaben geleert |
| `form-dispose-cleanup-status-priority` | Kategorie 9 bleibt nach Dispose und einer später abgearbeiteten UI-Fortsetzung erhalten |
| `gui-single-instance` | Tatsächlich gestartete zweite GUI-EXE liefert Kategorie 4 ohne Dialog |
| `gui-process-roundtrip` | Produktions-GUI-EXE separat gestartet, nur durch Testcode mit synthetischen Werten bedient, Start/Ergebnis/Schließen; Exit 0 und leere Launcher-Standardkanäle |
| `powershell-wrapper-roundtrip` | Zentraler Aufruf über die von Codex verwendete PowerShell-Laufzeit, GUI-Durchlauf, ausschließlich fester Ergebnisstatus des Skripts |
| `wrapper-busy-preserves-other-instance` | Kontrolliert geöffnete erste Instanz bleibt leer, ungestartet und aktiv; zweiter Wrapper erhält BUSY; alter PID-only-Driver verweigert Bedienung |
| `wrapper-ownership-rejects-other-and-exited-owner` | Gleicher EXE-Pfad und passende Zeitordnung reichen ohne tatsächliche Elternbeziehung nicht aus; beendete Owner/Kandidaten werden abgewiesen |
| `parent-options-validation` | Gebundene/ungebundene CLI-Aufrufe, fehlende Parameter, Null/negative/überlaufende IDs und Zeitstempel, eigene PID abgewiesen |
| `parent-handle-lifetime` | Mit Startzeit gebundener Prozesshandle löst nach hartem Ende des synthetischen Aufrufers Abbruchsignal aus |
| `parent-binding-mismatch-no-start` | Abweichende Erzeugungszeit führt ohne Dialog/Zielstart zu Kategorie 5 |
| `parent-already-ended-no-start` | Vor dem Launcherstart beendeter Aufrufer führt ohne Zielstart zu Kategorie 2 |
| `parent-killed-awaiting-approval` | Eingaben in normaler gebundener GUI, keine Freigabe; hartes Aufruferende schließt Launcher ohne Zielstart |
| `parent-killed-running-tree` | Nur synthetischen Aufrufer hart beenden; GUI meldet Kategorie 2, Ziel und Enkel binnen 5 Sekunden beendet |
| `parent-ended-before-form-shown` | Bereits gesetztes Aufrufer-Abbruchsignal schließt auch bei noch nicht vorhandenem Fensterhandle ohne Runner-Aufruf |
| `parent-cancels-without-ui-pump` | UI-Nachrichtenverarbeitung absichtlich aussetzen; Zielbaum wird trotzdem beendet; danach UI-Abschluss und Kategorie 2 |
| `powershell-wrapper-hard-kill` | Tatsächliches zentrales Skript mit synthetischem Baum; PowerShell per nicht rekursivem `Kill()` ohne `finally` beenden; Launcher, Ziel und Enkel binnen 5 Sekunden beendet |
| `parent-watch-disposal` | Zwölf Überwachungen bei weiterlebendem Aufrufer anlegen/freigeben; kein Handle-Wachstum oberhalb der kleinen Toleranz |
| `repeated-launch-handle-lifetime` | Zwölf weitere Starts ohne wachsenden Handle-Bestand über die festgelegte kleine Toleranz |

Der PowerShell-Host dieser Sandbox gibt beim Start teilweise eine feste `InitializeDefaultDrives`-Diagnose aus. Nur diese genaue bekannte Hostmeldung wird im Wrapper-Test separat toleriert; keine beliebigen Zielausgaben werden bereinigt oder freigegeben. Windows PowerShell 5.1 scheiterte hier an der AuthorizationManager-Prüfung vor Ausführung des Skripts; dieser Host ist in dieser Umgebung nicht nachgewiesen. Es wurde keine Ausführungsrichtlinie geändert.

Der Dialog wurde zusätzlich ohne eingetragene Werte mittels `DrawToBitmap` gerendert und visuell geprüft (`artifacts/preview.png`). Das ist eine Layoutprüfung des eigenen Fensters, kein Screenshot persönlicher Desktop-Inhalte und kein Ersatz für manuelle Tastatur-/Clipboard-Bedienung.

## Separater Codex-Werkzeug-Abbruchtest vom 27. Oktober

Aus einem Codex-Werkzeugprozess mit PTY wurde der Testmodus `--tool-cancel-test` des **Test-Runners** gestartet. Dieser startete das zentrale Aufrufskript, die normale GUI-EXE sowie einen synthetischen Zielprozess mit Enkel. Die Testautomatisierung erteilte ausschließlich für diesen synthetischen Auftrag die Startfreigabe; die produktive EXE hat keinen Autofill-/Autoapprove-Testschalter.

Nach `TOOL_CANCEL_READY` wurde über den Werkzeugkanal Strg+C gesendet. Anschließend wurden die zuvor gespeicherten numerischen Test-PIDs geprüft: Aufrufskript, Launcher, Ziel und Enkel waren beendet. Ergebnis: **`TOOL_CTRL_C_CLEANUP_OK`**. Metadaten/Ergebnis stehen unter `artifacts/tool-cancel-state.json` und `artifacts/tool-cancel-result.txt`; darin keine Secret-Werte.

Zur gezielten Wiederholung (nicht Teil des regulären Testlaufs):

```powershell
$env:SHANDOFF_TEST_POWERSHELL = (Get-Process -Id $PID).Path
.\tests\SecretHandoff.Tests\bin\Debug\net10.0-windows\SecretHandoff.Tests.exe --tool-cancel-test
# Erst nach TOOL_CANCEL_READY Strg+C senden, anschließend die gespeicherten Test-PIDs prüfen.
```

Am 28. Oktober wurde diese frühere `finally`-Abhängigkeit durch eine Überwachung des gebundenen Aufrufers ergänzt. Das zentrale Skript übergibt seine PID und Windows-Erzeugungszeit; der Launcher überprüft und hält den konkreten Prozesshandle. Die obigen zehn neuen Testgruppen belegen den Abbruch bei hartem Aufruferende, einschließlich des tatsächlichen PowerShell-Skripts. Der Wiederholungstest erfasste zunächst zusätzliche Handles durch eigens angelegte Überwachungsthreads; die Umsetzung verwendet nun einen freigegebenen Hintergrundtask und besteht den Handle-Test.

**Weiterhin nicht nachgewiesen:** der tatsächliche Abbruchknopf der Codex-Desktop-App und andere Host-/Terminal-Versionen. Der Launcher erkennt das Ende des gebundenen Prozesses. Falls Codex bei einem Auftragsabbruch den Launcher und diesen Prozess weiterleben lässt, wird allein das Ende des Chats/Auftrags nicht erkannt. Die manuellen Aufrufe ohne `--watch-parent` sind absichtlich nicht gebunden. Der harte Prozessabbruch-Test ersetzt keine vollständige Desktop-Integration und keine Sicherheitsisolation.

## Verhältnis zu den MVP-Kriterien

Die 47 Testgruppen sind nicht mit den 16 umfassenderen Abnahmekriterien gleichzusetzen. Für A02/A06 ist der .NET-Roundtrip nachgewiesen; ein separates natives CRT-Prüfprogramm fehlt. A03/A04/A07/A08/A09/A11/A13 werden durch mehrere konkrete Fälle abgedeckt, jedoch noch nicht in allen vorgesehenen Varianten. A10 ist für die Standardkanäle durch einen getrennten Testhost mit Null-Byte-Prüfung und Detektor-Negativkontrolle geprüft. A12 ist zusätzlich für hartes Aufruferende sowie UI-Ausnahme/Dispose bei laufendem Baum geprüft, bleibt wegen des echten Desktop-Abbruchs und der noch nicht in Echtzeit durchlaufenen 5-/15-Minuten-Fristen teilweise offen. A14 hat den Metadaten-Snapshot-Test und simulierte Pfadänderungen vor Start; eine gezielte Matrix verschachtelter/inkompatibler Jobs fehlt. A15 prüft Statuspriorität, keinen echten Kernel-Ausfall. Für A16 liegt Codeprüfung vor, noch keine vollständige Beobachtung von Dateischreibzugriffen über alle Pfade.

Die Testautomatisierung ordnet Wrapper-Kinder über gehaltene Prozesshandles, Erzeugungszeit, tatsächliche Eltern-PID und EXE-Pfad zu. Vor schreibenden GUI-Nachrichten wird zusätzlich der Fensterbesitz geprüft. Der bisherige PID-only-Modus `--drive-synthetic-gui` ist deaktiviert; die Produkt-EXE hat weiterhin keinen Autofreigabeschalter. Die Pfadtests verwenden echte lokale Dateien für den Positivfall; Netzlaufwerke, Reparse Points und Pfadänderungen werden instanzgebunden simuliert. Echte Junction-/SUBST-Integrationstests und konkurrierende Dateimanipulation wurden nicht ausgeführt. Die Ausgabetests erfassen keine anderen Ausgabekanäle des Zielprogramms und sind kein Isolationsnachweis.

Nächste konkrete Prüfungen:

1. Manueller Demo-Durchlauf mit echten Windows-Einfügewegen, Escape, Fensterschließen, langen Angaben und mehreren DPI-Einstellungen; ausschließlich öffentliche synthetische Werte verwenden.
2. Echten Codex-Desktop-Abbruch mit synthetischem Baum prüfen. Bei fehlender zuverlässiger Beendigung den Aufrufervertrag technisch ergänzen, bevor dieser Startweg als unterstützt gilt.
3. Offene Varianten der Abnahmekriterien ergänzen, danach ein selbstenthaltendes lokales Paket bauen und dessen Runtime-Pflege festlegen.

Private Projekte, Passwortmanager, echte Tokens und Kontozugriffe werden für diese Schritte nicht benötigt.
