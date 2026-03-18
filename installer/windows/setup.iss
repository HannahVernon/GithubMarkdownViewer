; Inno Setup Script for GitHub Markdown Viewer
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)
;
; Usage:
;   1. Run installer\publish.ps1 to build the win-x64 binaries
;   2. Open this file in Inno Setup Compiler and click Build
;   Or from command line: iscc installer\windows\setup.iss

#define MyAppName "GitHub Markdown Viewer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Hannah Vernon"
#define MyAppURL "https://github.com/HannahVernon/GithubMarkdownViewer"
#define MyAppExeName "GithubMarkdownViewer.exe"
#define PublishDir "..\..\installer\publish\win-x64"

[Setup]
AppId={{B8F3E2A1-7D4C-4E5F-9A1B-3C6D8E9F0A2B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile=..\..\LICENSE
OutputDir=..\..\installer\output
OutputBaseFilename=GithubMarkdownViewer-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\..\GithubMarkdownViewer\Assets\app-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Associate .md files with {#MyAppName}"; GroupDescription: "File associations:"

[Files]
; Include all files from the publish output
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Include LICENSE
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File association for .md files (only if the user selected the task)
Root: HKCU; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "GithubMarkdownViewer.md"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\GithubMarkdownViewer.md"; ValueType: string; ValueName: ""; ValueData: "Markdown Document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\GithubMarkdownViewer.md\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\GithubMarkdownViewer.md\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Notify shell of association changes after install
  if CurStep = ssPostInstall then
  begin
    // SHChangeNotify is called automatically by Inno Setup when ChangesAssociations=yes
  end;
end;
