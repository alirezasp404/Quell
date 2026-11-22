; Inno Setup Script for Quell
; Generates Windows Installer executable (Quell-Setup-win-x64.exe)

#define MyAppName "Quell"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "CipherUnits"
#define MyAppURL "https://github.com/cipherunits/Quell"
#define MyAppExeName "Quell.exe"
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist\windows"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "Quell-Setup-win-x64"
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
OutputBaseFilename={#OutputBaseFilename}
OutputDir={#OutputDir}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\..\Assets\quell_icon.ico
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
