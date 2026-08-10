[Setup]
AppName=Perma Notes
AppVersion=1.2.0
AppPublisher=Antigravity
AppPublisherURL=https://github.com
DefaultDirName={localappdata}\Programs\Perma Notes
DefaultGroupName=Perma Notes
DisableProgramGroupPage=yes
; Require lowest privileges so the installer doesn't need Admin rights
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=PermaNotes_Setup
SetupIconFile=Assets\icon.ico
UninstallDisplayIcon={app}\PermaNotes.exe
AppMutex=DesktopNotes_SingleInstance_F7A2B
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Automatically start Perma Notes when Windows starts"; GroupDescription: "System Integration"

[Files]
Source: "dist\PermaNotes.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Perma Notes"; Filename: "{app}\PermaNotes.exe"
Name: "{autodesktop}\Perma Notes"; Filename: "{app}\PermaNotes.exe"; Tasks: desktopicon
; Add to Startup folder if the user checks the task
Name: "{userstartup}\Perma Notes"; Filename: "{app}\PermaNotes.exe"; Tasks: startup

[Run]
Filename: "{app}\PermaNotes.exe"; Description: "{cm:LaunchProgram,Perma Notes}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /f /im PermaNotes.exe"; Flags: runhidden
