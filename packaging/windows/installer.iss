; Inno Setup Script for Fdb Studio
; Generates Windows Installer executable (Fdb-Studio-Setup-win-x64.exe)

#define MyAppName "Fdb Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CipherUnits"
#define MyAppURL "https://github.com/cipherunits/Fdb"
#define MyAppExeName "Fdb.exe"
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist\windows"
#endif

[Setup]
AppId={{E6F7A8D3-89B4-4D6C-BF55-08422A943F21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=Fdb-Studio-Setup-win-x64
OutputDir={#OutputDir}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\..\Assets\fdb_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
