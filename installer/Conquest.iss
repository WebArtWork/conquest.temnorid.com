; Inno Setup script for the Windows installer: Temnorid-Conquest-Setup.exe
; Build the player first (Unity: Temnorid > Conquest > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Conquest.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Conquest"
#define AppVersion "0.1.0"
#define AppExe "TemnoridConquest.exe"

[Setup]
AppId={{4C7A9E10-5B2D-4D86-8F31-A0E6D2B94C55}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://conquest.temnorid.com
DefaultDirName={autopf}\Temnorid Conquest
DefaultGroupName=Temnorid Conquest
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Conquest-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridConquest\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent
