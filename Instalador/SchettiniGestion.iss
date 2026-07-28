; Inno Setup — SCHPOS (testing / entrega cliente)
; Requiere Inno Setup 6: https://jrsoftware.org/isdl.php

#define MyAppName "SCHPOS"
#define MyAppVersion "2.2.3"
#define MyAppPublisher "Schettini Tec"
#define MyAppExeName "SCHPOS.exe"
#define MyAppUrl "https://github.com/Sartorifranco/SchettiniGestion"

#if FileExists(AddBackslash(SourcePath) + "prerequisites\ndp48-x86-x64-allos-enu.exe")
  #define Ndp48Bundled
#endif
#if FileExists(AddBackslash(SourcePath) + "prerequisites\SqlLocalDB.msi")
  #define LocalDbBundled
#endif

[Setup]
AppId={{A7B3C9D1-4E2F-5A6B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=Output
OutputBaseFilename=SCHPOS-Setup-{#MyAppVersion}
SetupIconFile=..\SchettiniGestion.WPF\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
; Generado por build-release.ps1 en la carpeta staging\
Source: "staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef Ndp48Bundled
Source: "prerequisites\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not Net48Installed
#endif
#ifdef LocalDbBundled
Source: "prerequisites\SqlLocalDB.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not LocalDbInstalled
#endif

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
#ifdef Ndp48Bundled
Filename: "{tmp}\ndp48-x86-x64-allos-enu.exe"; Parameters: "/q /norestart"; StatusMsg: "Instalando .NET Framework 4.8..."; Flags: waituntilterminated; Check: not Net48Installed
#endif
#ifdef LocalDbBundled
Filename: "msiexec.exe"; Parameters: "/i ""{tmp}\SqlLocalDB.msi"" /passive IACCEPTSQLLOCALDBLICENSETERMS=YES /norestart"; StatusMsg: "Instalando SQL Server LocalDB..."; Flags: waituntilterminated; Check: not LocalDbInstalled
#endif
Filename: "{app}\{#MyAppExeName}"; Parameters: "/bootstrap"; StatusMsg: "Preparando base de datos..."; Flags: waituntilterminated runhidden; Check: ShouldRunBootstrap
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName} ahora"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function Net48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 528040);
end;

function Net472OrHigherInstalled: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 461808);
end;

function LocalDbInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\cmd.exe'), '/c sqllocaldb info MSSQLLocalDB >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

function ShouldRunBootstrap: Boolean;
begin
  Result := Net472OrHigherInstalled;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if Net48Installed or Net472OrHigherInstalled then
    Exit;
#ifdef Ndp48Bundled
  Exit;
#endif
  if MsgBox('No se detecto .NET Framework 4.7.2 o superior.' + #13#10 +
    'Instale .NET Framework 4.8 desde Microsoft y vuelva a ejecutar el instalador.' + #13#10#13#10 +
    'Desea continuar de todos modos?', mbConfirmation, MB_YESNO) = IDNO then
    Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CfgDir, CfgPath, CfgContent, LicSrc, LicDst: String;
begin
  if CurStep = ssPostInstall then
  begin
    CfgDir := ExpandConstant('{commonappdata}\SCHPOS');
    CfgPath := CfgDir + '\conexion.cfg';
    CfgContent := 'Server=(LocalDB)\MSSQLLocalDB;Database=SchPosDB;Integrated Security=True;Encrypt=False;';
    if not DirExists(CfgDir) then
      CreateDir(CfgDir);
    if not FileExists(CfgPath) then
      SaveStringToFile(CfgPath, CfgContent, False);

    LicSrc := ExpandConstant('{app}\licencia.key');
    LicDst := CfgDir + '\licencia.key';
    if FileExists(LicSrc) and not FileExists(LicDst) then
      FileCopy(LicSrc, LicDst, False);
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
end;
