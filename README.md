# Secret Handoff

Ein Windows-Prototyp für die bewusste manuelle Übergabe von Secrets an **neu gestartete Programme und Agent-Aufträge** in beliebigen Projekten.

Der Nutzer arbeitet in Codex Desktop. Ein lokales Fenster zeigt Programm, Argumente, Arbeitsverzeichnis und benötigte Secret-Namen. Der Nutzer fügt die Werte aus seinem Passwortmanager in verdeckte Felder ein und bestätigt ausdrücklich den Start. Der Launcher übergibt die Werte ausschließlich über die Umgebung des Zielprozesses; dessen Nachkommen können sie erben.

## Stand am 28. Oktober 2026

**Ausführbarer Prototyp für synthetische Tests; noch kein vollständig abgenommener MVP.** JSON-Aufruf, Dialog, minimale Zielumgebung, direkte Prozesserzeugung, Job-Object-Bereinigung und Überwachung des Aufruferprozesses sind implementiert. Die vier Review-Befunde wurden nachgebessert: eindeutige Testprozess-Zuordnung, Bereinigung bei UI-Fehlern, unabhängig überprüfte Ausgabeunterdrückung und lokale Pfadprüfung. Build ohne Warnungen/Fehler, 47 synthetische Testgruppen bestanden. Einzelheiten und offene Kriterien: [TESTING.md](TESTING.md). Der Quellcode steht unter der [MIT-Lizenz](LICENSE). Installer, portable Pakete und der automatisierte Release-Bau sind in [RELEASING.md](RELEASING.md) beschrieben.

Im Vorgängerprojekt wurden ein begrenzter PowerShell-/WinForms-Dialog mit Kindprozess-Übergabe und ein synthetischer `op run --environment`-Aufruf getestet. Die Ergebnisse sind im Projektauftrag berichtet; hier wurden lediglich die beiden freigegebenen allgemeinen Referenzskripte gelesen, nicht ausgeführt oder kopiert. Sie sind kein Nachweis für einen gehärteten Launcher.

## Implementierter Prototyp

- Ein allgemeines Windows-Programm; je Auftrag eine JSON-Beschreibung ohne Secret-Werte, keine projektspezifischen Übergabeskripte.
- Verdeckte lokale Eingabe mit Einfügeknopf und Strg+V; explizit **Starten** oder **Abbrechen**.
- Keine Secret-Werte im Chat, in Argumenten, Dateien, Logs, Telemetrie, globalen Umgebungsvariablen oder einem zusätzlichen Tresor.
- Neu aufgebauter Umgebungsblock, direkte Prozesserzeugung und kontrollierter Prozess-Lebenszyklus.
- Keine Weiterleitung von Ziel-stdout/stderr an Codex; Rückmeldung durch feste Statuskategorien und Exitcodes.
- Beim zentralen Skriptaufruf beendet der Launcher seinen Auftrag auch dann, wenn der überwachte Aufrufer hart beendet wird und dessen Bereinigung ausbleibt.

Technik: **C# mit WinForms auf .NET 10 LTS**, zunächst Windows 11 x64. Der gekapselte Win32-Prozessadapter ordnet das Ziel bereits bei der Erzeugung einem Windows Job Object zu. Der aktuelle Build verwendet das vorhandene SDK **10.0.203** unter `C:\Users\<Benutzer>\.dotnet` und benötigt eine vorhandene .NET-10-Desktop-Runtime. Release-Pakete enthalten die .NET-Laufzeit selbst; siehe [RELEASING.md](RELEASING.md). Quelle zur LTS-Einordnung: [Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Lokal ausprobieren

Die vorbereitete [Startdatei](artifacts/demo/Start-Synthetic-Demo.cmd) per Doppelklick öffnen. Im Fenster ausschließlich diese öffentlichen synthetischen Testwerte verwenden:

- `EXAMPLE_API_TOKEN`: `synthetic-demo-one`
- `SECOND_SECRET`: `synthetic-demo-two`

Aufruf prüfen, Werte lokal einfügen und **Starten** wählen. Erwartet: `COMPLETED`, Ziel-Exitcode 0. Andere Werte führen zu `TARGET_FAILED`, Ziel-Exitcode 49. Abbrechen vor Start führt zu keinem Prüfprozess. Die echte Windows-Zwischenablage und die physische Tastaturbedienung sind noch manuell zu prüfen; automatische Tests benutzen eine separate Test-Zwischenablage.

Nach Verschieben des Projektordners die Demo neu erzeugen, da ihre Auftragsdatei absolute Pfade enthält. Neu bauen und testen, aus PowerShell im Projektordner:

```powershell
.\scripts\Build-Prototype.ps1
```

Das Skript verwendet das Benutzer-SDK, baut ohne externe Pakete und stellt die Testversion unter `artifacts\demo` bereit. Es verändert weder den globalen PATH noch die PowerShell-Ausführungsrichtlinie. Keine Shell-Vorbereitung ist für das Öffnen der fertigen Demo nötig.

Der allgemeine Aufruf der EXE ist `SecretHandoff.exe --request <absoluter JSON-Pfad>`. Ein zentral verwendbares [Aufrufskript](scripts/Invoke-Handoff.ps1) wartet auf die GUI-EXE und liefert nur `HANDOFF_EXIT_<Code>` zurück. Beispiel mit einem beliebigen wertfreien Auftrag:

```powershell
.\scripts\Invoke-Handoff.ps1 -RequestPath 'D:\Projects\Example\task.handoff.json'
```

Das Skript bindet den Launcher automatisch an seinen eigenen Prozess (ID und Windows-Startzeit). Endet dieser Prozess, wird eine noch offene Freigabe verworfen oder der laufende Prozessbaum beendet. Die manuelle Demo und der direkte EXE-Aufruf ohne diese Bindung bleiben unabhängig vom Aufrufer. Die Bindung ist eine Lebenszyklussteuerung, kein Berechtigungsnachweis. Wird lediglich ein Codex-Auftrag beendet, während der gebundene Prozess weiterlebt, löst das allein keinen Abbruch aus; dieser konkrete Desktop-Ablauf bleibt zu prüfen.

Die JSON-Struktur und die bewusst eingeschränkte Zielumgebung stehen in [ARCHITECTURE.md](ARCHITECTURE.md). Ziel-stdin ist geschlossen bzw. liest aus `NUL`; interaktive CLI-Sitzungen werden nicht unterstützt.

Auftragsdatei, Programm und Arbeitsverzeichnis müssen auf einem verfügbaren lokalen Laufwerk liegen. Netzlaufwerke, SUBST-Aliase und Reparse Points in den Pfadkomponenten (etwa Junctions und symbolische Links) werden abgewiesen. Kann eine Sandbox die erforderlichen Windows-Abfragen nicht erlauben, wird der Start ebenfalls abgewiesen; es gibt keinen ungeprüften Ersatzstart. Die aktuellen Integrationstests wurden deshalb nach erfolgreichem Build außerhalb der Codex-Sandbox ausgeführt.

## Sicherheitsgrenze

Das Werkzeug soll versehentliche dauerhafte Ablage und Offenlegung im Chat vermeiden. Es isoliert Secrets **nicht gegen ausreichend berechtigte Prozesse desselben Windows-Benutzers**. Der genehmigte Zielcode kann Secrets lesen, ausgeben, speichern und übertragen. Ein angezeigter Name oder Pfad ist kein Identitäts- oder Berechtigungsnachweis. Prozessende widerruft keine Tokens; sofortige vollständige Speicherüberschreibung ist nicht garantiert. Zwischenablage, Betriebssystem-Dumps und Passwortmanager bleiben außerhalb der Kontrolle des Launchers.

## Noch offen

Noch ausstehend sind insbesondere die manuelle Bedienprüfung, der tatsächliche Abbruchknopf von Codex Desktop, die vollständige Prüfung aller MVP-Kriterien und die vollständige Paketabnahme auf einem sauberen Windows-System. Strg+C in einem Codex-Werkzeugprozess wurde am 27. Oktober separat geprüft; am 28. Oktober zusätzlich das harte Beenden des zentralen PowerShell-Aufrufers mit synthetischem Ziel und Enkel. Das belegt nicht automatisch das Verhalten anderer Abbruchwege. Nächste Schritte und Nachweisgrenzen: [TESTING.md](TESTING.md).

Lokale Arbeitsregeln können ergänzend in einer nicht veröffentlichten `AGENTS.md` stehen.
