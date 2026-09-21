#ifndef AppVersion
 #define AppVersion "0.3.3"
#endif
[Setup]
AppId={{D83BFF6F-65F3-4FA7-8C88-1AC1E3CE2C51}
AppName=FloatTodo
AppVersion={#AppVersion}
AppPublisher=FloatTodo
DefaultDirName={autopf}\FloatTodo
DefaultGroupName=FloatTodo
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=..\artifacts\installer
OutputBaseFilename=FloatTodo-Setup-{#AppVersion}
SetupIconFile=..\src\FloatTodo.WinUI\Assets\app.ico
UninstallDisplayIcon={app}\FloatTodo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=FloatTodo.exe
RestartApplications=no
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start FloatTodo when I sign in"; Flags: unchecked; Check: not IsAdminInstallMode

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FloatTodo"; Filename: "{app}\FloatTodo.exe"
Name: "{autodesktop}\FloatTodo"; Filename: "{app}\FloatTodo.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FloatTodo"; ValueData: """{app}\FloatTodo.exe"""; Tasks: startup; Flags: uninsdeletevalue; Check: not IsAdminInstallMode

[Run]
Filename: "{app}\FloatTodo.exe"; Description: "Open FloatTodo"; Flags: nowait postinstall skipifsilent runasoriginaluser

; User data under LOCALAPPDATA\FloatTodo is deliberately excluded from uninstall.

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FloatTodo', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\FloatTodo.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FloatTodo');
end;
