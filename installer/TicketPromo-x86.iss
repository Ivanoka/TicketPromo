#define MyAppName "Ticket Promo"
#define MyAppVersion "2026.11.06"

#define PublishDir "..\bin\Release\net10.0-windows\win-x86\publish"
#define RuntimeInstaller "redist\windowsdesktop-runtime-10.0.14-win-x86.exe"

[Setup]
AppId={{A13F9D71-5E82-4C61-9D34-7F8A2B6C104E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
MinVersion=10.0.17763
UninstallDisplayName={#MyAppName}

DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename=TicketPromo-Setup-{#MyAppVersion}-x86

PrivilegesRequired=admin
ArchitecturesAllowed=x86

SetupIconFile=assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppName}.exe

WizardStyle=modern dynamic
ShowLanguageDialog=no
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "{#RuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "assets\config.json"; DestDir: "{commonappdata}\{#MyAppName}"; Flags: onlyifdoesntexist
Source: "assets\qr-code.png"; DestDir: "{commonappdata}\{#MyAppName}"; Flags: onlyifdoesntexist

[Dirs]
Name: "{commonappdata}\{#MyAppName}"; Permissions: users-modify

[Icons]
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppName}.exe"; WorkingDir: "{app}"

[Run]
Filename: "{tmp}\windowsdesktop-runtime-10.0.14-win-x86.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Установка .NET 10 Desktop Runtime..."; Flags: waituntilterminated
Filename: "{app}\{#MyAppName}.exe"; Description: "Запустить {#MyAppName}"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\{#MyAppName}"

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM ""Ticket Promo.exe"""; Flags: runhidden waituntilterminated