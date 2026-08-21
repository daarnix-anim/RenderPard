[Setup]
; App Information
AppName=RenderPard
AppVersion=1.6.1
AppPublisher=RenderPard
AppPublisherURL=https://anipard.ru
AppSupportURL=https://anipard.ru
AppUpdatesURL=https://anipard.ru

; Default installation directory
DefaultDirName={autopf}\RenderPard

; Start Menu folder name
DefaultGroupName=RenderPard

; Force close applications during silent update
CloseApplications=force

; Output settings
OutputDir=Output
OutputBaseFilename=RenderPard_Setup_v1.6.1

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Architecture (64-bit app)
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Include all published files
Source: "PublishOutput\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu Icon
Name: "{group}\RenderPard"; Filename: "{app}\RenderPard.UI.exe"
; Desktop Icon
Name: "{autodesktop}\RenderPard"; Filename: "{app}\RenderPard.UI.exe"; Tasks: desktopicon
; Uninstall Icon
Name: "{group}\{cm:UninstallProgram,RenderPard}"; Filename: "{uninstallexe}"

[Run]
; Option to launch after setup
Filename: "{app}\RenderPard.UI.exe"; Description: "{cm:LaunchProgram,RenderPard}"; Flags: nowait postinstall skipifsilent
