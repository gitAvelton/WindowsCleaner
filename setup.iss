[Setup]
AppName=Windows Cleaner
AppVersion=1.0
DefaultDirName={autopf}\WindowsCleaner
DefaultGroupName=Windows Cleaner
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\WindowsCleaner.exe
OutputDir=.
OutputBaseFilename=WindowsCleaner-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest

[Files]
Source: "bin\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Windows Cleaner"; Filename: "{app}\WindowsCleaner.exe"
Name: "{autodesktop}\Windows Cleaner"; Filename: "{app}\WindowsCleaner.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Verknüpfung auf dem Desktop erstellen"; GroupDescription: "Zusätzliche Optionen:"