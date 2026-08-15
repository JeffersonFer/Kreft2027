[Setup]
AppId={{21958C75-22E1-4A2E-814C-D7CE06FA2978}
AppName=KreftBimTools
AppVersion=1.0.0
AppPublisher=Jefferson Fernando Santana
DefaultDirName={autopf}\Kreft\BimTools
DisableProgramGroupPage=yes
OutputDir=..\..\dist\Installer
OutputBaseFilename=KreftBimTools-Setup-1.0.0
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\..\resources\icons\kreftAddinIcon.ico
UninstallDisplayIcon={app}\kreftAddinIcon.ico
WizardImageFile=..\..\resources\icons\KreftBimToolsBanner.bmp

[Files]
; Revit 2025 - DLLs (ProgramData)
Source: "..\KreftBimTools.Revit\bin\x64\Release R25\net8.0-windows\*"; DestDir: "{commonappdata}\Kreft\BimTools\DLLs\2025"; Flags: recursesubdirs ignoreversion
; Revit 2026 - DLLs (ProgramData)
Source: "..\KreftBimTools.Revit\bin\x64\Release R26\net8.0-windows\*"; DestDir: "{commonappdata}\Kreft\BimTools\DLLs\2026"; Flags: recursesubdirs ignoreversion
; Revit 2027 - DLLs (ProgramData - local das DLLs não muda, só o manifesto)
Source: "..\KreftBimTools.Revit\bin\x64\Release R27\net10.0-windows\*"; DestDir: "{commonappdata}\Kreft\BimTools\DLLs\2027"; Flags: recursesubdirs ignoreversion
; Manifesto .addin - 2025 (ProgramData)
Source: "..\..\resources\addins\KreftBimTools.2025.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
; Manifesto .addin - 2026 (ProgramData)
Source: "..\..\resources\addins\KreftBimTools.2026.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
; Manifesto .addin - 2027 (Program Files - mudança de segurança do Revit 2027)
Source: "..\..\resources\addins\KreftBimTools.2027.addin"; DestDir: "{commonpf}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion
; Ícone (usado na desinstalação)
Source: "..\..\resources\icons\kreftAddinIcon.ico"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2025"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2026"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2027"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools"

[UninstallDelete]
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2025"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2026"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs\2027"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools\DLLs"
Type: dirifempty; Name: "{commonappdata}\Kreft\BimTools"
