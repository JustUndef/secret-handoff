# Installer-Prüfungen

## Umfang

`scripts/Test-Installer.ps1` prüft stille Installation, erneute Installation derselben Version, Start der installierten EXE und Deinstallation. Die PATH-Checkbox bleibt dabei bei einer neuen Installation ausgeschaltet.

`scripts/Test-InstallerPath.ps1` kompiliert denselben Installer mit einem ausschließlich zur Build-Zeit abweichenden Registry-Unterpfad. Alle PATH-Daten liegen unter einem neuen `HKCU\Software\SecretHandoff\PathTests\<Zufalls-ID>\Environment`; der echte Benutzer- und System-PATH wird weder ersetzt noch ausgegeben. Es gibt keinen Laufzeitschalter zum Umleiten der Registry im ausgelieferten Installer. Das Testpaket wird außerhalb der Release-Dateien erzeugt und nicht veröffentlicht. Die eigene temporäre Installation und der erzeugte Registry-Testbereich werden anschließend entfernt. Bei bereits vorhandener Installation von Secret Handoff verweigern die Installer-Tests den Start.

Die Tests prüfen den ausgeschalteten Standard, Hinzufügen, erneute Installation ohne Duplikat, Beibehaltung später ergänzter fremder Einträge, vorhandene nicht verwaltete Einträge mit abweichender Großschreibung/Anführungszeichen/abschließendem Backslash, ausdrückliches Abwählen, beide Registry-Zeichenkettentypen, ursprünglich fehlenden sowie leeren PATH. Variablenreferenzen und leere PATH-Segmente sind synthetische Testdaten und müssen unverändert bleiben.

## Reproduktion

```powershell
.\scripts\Test-Installer.ps1 -InstallerPath '<Release-Ordner>\SecretHandoff-<Version>-win-x64-Setup.exe'
.\scripts\Test-InstallerPath.ps1 -PublishDirectory '<Build-Basisordner>\app'
```

Voraussetzung ist vorhandenes Inno Setup 6; alternativ dessen Compiler mit `-IsccPath` angeben. Beide Prüfungen sind im Release-Workflow vor dem Artefakt-Upload eingebunden.

## Nachweisstand

Am 28. Oktober 2026 unter Windows lokal ausgeführt: Inno-Kompilierung erfolgreich, alle sechs synthetischen PATH-Testgruppen mit `INSTALLER_PATH_TESTS_PASSED` bestanden. Dabei wurde der letzte zuvor geprüfte veröffentlichungsfähige Programm-Build verwendet; die parallel in Entwicklung befindliche CLI-Erweiterung wurde weder gebaut noch verändert. Ein erster Testlauf zeigte, dass der Test auch den temporären Deinstallations-Hilfsprozess abwarten muss. Die Tests warten nun dessen Prozessbaum ab. Besitzmetadaten werden in einem eigenen Installer-Registry-Schlüssel gehalten, damit sie eine erneute Installation überstehen.

Auch der normale lokale Installer-Rundlauf bestand anschließend. Der [GitHub-Testlauf für Commit 792b863](https://github.com/JustUndef/secret-handoff/actions/runs/40716000388) bestand vollständig: bestehende synthetische Integrationstests, Release-Paketbau, Paketprüfung, normaler Installer-Rundlauf, isolierte PATH-Testserie und Artefakt-Upload. Die Veröffentlichung wurde beim manuellen Test wie vorgesehen übersprungen; kein Versions-Tag oder Release wurde angelegt.

Manuelle Checkbox-Bedienung und Übernahme der Änderung durch verschiedene Terminal-/Codex-Startwege sind gesonderte Prüfungen. Eine bereits laufende Anwendung bekommt durch den Installer keinen neuen Prozess-Umgebungsblock. Gleichzeitige unabhängige Schreibzugriffe anderer Programme auf denselben Registry-PATH sind nicht transaktional koordiniert.
