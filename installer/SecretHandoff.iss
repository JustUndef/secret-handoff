#ifndef AppVersion
  #error AppVersion must be supplied by Build-Release.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by Build-Release.ps1
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied by Build-Release.ps1
#endif

[Setup]
AppId={{7C962902-EE40-45C8-846C-BA02FD219454}
AppName=Secret Handoff
AppVersion={#AppVersion}
AppPublisher=JustUndef
AppPublisherURL=https://github.com/JustUndef/secret-handoff
DefaultDirName={localappdata}\Programs\Secret Handoff
DefaultGroupName=Secret Handoff
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
OutputDir={#ReleaseDir}
OutputBaseFilename=SecretHandoff-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\SecretHandoff.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=no
ChangesEnvironment=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.AddUserPath=Secret Handoff zum Benutzer-PATH hinzufügen (neue Terminals)
english.AddUserPath=Add Secret Handoff to the user PATH (new terminals)

[Tasks]
Name: "userpath"; Description: "{cm:AddUserPath}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Secret Handoff"; Filename: "{app}\SecretHandoff.exe"
Name: "{group}\Anleitung"; Filename: "{app}\README.md"

#include "UserPath.iss"
