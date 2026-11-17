[Setup]
AppName=Windows Cleaner
AppVersion=1.0
CloseApplications=yes
RestartApplications=yes
DefaultDirName={autopf}\WindowsCleaner
DisableProgramGroupPage=yes
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\WindowsCleaner.exe
OutputDir=.
OutputBaseFilename=WindowsCleaner-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
WizardStyle=modern dark
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
Source: "bin\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Windows Cleaner"; Filename: "{app}\WindowsCleaner.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"