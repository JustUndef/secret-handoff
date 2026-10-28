# Architektur und MVP

Stand: 28. Oktober 2026. **MVP-Spezifikation mit synthetischem Prototyp, keine gehärtete Sicherheitssoftware.** Die folgenden Festlegungen beschreiben das MVP-Soll. Der implementierte Stand, ausgeführte Prüfungen und noch offene Nachweise stehen in [TESTING.md](TESTING.md).

## 1. Ausgangslage und Nachweisstand

Der Nutzer arbeitet in Codex Desktop und bewahrt private Passwörter in seinem Passwortmanager auf. Ein allgemeiner lokaler Launcher soll pro Auftrag benötigte Werte entgegennehmen, den Zielaufruf zeigen und ihn nach manueller Freigabe starten. Es soll kein neues Übergabeskript für jedes Projekt nötig sein.

| Gegenstand | Nachweisstand | Aussagegrenze |
| --- | --- | --- |
| PowerShell-/WinForms-Referenz | Laut Projektauftrag aus Codex Desktop erfolgreich mit lokaler Eingabe getestet; Identitätsaufruf bestätigte `SERVICE_ACCOUNT` | Fester Einzelaufruf, kein allgemeiner Launcher; keine privaten Vault-Inhalte oder Kontakte abgerufen |
| Synthetischer Environment-Test | Laut Projektauftrag Übergabe mit `op run --environment` an einen Node-Prüfprozess erfolgreich | Environment-Auswahl begrenzt nicht die Berechtigung des angemeldeten Accounts |
| Referenzdateien in der Dokumentationsrunde | Nur die zwei ausdrücklich freigegebenen Skripte gelesen, nichts ausgeführt oder übernommen | Kein erneuter Kontozugriff |
| Neuer Workspace | Zu Beginn leer; keine vorhandenen AGENTS.md in Workspace oder geprüften übergeordneten Verzeichnissen, kein Git-Repository | Inzwischen eigener Prototyp mit synthetischen Tests; keine private Historie übernommen |
| Lokale Entwicklungsmittel | Der PATH-Host unter `C:\Program Files\dotnet` meldet SDKs für .NET 7, 8 und 9; der gesondert geprüfte Host `C:\Users\<Benutzer>\.dotnet\dotnet.exe` meldet SDK **10.0.203**. .NET-10-Desktop-Runtime unter Program Files: **10.0.13**, unter dem Benutzerpfad: **10.0.7** | Prototyp erfolgreich mit dem Benutzer-SDK gebaut und getestet |

Die Referenz zeigt verwendbare UI-Bausteine. Sie trimmt die Eingabe, liest Zielausgaben vollständig in den Speicher und beendet im Timeout nur den direkten Prozess. Diese Eigenschaften werden nicht als allgemeine MVP-Regeln übernommen. Ihre erfolgreiche Einzelprüfung belegt keine Härtung.

Vorherige Produktprüfungen werden nicht wiederholt. Service Accounts begrenzen Berechtigungen, ersetzen aber den gewünschten manuellen Dialog nicht. Eine weitere permanente Ablage ihres Tokens wurde verworfen. Summon, Keygrant, KeePass/KPScript und Pinentry liefern mögliche Teile; eine für diesen Ablauf vollständig geprüfte Kombination liegt nicht vor. Daraus folgt keine Behauptung, es gebe keine Alternativen.

## 2. Ziel und MVP-Grenze

Ein portables Windows-Programm öffnet einen lokalen Dialog und startet einen **neuen** Prozess mit einmalig eingegebenen Secret-Werten als Umgebungsvariablen. Der Nutzer genehmigt genau die angezeigten Metadaten. Das Programm ist weder Passwortmanager noch Berechtigungsbroker eines Providers.

Enthalten: Auftragsprüfung, vollständige Vorschau, verdeckte Eingabe, Einfügen, ausdrücklicher Start, Abbruch, feste Zeitlimits, Ausschluss gleichzeitiger Aufträge, Statuscodes, Prozessbaum-Bereinigung und synthetische Tests.

Nicht enthalten: Passwortmanager-API, Dienstezugriff im Kern, Hintergrunddienst, Webserver, Remote-IPC, nachträgliche Änderung laufender Desktop-Prozesse, automatische Agent-Tool-Freigabe, UAC-Erhöhung, interaktive Terminal-Sitzung, persistenter Auftragsspeicher oder Secret-Tresor. Ein Zielprogramm darf ein eigenes GUI-Fenster öffnen. Programme, die nur einen bestehenden Prozess ansprechen oder sich über Dienste ablösen, sind keine unterstützten betreuten Aufträge. 1Password kann später wie jedes andere explizit genehmigte Zielprogramm gestartet werden.

## 3. Technikentscheidung

**Empfehlung: C#, WinForms, .NET 10 LTS; erste unterstützte Plattform Windows 11 x64.** Native Formularfelder und Zwischenablagezugriff passen zum kleinen Windows-Dialog. Typisierte Aufträge und ein separat testbarer Ablauf vermeiden die wachsende Fehlerfläche eines allgemeinen PowerShell-Launchers. WinForms ist ein Windows-Desktop-Framework; .NET 10 ist die aktuelle LTS-Linie. Das ist eine Projektentscheidung auf Basis der [WinForms-Dokumentation](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/) und der [Microsoft-Supportpolicy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), geprüft am 27. Oktober 2026.

Der Release-Bau erzeugt eine lokal entpackbare, selbstenthaltende Veröffentlichung und einen Benutzer-Installer ohne global vorausgesetzte .NET-Laufzeit; siehe [RELEASING.md](RELEASING.md). Sie muss bei Runtime-Sicherheitsupdates neu gebaut werden. Der aktuelle Prototyp ist frameworkabhängig und verwendet die vorhandene Desktop-Runtime. Der nachträglich beauftragte Installer erweitert den ursprünglichen MVP-Umfang; ein Autoupdater bleibt ausgeschlossen. Das vorhandene .NET-10-SDK lässt sich über `C:\Users\<Benutzer>\.dotnet\dotnet.exe` ausdrücklich auswählen; eine Neuinstallation oder globale PATH-Änderung ist dafür nicht nötig. Die erste Prüfung nur des PATH-Hosts hatte diese separate Installation nicht erfasst.

Eine GUI-EXE (`WinExe`) enthält Eingabe und Ablauf. Der implementierte allgemeine Aufruf lautet `SecretHandoff.exe --request <absoluter JSON-Pfad>`. Ohne Argumente öffnet sie eine Dateiauswahl. Der Dateiauswahlpfad wird nicht als Verlauf gespeichert. Der Aufrufer benötigt kein interaktives Terminal und übergibt nur einen Metadatenpfad. Er muss bei Bedarf über seine Prozess-API auf die GUI-EXE warten und deren Exitcode auswerten; ein Shell-Aufruf wartet bei GUI-Programmen nicht zwingend automatisch. Das zentrale Skript `scripts/Invoke-Handoff.ps1` übernimmt dieses Warten. Keine Integration in die Umgebung bereits laufender Codex-Prozesse wird versprochen.

Für Agent-Aufträge bindet das zentrale Skript den Launcher an seinen eigenen Prozess. Dazu dient die optionale EXE-Schnittstelle `--request <Pfad> --watch-parent <PID> <Startzeit-FILETIME>`. Der Launcher öffnet einen nicht vererbbaren Windows-Prozesshandle mit `SYNCHRONIZE` und `PROCESS_QUERY_LIMITED_INFORMATION`, vergleicht die Windows-Erzeugungszeit und behält denselben Handle bis zum Auftragsende. Damit wird eine später wiederverwendete PID nicht mit dem ursprünglichen Aufrufer verwechselt. Geprüft werden ausschließlich Lebensdauer und Zeitstempel; es gibt weder Speicherauslesen noch ein Berechtigungsversprechen aufgrund dieser Metadaten. Grundlagen: [OpenProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess), [GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes) und [Windows-Prozess als Synchronisationsobjekt](https://learn.microsoft.com/en-us/windows/win32/sync/synchronization-objects).

Ein Hintergrundtask beobachtet den Handle mit begrenzten Warteintervallen von 50 ms. Bei erkanntem Prozessende oder einem Wartefehler wird der Auftrag abgebrochen. Der Abbruchtoken des Prozessrunners ist direkt mit dieser Überwachung verbunden, sodass die Zielbereinigung unabhängig von der UI-Ereignisschleife anläuft. Vor Freigabe schließt der Dialog ohne Zielstart; während der Ausführung schließt er nach der Bereinigung automatisch. Bereits erzeugte Seiteneffekte können durch eine zeitgleiche Erkennung des Aufruferendes nicht rückgängig gemacht werden. Beim Freigeben der Überwachung wird zuerst ihr Task beendet und danach der Handle geschlossen.

Fehlende bzw. bereits beendete Aufrufer führen ohne Zielstart zu `CANCELLED`, ein nicht passender Zeitstempel zu `INVALID_REQUEST`, fehlende Rechte zur Überwachung zu `START_FAILED`. Bei gebundenen Aufrufen werden Start-/Konfigurationsfehler über den Exitcode zurückgegeben, ohne einen verwaisten modalen Fehlerdialog zu öffnen. Das JSON-Format bleibt unverändert und enthält keine Prozessbindung.

Der Aufrufervertrag bleibt: Ein Codex-Auftragsabbruch muss entweder den Launcher direkt beenden oder den gebundenen Aufruferprozess beenden. Wird nur das Warten eingestellt und der gebundene Prozess bleibt am Leben, kann der Launcher diesen Auftragssachverhalt nicht erkennen. Der tatsächliche Desktop-Abbruchknopf ist weiterhin gesondert nachzuweisen. Der manuelle Start ohne Bindung bleibt unabhängig und wird im lokalen Fenster beendet.

| Baustein | Verantwortung |
| --- | --- |
| RequestLoader / Validator | Begrenztes JSON einlesen, Pfade und Namen prüfen, unveränderlichen Auftrag erzeugen |
| LocalPathPolicy | Lokales Laufwerk und jede kanonische Pfadkomponente prüfen; Netzlaufwerke, DOS-Aliase und Reparse Points abweisen |
| ApprovalForm | Vollständige Vorschau, lokale Eingabe im STA-Thread, Start-/Abbruchereignisse |
| ApprovalLifecycle | Einmalige Ausführung und Abbruch besitzen; Runner und Bereinigung unabhängig von UI-Fortsetzungen abwarten |
| ParentProcessWatch | Geprüften Aufruferhandle beobachten; beim erkannten Ende Abbruch auslösen; Handle und Task freigeben |
| EnvironmentPolicy | Neuen Umgebungsblock aus festgelegter Basis und eingegebenen Werten aufbauen |
| WindowsProcessRunner | Win32-Start, begrenzte Handle-Vererbung, Job Object, Warten und Beenden |
| SyntheticProbe / Tests | Lokale Prüfprozesse ohne Netz-/Kontozugriffe; ausschließlich synthetische Werte |

Kein allgemeines Plugin-System und kein lokaler Agent-Server. Der kleine Win32-Adapter ist nötig, um die Job-Zuordnung bereits bei der Prozesserzeugung vorzunehmen. Ein ungeschütztes `Process.Start()` mit nachträglichem Zuordnen wäre für die geforderte Abbruch-/Absturzbehandlung unzureichend.

## 4. Auftragsvertrag

Die UTF-8-JSON-Datei enthält ausschließlich Secret-Namen und Beschreibungen sowie Zielprogramm, Argumente und Arbeitsverzeichnis. Keine Werte, Platzhalterersetzung, eingebetteten Skripte oder frei definierbaren Umgebungswerte. Strukturelles Beispiel mit absichtlich nicht vorausgesetzten Pfaden:

```json
{
  "executable": "C:\\Tools\\ExampleWorker.exe",
  "arguments": ["--mode", "check", "--label", "Beispielauftrag"],
  "workingDirectory": "D:\\Projects\\Example",
  "secrets": [
    {
      "name": "EXAMPLE_API_TOKEN",
      "description": "API-Token für diesen Beispielauftrag"
    }
  ]
}
```

Die Dateiendung ist frei; `.handoff.json` ist die empfohlene Konvention. Es gibt zunächst genau diesen Vertrag ohne optionale Erweiterungsfelder. Validierung erfolgt **vor** der Eingabe:

- Unbekannte oder doppelte JSON-Schlüssel, falsche Typen und fehlende Pflichtfelder ablehnen. Grenzen: 64 KiB Datei, 1–16 Secrets, 128 Argumente, 128 Zeichen je Name, 512 je Beschreibung. Die serialisierte Windows-Aufrufzeile einschließlich Abschluss-NUL darf 32.767 UTF-16-Codeeinheiten nicht überschreiten.
- `executable` ist ein bestehender absoluter lokaler `.exe`-Pfad, `workingDirectory` ein bestehender absoluter lokaler Ordner. Auch die Auftragsdatei unterliegt der lokalen Pfadprüfung. Keine UNC-/Gerätepfade, PATH-Suche, Dateizuordnung oder Umgebungsvariablen-Ersetzung.
- Argumente sind einzelne Zeichenketten, dürfen leer sein und enthalten niemals Secret-Werte oder Secret-Referenzen. Keine Ersetzung von `%NAME%`, `$env:NAME` oder `${NAME}` durch den Launcher. NUL ablehnen; Steuerzeichen in Argumenten sichtbar als Escape-Sequenz darstellen. Beschreibungen/Pfade dürfen keine irreführenden Steuer- oder Bidi-Formatzeichen enthalten.
- Namen nach `[A-Za-z_][A-Za-z0-9_]*`, Duplikate ohne Beachtung der Groß-/Kleinschreibung ablehnen. Alle Secrets sind erforderlich.
- Metadaten können ihrerseits sensibel sein. Keine automatische Übernahme von Werten aus Chat oder Zwischenablage in JSON; keine rohen Auftragsdateien in Fehlerlogs. Der Launcher kann einen versehentlich in Metadaten gespeicherten Secret-Wert nicht zuverlässig erkennen.

Unterstützt werden kanonische absolute DOS-Pfade auf verfügbaren lokalen Fest-, Wechsel-, CD-ROM- und RAM-Laufwerken. Gewöhnliche Schrägstriche und Punktsegmente werden vor Dateizugriffen syntaktisch normalisiert; die Vorschau verwendet den kanonischen Snapshot. Vor Attribut- oder Inhaltszugriffen werden Laufwerkstyp und direkte DOS-Geräteabbildung geprüft. Netzlaufwerke, unbekannte Laufwerke und SUBST-Aliase werden abgewiesen. Jede kanonische Pfadkomponente einschließlich der Laufwerkswurzel wird einzeln geprüft; Reparse Points einschließlich Junctions, symbolischer Links, eingebundener Ordner und entsprechender Cloud-Platzhalter sind ausgeschlossen. Alternative Datenströme, Gerätenamen, Wildcards und mehrdeutige abschließende Punkte/Leerzeichen werden ebenfalls abgewiesen. Scheitert eine Abfrage, wird der Auftrag abgewiesen, auch bei einer Sandbox-Beschränkung.

Die Datei wird einmal gelesen. Die Vorschau und der Start verwenden denselben unveränderlichen Auftrag. Programm und Arbeitsverzeichnis werden zu Beginn des Runners und unmittelbar vor `CreateProcessW` erneut mit derselben Pfadpolicy geprüft; kein erneutes stilles Einlesen geänderter Argumente. Ein neues Einlesen erfordert einen neuen Dialog. Die Prüfung bindet keine Dateiidentität und sperrt Pfadkomponenten nicht gegen Austausch. Zwischen Laufwerksprüfung, Komponentenprüfungen und Start bleiben Zeitfenster für konkurrierende Änderungen; dabei kann auch ein anderer oder entfernter Zielpfad erreichbar werden. Die Policy ist keine Isolation gegen berechtigte lokale Manipulation.

## 5. Dialog und Werte

Das Fenster zeigt den absoluten Auftragspfad, den Programmpfad, jedes Argument einzeln mit Index und eindeutiger Zeichenketten-Darstellung, das Arbeitsverzeichnis und alle Secret-Namen mit Beschreibung. Die Argumentliste ist maßgeblich; eine zusätzlich angezeigte Aufrufzeile dient nur der Übersicht. Lange Angaben sind vollständig per Scrollen erreichbar und werden nicht nur mit Auslassungspunkten angezeigt. Pfad und Arbeitsverzeichnis bleiben während der Eingabe unverändert sichtbar.

Zusätzlich sichtbar: feste Umgebungspolitik, Ausgabeunterdrückung, Zeitlimits und Hinweis, dass Ziel und Nachkommen die Werte erhalten und der Launcher keine Programmisolation bietet. Aus einem Anzeigenamen wird keine vertrauenswürdige Identität abgeleitet.

Pro Secret ein verdecktes Textfeld (`UseSystemPasswordChar`), Einfügeknopf und Strg+V über denselben Einfügepfad. Kein automatisches Lesen der Zwischenablage, kein Klartext-Schalter, keine Kopier-/Ausschneidefunktion. Einfügen ersetzt die markierte Auswahl genau einmal; ein belegtes Clipboard liefert eine feste lokale Meldung ohne seinen Inhalt. Keine stillschweigende Kürzung oder Normalisierung. NUL und Zeilenumbrüche ablehnen; sonstige Unicode-Zeichen und führende/abschließende Leerzeichen unverändert lassen. Leere Werte ablehnen; bis 16.384 UTF-16-Codeeinheiten je Wert, darüber feste Fehlermeldung ohne Wert. Das ist eine bewusste MVP-Grenze für einzeilige Secrets.

**Starten** ist erst bei gültigen Feldern möglich und erfordert eine ausdrückliche Aktivierung per Maus oder Tastatur auf dem Startknopf; Enter im Eingabefeld startet nicht. Escape, Abbrechen und Fensterschließen vor Start verwerfen den Auftrag. Doppelklick oder konkurrierende Ereignisse dürfen höchstens einen Prozess erzeugen.

Nach Start werden Felder und temporäre Werteverweise so früh wie möglich geleert. Während der Laufzeit bleibt ein Statusfenster mit **Auftrag beenden** offen; Schließen beendet ebenfalls den Auftrag. Keine Wiederverwendung bei Neustart oder Fehler. Die Zwischenablage wird weder gespeichert noch automatisch gelöscht: Löschen würde die Historie ohnehin nicht zuverlässig entfernen und könnte neueren Nutzerinhalt überschreiben. Clipboard-Historie, Synchronisation und Passwortmanager-Verhalten bleiben außerhalb des Werkzeugs.

`ApprovalLifecycle` besitzt den Runner-Task und dessen Abbruchquelle unabhängig von der WinForms-Ereignisschleife. Beim Freigeben des Formulars wird zuerst abgebrochen und die Runner-Bereinigung abgewartet; dabei wird nicht auf eine UI-Fortsetzung gewartet. Auch nach einer Ausnahme aus `Application.Run` erfolgt eine feste Fehleranzeige erst nach diesem Schritt. `CLEANUP_FAILED` behält Vorrang vor einem UI-Fehler; eine fehlschlagende Fehleranzeige überschreibt dieses Ergebnis nicht. Späte UI-Fortsetzungen bedienen keine bereits freigegebenen Controls.

## 6. Umgebung, Argumente und Prozesserzeugung

Der Ziel-Umgebungsblock wird neu aufgebaut, **nicht aus dem Elternprozess kopiert**. Feste minimale Basis: `SystemRoot` und `WINDIR` aus dem Windows-Verzeichnis per Betriebssystem-API; `PATH` ausschließlich aus dessen `System32`- und Windows-Ordner. Dazu kommen nur die bestätigten Secret-Namen und Werte. Keine geerbten Provider-Tokens, Suchpfade oder Laufzeitoptionen, keine automatische Fallback-Vererbung. Programme, die weitere Variablen brauchen, können mit diesem MVP inkompatibel sein; eine spätere Erweiterung braucht eine eigene geprüfte Policy. Das verändert keine Benutzer-/Systemvariablen. Bereits im aufrufenden Prozess vorhandene Secrets werden dadurch nicht rückwirkend entfernt.

Secret-Namen werden zusätzlich ohne Beachtung der Groß-/Kleinschreibung gegen eine versionierte Sperrliste geprüft. Ausgangsliste: `PATH`, `PATHEXT`, `SystemRoot`, `WINDIR`, `COMSPEC`, `TEMP`, `TMP`, `HOME`, `USERPROFILE`, `APPDATA`, `LOCALAPPDATA`, `PSModulePath`, `ENV`, `BASH_ENV`, `JAVA_TOOL_OPTIONS`, `_JAVA_OPTIONS`, `JDK_JAVA_OPTIONS`; außerdem Präfixe `SHANDOFF_`, `DOTNET_`, `CORECLR_`, `COMPlus_`, `COR_`, `NODE_`, `PYTHON`, `RUBY`, `PERL`, `LD_`, `DYLD_`, `ELECTRON_`, `GIT_`, `SSH_` und `PROGRAM`. Damit sollen offensichtliche Suchpfad-/Laufzeitsteuerungen nicht als Secrets eingeschleust werden. Die Sperrliste ist keine vollständige Erkennung aller programmspezifischen Steuervariablen und kein Schutz vor genehmigtem schädlichem Zielcode.

Programm und Argumentliste bleiben im Modell getrennt. Der Adapter verwendet `CreateProcessW` mit explizitem `lpApplicationName`, eigenem Unicode-Umgebungsblock und Arbeitsverzeichnis. Windows benötigt dabei technisch eine Aufrufzeichenkette; sie wird an genau einer Stelle aus der Liste mit getesteter Windows-/CRT-Quotierung erzeugt. Keine Shell-Zeichenketten zusammensetzen. Leere Argumente, Leerzeichen, Anführungszeichen und Backslashes müssen den Roundtrip in .NET-/CRT-Prüfprozessen bestehen. Programme mit eigener abweichender Argumentauswertung benötigen einen gesonderten Kompatibilitätsnachweis. Grundlagen: [CreateProcessW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw) und [CRT-Argumentregeln](https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments?view=msvc-170).

Keine automatisch vorgeschalteten `cmd.exe /c`, `powershell -Command` oder vergleichbaren Interpreter. `.cmd`, `.bat` und `.ps1` werden nicht direkt gestartet. Ein ausdrücklich konfigurierter Interpreter, etwa `pwsh.exe` mit `-File` und einem Skriptpfad oder `node.exe` mit einem Skriptpfad, ist ein genehmigter Zielaufruf; er wird mit allen Argumenten angezeigt. Dessen eigene Auswertung und das Skript gehören zur Vertrauensentscheidung. Keine Sicherheitsgarantie aufgrund einer Endung oder eines Pfads.

Der Runner erstellt ein unbenanntes Job Object mit `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, ohne Breakaway-Freigaben. Die Zuordnung erfolgt während `CreateProcessW` mittels `STARTUPINFOEX` / `PROC_THREAD_ATTRIBUTE_JOB_LIST`; keine nachträgliche Zuordnung mit ungeschütztem Zeitfenster. Der Job-Handle bleibt nicht vererbbar und wird nicht an Ziel oder Aufrufer weitergegeben. Nur explizite Standard-I/O-Handles werden über eine Handle-Liste vererbt. Scheitern Job-Konfiguration, Attributliste oder Start, gibt es keinen unbetreuten Ersatzstart. Diese Planung nutzt die dokumentierten [Prozesserzeugungsattribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).

## 7. Ablauf, Abbruch und Status

Zustände: `Validating → AwaitingApproval → Starting → Running → CleaningUp → Terminal`. Jeder Pfad nach einer Eingabe führt über Bereinigung. Vor `Starting` ist kein Zielprozess erlaubt. Ein Abbruch während `Starting` wird verriegelt und beendet einen gerade erzeugten Job, bevor der Auftrag als abgeschlossen gilt. Windows darf bereits genehmigten Zielcode unmittelbar nach Prozesserzeugung ausführen; ein zeitgleicher Abbruch kann ausgeführte Seiteneffekte nicht zurücknehmen.

- Pro Windows-Benutzer und interaktiver Sitzung ein Auftrag, umgesetzt mit einem sitzungsgebundenen, benutzerbezogenen Mutex. Weitere Anfragen erhalten sofort `BUSY`, ohne Eingabefenster, Queue oder Wertübernahme. Der Mutex ist keine Identitäts-/Sicherheitsgrenze gegen denselben Benutzer.
- Freigabezeitlimit: 5 Minuten ab Dialoganzeige. Laufzeitlimit: 15 Minuten ab erfolgreicher Prozesserzeugung. Feste MVP-Werte; ein anderer Bedarf ist eine spätere Vertragsänderung. Kein verstecktes unbegrenztes Warten.
- Abbruch, Fensterschließen und Laufzeitlimit beenden den gesamten betreuten Job. Es gibt keinen „im Hintergrund weiterlaufen“-Pfad. Bei normalem Ende des direkten Zielprozesses werden verbleibende Job-Nachkommen ebenfalls beendet; dieses Verhalten wird vor Start angezeigt.
- Nach einem Beendigungsereignis wird der Job terminiert, sein Prozessbestand bis null geprüft und alle Handles werden geschlossen. Bereinigungsbudget: 5 Sekunden. Bei ausbleibender Bestätigung `CLEANUP_FAILED`, keine Erfolgsmeldung; letzter Job-Handle wird trotzdem geschlossen. Unter normal funktionierendem Windows sollen innerhalb dieses Budgets keine betreuten Prozesse verbleiben.
- Bei Launcher-Absturz soll das Schließen seines letzten Job-Handles durch Windows die betreuten Prozesse beenden. Ein bereits bei Start bestehender übergeordneter Job, etwa des Desktop-Aufrufers, muss im Integrationstest berücksichtigt werden. Unvereinbare Einschränkungen führen zum Startfehler.
- Bei gebundenem Aufruferende greift derselbe Abbruch-/Bereinigungspfad auch ohne `finally` des Aufrufskripts. Eine noch offene Ergebnisanzeige schließt automatisch; ein bereits ermitteltes Ergebnis, insbesondere `CLEANUP_FAILED`, bleibt dabei erhalten.
- Solange kein gebundener Aufrufer endet, bleibt die feste Ergebnisanzeige lokal bis Bestätigung sichtbar; keine Eingabefelder oder Werte bleiben erhalten. Nach Abschluss des Jobs kann der bereits bereinigte Prozess seinen Mutex freigeben. Aufrufer warten bis zum Schließen dieser Ergebnisanzeige auf den Exitcode; UI-Wartezeit ist keine Zielprozess-Laufzeit.

Windows Job Objects betreuen übliche per Prozesserzeugung gestartete Nachkommen. Sie sind keine Sandbox: Übergaben an bestehende Prozesse, Dienste oder alternative Aktivierungswege werden nicht umfassend erfasst. Solche Aufträge sind nicht unterstützt und dürfen nicht als „keine verwaisten Prozesse unter allen Umständen“ beworben werden. Grundlage: [Microsoft: Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).

| Exitcode | Feste Kategorie | Bedeutung |
| --- | --- | --- |
| 0 | `COMPLETED` | Direkter Zielprozess mit Code 0 beendet; Job-Bereinigung bestätigt |
| 2 | `CANCELLED` | Nutzerabbruch oder erkanntes Aufruferende; falls gestartet, Bereinigung bestätigt |
| 3 | `TIMED_OUT` | Freigabe- oder Laufzeitlimit erreicht; falls gestartet, Bereinigung bestätigt |
| 4 | `BUSY` | Ein anderer Auftrag ist aktiv |
| 5 | `INVALID_REQUEST` | Vertrag, Pfad oder Umgebungsname ungültig; kein Zielstart |
| 6 | `START_FAILED` | Zielstart/Job-Einrichtung fehlgeschlagen; kein weiterlaufender betreuter Prozess |
| 7 | `TARGET_FAILED` | Zielprozess mit anderem Code beendet; Bereinigung bestätigt |
| 8 | `INTERNAL_ERROR` | Interner Fehler; falls gestartet, Bereinigung bestätigt |
| 9 | `CLEANUP_FAILED` | Beendigung nicht vollständig bestätigt; hat Vorrang vor allen anderen Kategorien |

Der numerische Ziel-Exitcode darf lokal angezeigt werden, wird aber nicht als Launcher-Exitcode durchgereicht. Fehlende Eingaben halten den Dialog offen und erzeugen keinen Prozess. Sie sind kein terminaler Fehlerstatus. Fehlertexte bestehen aus eigenen festen Meldungen und gegebenenfalls numerischem Windows-Fehlercode; keine rohen Exceptions oder Zielmeldungen.

## 8. Ausgaben und Sicherheitsgrenzen

Ziel-stdin liest aus `NUL`; stdout und stderr schreiben nach `NUL`. Keine Pipes, Ausgabe-Puffer, Konsolenvererbung, Rohdaten-Vorschau oder Ausgabe-Dateien des Launchers. Somit können auch sehr große oder binäre Ausgaben keinen unbegrenzt wachsenden Erfassungspuffer erzeugen. Der Launcher selbst schreibt keine Standardausgabe/-fehler und keine persistenten Logs; Codex erhält höchstens einen vom Aufrufer aus dem Exitcode abgeleiteten festen Status. Auch im Fehlerfall keine ungeprüften Zieltexte. Für Diagnosen dienen ausschließlich synthetische Prüfprogramme und strukturierte Testresultate.

Diese Unterdrückung begrenzt nur diese Standardkanäle. Das genehmigte Ziel kann eigene Fenster, Dateien, Netzwerkverbindungen, Telemetrie, bestehende Prozesse oder andere Ausgabekanäle verwenden. Es kann Secrets transformieren, weitergeben oder dauerhaft speichern. Ein späterer Ausgabefilter wäre keine vollständige Sicherheitsgarantie. Das Tool ist kein Netzwerkfilter und erteilt dem Ziel keine gegenüber dem Benutzer eingeschränkten Betriebssystemrechte.

Weitere Grenzen:

- Ziel und normale Nachkommen können die Umgebungswerte lesen und vererben. Das Leeren von UI-Feldern oder Launcher-Verweisen entfernt keine bereits im Ziel kopierten Werte.
- Ausreichend berechtigte Schadsoftware desselben Windows-Benutzers, Debugger, Speicherabbilder und Systemadministratoren sind außerhalb des Schutzversprechens. Prozessumgebung ist kein geschützter Tresor.
- UI, .NET-Strings, Clipboard und native Umgebungsblöcke können Kopien enthalten. Veränderbare eigene Puffer werden nach Gebrauch bestmöglich überschrieben und Handles zuverlässig freigegeben; sofortige vollständige Speicherlöschung wird nicht garantiert. Keine eigenen Dumps oder Crash-Uploads; Betriebssystem-/Fremd-Dumps können nicht ausgeschlossen werden.
- Keine Secret-Dateien, Registry-Werte, Credential-Manager-/DPAPI-Ablage oder globalen Umgebungsänderungen durch den Launcher. Paging, Ruhezustand, Clipboard-Verlauf und Zielprogramm-Ablage kann er nicht ausschließen.
- Ein angezeigter App-Name oder Pfad ist keine Account-/Vault-Berechtigung und kein Beweis unveränderten Codes. Skripte, Module, Arbeitsverzeichnis, Dateiaustausch zwischen Prüfung und Start und lokale Manipulation bleiben relevant. Es gibt keinen automatischen Vertrauensspeicher.
- Prozessende beendet die lokale Bereitstellung, widerruft aber kein Token und macht bereits erfolgte externe Aktionen nicht rückgängig.

## 9. Prüffähige MVP-Abnahmekriterien

Alle Tests verwenden synthetische Werte und lokale Prüfprogramme ohne echte Konten oder Netzwerkzugriffe. Keine Secret-Werte in Testberichten, Snapshots, Screenshots oder Logs. Ein lokaler Prüfprozess meldet nur feste Prüfcodes; der separate Ausgabeverwerfungstest schreibt absichtlich synthetische Werte in die verworfenen Standardkanäle. Die produktive I/O-Policy wird dabei unverändert verwendet. Automatisierte GUI-Prüfungen aus Codex-Werkzeugprozessen sind durchgeführt; eine manuelle Bedienprüfung und der tatsächliche Desktop-Abbruchknopf sind noch offen. Nachweise: [TESTING.md](TESTING.md).

| ID | Prüfung | Erforderliches Ergebnis |
| --- | --- | --- |
| A01 | Start aus Codex Desktop ohne vorbereitetes Terminal; langer Zielpfad und mehrere Secrets | Vollständige, lesbare Vorschau; kein Zielstart vor Freigabe; alle Argumente erreichbar |
| A02 | Synthetische Werte inkl. Unicode, Leerzeichen am Rand und Sonderzeichen übergeben | Exakte Übereinstimmung im Ziel, keine Werte in argv oder Auftragsdatei; keine Änderung der Aufrufer-/Benutzer-/Systemumgebung |
| A03 | Abbrechen, Escape und Fensterschließen vor Start sowie 5-Minuten-Freigabezeitlimit | Kein Prüfprozess erzeugt, kein Startmarker; Eingaben verworfen, Kategorie 2 bzw. 3 |
| A04 | Ein oder mehrere Felder leer; NUL, Zeilenumbruch oder überlanger Wert | Verständliche feste lokale Meldung, keine Kürzung, kein Zielstart; gültige Eingabe ermöglicht späteren Start |
| A05 | Einfügeknopf und Strg+V mit Auswahl, leerer und vorübergehend gesperrter Zwischenablage | Exakt einmaliges Einfügen, stets verdeckt; kein automatisches Clipboard-Lesen; nach Sperre erneuter Versuch möglich |
| A06 | Argumente: leer, Leerzeichen, `"`, Backslash vor `"`/am Ende, Unicode und `& \| > % $ ;` | Identischer argv-Roundtrip; keine unerwartete Shell-Aktion, Expansion oder Zusatzprozesse |
| A07 | Ungültiges JSON, doppelte Schlüssel, unbekannte Felder, relative/fehlende Pfade, direkte Skriptdatei | Kategorie 5 vor Werteingabe, kein Zielstart; keine rohen JSON-/Exception-Inhalte im Agent-Kontext |
| A08 | Case-Duplikate, `Path`, `NODE_OPTIONS` und andere Sperrnamen; synthetische Credentials im Elternprozess | Ungültige Namen abgewiesen; Ziel erhält exakt die festgelegte Basis und genehmigte Namen, keine geerbten Credential-/Steuervariablen |
| A09 | Fehlendes/ungültiges EXE, verweigerter Start oder scheiternde Job-Zuordnung | Feste Kategorie 5 oder 6 nach Fehlerphase, keine Rohfehler, kein unbetreuter Ersatzstart |
| A10 | Ziel schreibt synthetische Secrets auf stdout/stderr, binär und in großer Menge | Beide Kanäle verworfen; keine Werte im Agent-Kontext oder Launcher-Dateien; kein Ausgabepufferwachstum/Deadlock |
| A11 | Ziel endet mit 0 bzw. Nichtnull, mit/ohne weiterlaufende Nachkommen | Kategorie 0 bzw. 7, Zielcode lokal korrekt; Job danach leer, alle Handles freigegeben |
| A12 | Abbruch während Start und Laufzeit; Laufzeitlimit; Launcher während Start/Lauf töten; aufrufenden Agent-Auftrag in Codex Desktop abbrechen | Unter normal funktionierendem Windows kein betreuter Prozess nach spätestens 5 Sekunden; Status erst nach Bereinigung; Absturz und Aufruferabbruch beenden Job ebenfalls |
| A13 | Zwei parallele Anfragen, schneller Doppelklick auf Start, wiederholte Aufträge | Zweite Anfrage erhält 4 ohne Dialog; höchstens ein Start je Freigabe; keine Wiederverwendung von Werten, kein wachsender Handle-Bestand |
| A14 | Auftrag nach Vorschau ändern; Ziel im übergeordneten Desktop-Job starten | Metadaten bleiben identisch zum genehmigten Snapshot; kompatible Job-Verschachtelung funktioniert, sonst Kategorie 6 |
| A15 | Bereinigungsfehler synthetisch injizieren | Kategorie 9 überschreibt Erfolg/Abbruch; kein falsches „beendet“; letzter Job-Handle wird geschlossen |
| A16 | Sämtliche Erfolgs-/Fehlerpfade auf Dateischreibzugriffe und Diagnosekanäle prüfen | Keine vom Launcher persistierten Secrets, Rohlogs oder Telemetrie; keine Secret-Werte in Testartefakten |

Für A03/A11/A12 darf das synthetische Prüfprogramm einen wertfreien Startmarker in einem Testverzeichnis und bekannte lokale Synchronisationsereignisse verwenden. Nachkommen/PIDs werden kontrolliert nachverfolgt; es werden keine fremden Prozesse beendet. Zeitlimits erhalten eine intern injizierbare monotone Uhr für schnelle Tests; mindestens ein manueller Test prüft die echten Fristen. A16 ist ein begrenzter Nachweis für die getesteten Pfade, keine Garantie über beliebigen Zielcode oder das Betriebssystem.

## 10. Erster Implementierungsschritt

**Eine synthetische vertikale Scheibe, noch keine Freigabe für reale Secrets.** Dieser Schritt ist als erster Prototyp umgesetzt; die folgenden Punkte beschreiben seinen Umfang. Dafür wird das vorhandene .NET-10-SDK über `C:\Users\<Benutzer>\.dotnet\dotnet.exe` verwendet; kein globaler Installationsschritt nötig.

1. Kleine Projektstruktur mit WinForms-EXE, UI-unabhängigen Vertrags-/Ablaufklassen, isoliertem Win32-Runner und lokalem synthetischem Prüfprogramm anlegen. Keine Provider-Abhängigkeit und kein privater Referenzcode nötig.
2. Genau den obigen Auftragsvertrag, feste Umgebungspolicy, vollständige Vorschau und verdeckte Eingabe mit beiden Einfügewegen implementieren. Nur synthetischen Probeauftrag als mitgeliefertes Beispiel verwenden.
3. Direkten Start mit Job-Zuordnung bei Erzeugung, `NUL`-Handles und Einmal-Freigabe implementieren. Abbrechen vor/während Start, Beenden während Laufzeit und normales Zielende müssen bereits bereinigen; kein vorläufiger unbetreuter Launcher.
4. Zuerst A02, A03, A05, A06, A08, A10 und A11 sowie den Start-/Absturz-/Aufruferabbruchpfad aus A12 mit synthetischem Kind und Enkel prüfen. Aus Codex Desktop die lokale Dialogbedienung und den Aufrufervertrag manuell nachweisen. Fehlende MVP-Prüfungen offen ausweisen.

Review-Ergebnis dieser Scheibe: Quellcode, wertfreier generierter Beispielauftrag unter `artifacts/demo`, reproduzierbare lokale Testbefehle und [Prüfbericht](TESTING.md) mit tatsächlich ausgeführten Tests und offenen Kriterien. Erst die vollständige Umsetzung und Prüfung von A01–A16 erlaubt die Bezeichnung „MVP erfüllt“; auch dann gelten die dokumentierten Sicherheitsgrenzen.
