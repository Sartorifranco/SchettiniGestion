; Inno Setup — SCHPOS
; Requiere Inno Setup 6: https://jrsoftware.org/isdl.php
; Guardar este archivo en UTF-8.

#define MyAppName "SCHPOS"
#define MyAppVersion "2.3.56"
#define MyAppPublisher "SCHPOS"
#define MyAppExeName "SCHPOS.exe"
#define MyAppUrl "https://github.com/Sartorifranco/SchettiniGestion"

#if FileExists(AddBackslash(SourcePath) + "prerequisites\ndp48-x86-x64-allos-enu.exe")
  #define Ndp48Bundled
#endif
#if FileExists(AddBackslash(SourcePath) + "prerequisites\SqlLocalDB.msi")
  #define LocalDbBundled
#endif
#if FileExists(AddBackslash(SourcePath) + "prerequisites\vc_redist.x64.exe")
  #define VcRedistX64Bundled
#endif
#if FileExists(AddBackslash(SourcePath) + "prerequisites\vc_redist.x86.exe")
  #define VcRedistX86Bundled
#endif

[Setup]
AppId={{A7B3C9D1-4E2F-5A6B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppCopyright=© {#MyAppPublisher}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription=Instalador SCHPOS — gestión comercial y punto de venta
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile=
OutputDir=Output
OutputBaseFilename=SCHPOS-Setup-{#MyAppVersion}
SetupIconFile=..\SchettiniGestion.WPF\Resources\app.ico
WizardImageFile=graphics\wizard-left.png
WizardSmallImageFile=graphics\wizard-small.png
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
WizardImageStretch=yes
WizardImageBackColor=$1E1E1E
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
SetupLogging=yes
CloseApplications=yes
RestartIfNeededByRun=no
AllowNoIcons=yes
UsePreviousAppDir=yes
ShowLanguageDialog=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
SetupAppTitle=Instalar SCHPOS
SetupWindowTitle=Instalar SCHPOS
WelcomeLabel1=Bienvenido a SCHPOS
WelcomeLabel2=Este asistente instala SCHPOS, el sistema de gestión comercial y punto de venta.%n%nEn el paso siguiente va a indicar si esta PC es el SERVIDOR (donde viven los datos) o un CLIENTE (otra caja de la red).%n%nCierre SCHPOS si ya está abierto e inicie sesión como administrador.
ClickNext=Haga clic en Siguiente para continuar.
ReadyLabel1=Todo listo para instalar SCHPOS en esta PC.
InstallingLabel=Instalando SCHPOS. Esto puede tardar unos minutos si hay que instalar componentes de Windows…
FinishedHeadingLabel=SCHPOS quedó instalado
FinishedLabel=La instalación finalizó correctamente. Ya puede usar SCHPOS en esta PC.
ClickFinish=Haga clic en Finalizar para cerrar el instalador.
SelectTasksDesc=Elija las tareas adicionales:
ConfirmUninstall=¿Quitar SCHPOS de esta PC?%n%nLos datos de la base no se borran solos. Si esta PC era el servidor, haga un backup antes.

[CustomMessages]
SchposTipoTitulo=Tipo de instalación
SchposTipoSub=¿Qué va a ser esta PC?
SchposTipoIntro=SCHPOS puede instalarse de dos maneras. Conviene acertar ahora: el servidor guarda los datos y los clientes se conectan a él por la red.%n%nDespués se puede cambiar en Configuración → Red y Servidor.
SchposTipoServidor=SERVIDOR — Esta PC guarda la base de datos. Las otras cajas se conectan acá. Es la primera (o la principal) de un local.
SchposTipoCliente=CLIENTE — Esta PC es un puesto de venta. No instala el motor SQL: se conecta al servidor. Necesita la IP y la clave schpos de esa PC.
SchposMemoTipo=Tipo de esta PC:
SchposFinServidor=SCHPOS quedó instalado como SERVIDOR.%n%nAl abrir el sistema, active la licencia. Si va a usar varias cajas, en Configuración → Red y Servidor use «Preparar como SERVIDOR» para abrir el puerto 1433 y generar el archivo de clientes.%n%nDeje esta PC encendida cuando las otras cajas estén trabajando.
SchposFinCliente=SCHPOS quedó instalado como CLIENTE.%n%nAl abrir el sistema va a pedir los datos del servidor:%n  • IP (ejemplo: 192.168.18.115\SQLEXPRESS)%n  • Puerto 1433%n  • Usuario schpos y la contraseña del archivo del servidor.%n%nNo hace falta instalar SQL Express en esta PC.
SchposPuestoTitulo=Nombre de este puesto
SchposPuestoSub=¿Cómo se va a llamar esta caja?
SchposPuestoIntro=Cada PC tiene un nombre propio (CAJA-01, CAJA-02, MOSTRADOR, SERVIDOR). Aparece en el encabezado y sirve para distinguir las cajas. Después se puede cambiar en Configuración → Red y Servidor.
SchposPuestoNombre=Nombre del puesto:
SchposPuestoVacio=Escribí un nombre para este puesto. Ejemplos: CAJA-01, MOSTRADOR, SERVIDOR.
SchposMemoPuesto=Nombre de este puesto:

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
#ifdef Ndp48Bundled
Source: "prerequisites\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not Net48Installed
#endif
#ifdef VcRedistX64Bundled
Source: "prerequisites\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not VcRedistX64Installed
#endif
#ifdef VcRedistX86Bundled
Source: "prerequisites\vc_redist.x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not VcRedistX86Installed
#endif
#ifdef LocalDbBundled
Source: "prerequisites\SqlLocalDB.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: ShouldInstallLocalDb
#endif

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Comment: "SCHPOS — gestión comercial"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon; Comment: "SCHPOS — gestión comercial"

[Run]
#ifdef Ndp48Bundled
Filename: "{tmp}\ndp48-x86-x64-allos-enu.exe"; Parameters: "/q /norestart"; StatusMsg: "Instalando .NET Framework 4.8…"; Flags: waituntilterminated; Check: not Net48Installed
#endif
#ifdef VcRedistX64Bundled
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Instalando Visual C++ Redistributable (x64)…"; Flags: waituntilterminated; Check: not VcRedistX64Installed
#endif
#ifdef VcRedistX86Bundled
Filename: "{tmp}\vc_redist.x86.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Instalando Visual C++ Redistributable (x86)…"; Flags: waituntilterminated; Check: not VcRedistX86Installed
#endif
#ifdef LocalDbBundled
Filename: "msiexec.exe"; Parameters: "/i ""{tmp}\SqlLocalDB.msi"" /passive IACCEPTSQLLOCALDBLICENSETERMS=YES /norestart"; StatusMsg: "Instalando SQL Server LocalDB (solo servidor)…"; Flags: waituntilterminated; Check: ShouldInstallLocalDb
#endif
Filename: "{app}\{#MyAppExeName}"; Parameters: "/bootstrap"; StatusMsg: "Preparando la base de datos local…"; Flags: waituntilterminated runhidden; Check: ShouldRunBootstrap
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar SCHPOS ahora"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  PaginaModo: TInputOptionWizardPage;
  PaginaPuesto: TInputQueryWizardPage;
  NombrePuestoPrellenado: Boolean;
  FondosOscurosCreados: Boolean;

const
  ColorFondo = $1E1E1E;
  ColorPanel = $2D2D2D;
  ColorTexto = $EEEEEE;
  ColorSecundario = $B0B0B0;

function DwmSetWindowAttribute(hwnd: HWND; dwAttribute: DWORD; var pvAttribute: DWORD; cbAttribute: DWORD): HRESULT;
  external 'DwmSetWindowAttribute@dwmapi.dll stdcall delayload';

function ShowScrollBar(hWnd: HWND; wBar: Integer; bShow: BOOL): BOOL;
  external 'ShowScrollBar@user32.dll stdcall';

procedure SHChangeNotify(wEventId: Longint; uFlags: DWORD; dwItem1, dwItem2: Integer);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure OscurecerBarraTitulo;
var
  Valor: DWORD;
begin
  Valor := 1;
  DwmSetWindowAttribute(WizardForm.Handle, 20, Valor, 4);
  DwmSetWindowAttribute(WizardForm.Handle, 19, Valor, 4);
end;

procedure AcomodarEtiqueta(Lbl: TNewStaticText; Izq, Ancho: Integer);
begin
  if Lbl = nil then
    Exit;
  Lbl.Left := Izq;
  if Ancho > ScaleX(80) then
    Lbl.Width := Ancho;
end;

procedure RestaurarBannerIzquierdo;
var
  Ancho, Alto, PaginaAncho, TextoIzq, TextoAncho, ImgW, ImgH: Integer;
begin
  { El bitmap del asistente de Inno es 164x314. El control alLeft llena la altura:
    el ancho DEBE seguir esa misma proporción o Stretch deforma el logo. }
  Alto := WizardForm.WelcomePage.ClientHeight;
  if Alto < ScaleY(200) then
    Alto := WizardForm.ClientHeight;
  ImgW := 164;
  ImgH := 314;
  if (WizardForm.WizardBitmapImage.Bitmap <> nil) and (WizardForm.WizardBitmapImage.Bitmap.Height > 0) then
  begin
    ImgW := WizardForm.WizardBitmapImage.Bitmap.Width;
    ImgH := WizardForm.WizardBitmapImage.Bitmap.Height;
  end;
  Ancho := (Alto * ImgW) div ImgH;

  WizardForm.WizardBitmapImage.AutoSize := False;
  WizardForm.WizardBitmapImage.Stretch := True;
  WizardForm.WizardBitmapImage.Align := alLeft;
  WizardForm.WizardBitmapImage.Width := Ancho;
  WizardForm.WizardBitmapImage.Visible := True;
  WizardForm.WizardBitmapImage.BackColor := ColorFondo;
  WizardForm.WizardBitmapImage.BringToFront;

  PaginaAncho := WizardForm.WelcomePage.ClientWidth;
  TextoIzq := Ancho + ScaleX(20);
  TextoAncho := PaginaAncho - TextoIzq - ScaleX(16);
  AcomodarEtiqueta(WizardForm.WelcomeLabel1, TextoIzq, TextoAncho);
  AcomodarEtiqueta(WizardForm.WelcomeLabel2, TextoIzq, TextoAncho);
  AcomodarEtiqueta(WizardForm.FinishedHeadingLabel, TextoIzq, TextoAncho);
  AcomodarEtiqueta(WizardForm.FinishedLabel, TextoIzq, TextoAncho);
end;

procedure CubrirZonaTexto(Pagina: TWinControl; Imagen: TBitmapImage);
var
  Fondo: TPanel;
  Izq: Integer;
begin
  if Pagina = nil then
    Exit;
  Izq := 0;
  if (Imagen <> nil) and Imagen.Visible then
    Izq := Imagen.Left + Imagen.Width;
  Fondo := TPanel.Create(WizardForm);
  Fondo.Parent := Pagina;
  Fondo.Left := Izq;
  Fondo.Top := 0;
  Fondo.Width := Pagina.ClientWidth - Izq;
  Fondo.Height := Pagina.ClientHeight;
  Fondo.Anchors := [akLeft, akTop, akRight, akBottom];
  Fondo.BevelOuter := bvNone;
  Fondo.BevelInner := bvNone;
  Fondo.ParentBackground := False;
  Fondo.Color := ColorFondo;
  Fondo.SendToBack;
  if Imagen <> nil then
    Imagen.BringToFront;
end;

procedure CubrirConFondo(Contenedor: TWinControl);
var
  Fondo: TPanel;
begin
  if Contenedor = nil then
    Exit;
  Fondo := TPanel.Create(WizardForm);
  Fondo.Parent := Contenedor;
  Fondo.Align := alClient;
  Fondo.BevelOuter := bvNone;
  Fondo.BevelInner := bvNone;
  Fondo.ParentBackground := False;
  Fondo.Color := ColorFondo;
  Fondo.SendToBack;
end;

procedure CrearFondosOscuros;
var
  FondoForm: TPanel;
begin
  if FondosOscurosCreados then
    Exit;

  RestaurarBannerIzquierdo;

  FondoForm := TPanel.Create(WizardForm);
  FondoForm.Parent := WizardForm;
  FondoForm.Left := 0;
  FondoForm.Top := 0;
  FondoForm.Width := WizardForm.ClientWidth;
  FondoForm.Height := WizardForm.ClientHeight;
  FondoForm.Anchors := [akLeft, akTop, akRight, akBottom];
  FondoForm.BevelOuter := bvNone;
  FondoForm.BevelInner := bvNone;
  FondoForm.ParentBackground := False;
  FondoForm.Color := ColorFondo;
  FondoForm.SendToBack;

  CubrirZonaTexto(WizardForm.WelcomePage, WizardForm.WizardBitmapImage);
  CubrirZonaTexto(WizardForm.FinishedPage, WizardForm.WizardBitmapImage);
  if PaginaModo <> nil then
    CubrirConFondo(PaginaModo.Surface);
  if PaginaPuesto <> nil then
    CubrirConFondo(PaginaPuesto.Surface);

  RestaurarBannerIzquierdo;
  FondosOscurosCreados := True;
end;

procedure OcultarBarrasMemo(Memo: TNewMemo);
begin
  if Memo = nil then
    Exit;
  Memo.ScrollBars := ssNone;
  Memo.WordWrap := True;
  Memo.BorderStyle := bsNone;
  Memo.Color := ColorPanel;
  Memo.Font.Color := ColorTexto;
  if Memo.Handle <> 0 then
    ShowScrollBar(Memo.Handle, 3, False);
end;

procedure PintarControles(Control: TWinControl);
var
  I: Integer;
  Hijo: TControl;
begin
  for I := 0 to Control.ControlCount - 1 do
  begin
    Hijo := Control.Controls[I];
    if (Hijo is TNewButton) or (Hijo is TButton) or (Hijo is TBitmapImage) then
      Continue;

    if Hijo is TNewStaticText then
    begin
      TNewStaticText(Hijo).Color := ColorFondo;
      TNewStaticText(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TNewCheckListBox then
    begin
      TNewCheckListBox(Hijo).Color := ColorPanel;
      TNewCheckListBox(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TNewEdit then
    begin
      TNewEdit(Hijo).Color := ColorPanel;
      TNewEdit(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TPasswordEdit then
    begin
      TPasswordEdit(Hijo).Color := ColorPanel;
      TPasswordEdit(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TNewMemo then
      OcultarBarrasMemo(TNewMemo(Hijo))
    else if Hijo is TNewRadioButton then
    begin
      TNewRadioButton(Hijo).Color := ColorFondo;
      TNewRadioButton(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TNewCheckBox then
    begin
      TNewCheckBox(Hijo).Color := ColorFondo;
      TNewCheckBox(Hijo).Font.Color := ColorTexto;
    end
    else if Hijo is TPanel then
    begin
      TPanel(Hijo).ParentBackground := False;
      TPanel(Hijo).Color := ColorFondo;
    end;

    if Hijo is TWinControl then
      PintarControles(TWinControl(Hijo));
  end;
end;

procedure AplicarEstiloSchpos;
begin
  WizardForm.Color := ColorFondo;
  WizardForm.Font.Color := ColorTexto;
  WizardForm.MainPanel.ParentBackground := False;
  WizardForm.MainPanel.Color := ColorFondo;
  WizardForm.InnerPage.Color := ColorFondo;

  WizardForm.PageNameLabel.Font.Color := ColorTexto;
  WizardForm.PageDescriptionLabel.Font.Color := ColorSecundario;
  WizardForm.WelcomeLabel1.Font.Color := ColorTexto;
  WizardForm.WelcomeLabel2.Font.Color := ColorTexto;

  CrearFondosOscuros;
  OscurecerBarraTitulo;
  PintarControles(WizardForm);
  RestaurarBannerIzquierdo;
  OcultarBarrasMemo(WizardForm.ReadyMemo);
end;

function CmdLineTiene(const Valor: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Valor) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function EsModoServidor: Boolean;
begin
  if PaginaModo = nil then
    Result := not CmdLineTiene('/CLIENTE')
  else
    Result := PaginaModo.Values[0];
end;

function EsModoCliente: Boolean;
begin
  Result := not EsModoServidor;
end;

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

function ShouldInstallLocalDb: Boolean;
begin
  Result := EsModoServidor and (not LocalDbInstalled);
end;

function VcRedistX64Installed: Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'Installed', Installed)
    and (Installed = 1);
end;

function VcRedistX86Installed: Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X86', 'Installed', Installed)
    and (Installed = 1);
end;

function ShouldRunBootstrap: Boolean;
begin
  Result := EsModoServidor and Net472OrHigherInstalled;
end;

procedure PrecargarModoDesdeDisco;
var
  Ruta: String;
  Contenido: AnsiString;
begin
  if PaginaModo = nil then
    Exit;
  Ruta := ExpandConstant('{commonappdata}\SCHPOS\modo_red.cfg');
  if not FileExists(Ruta) then
    Exit;
  if not LoadStringFromFile(Ruta, Contenido) then
    Exit;
  if Pos('Cliente', Contenido) > 0 then
  begin
    PaginaModo.Values[0] := False;
    PaginaModo.Values[1] := True;
  end;
end;

procedure PrecargarNombrePuestoDesdeDisco;
var
  Ruta: String;
  Contenido: AnsiString;
  Nombre: String;
begin
  if PaginaPuesto = nil then
    Exit;
  Ruta := ExpandConstant('{commonappdata}\SCHPOS\puesto.cfg');
  if FileExists(Ruta) and LoadStringFromFile(Ruta, Contenido) then
  begin
    Nombre := Trim(Contenido);
    if Nombre <> '' then
    begin
      PaginaPuesto.Values[0] := Nombre;
      NombrePuestoPrellenado := True;
    end;
  end;
end;

procedure InitializeWizard;
begin
  PaginaModo := CreateInputOptionPage(wpWelcome,
    ExpandConstant('{cm:SchposTipoTitulo}'),
    ExpandConstant('{cm:SchposTipoSub}'),
    ExpandConstant('{cm:SchposTipoIntro}'),
    True, False);
  PaginaModo.Add(ExpandConstant('{cm:SchposTipoServidor}'));
  PaginaModo.Add(ExpandConstant('{cm:SchposTipoCliente}'));
  PaginaModo.Values[0] := True;

  if CmdLineTiene('/CLIENTE') then
  begin
    PaginaModo.Values[0] := False;
    PaginaModo.Values[1] := True;
  end
  else if CmdLineTiene('/SERVIDOR') then
  begin
    PaginaModo.Values[0] := True;
    PaginaModo.Values[1] := False;
  end
  else
    PrecargarModoDesdeDisco;

  PaginaPuesto := CreateInputQueryPage(PaginaModo.ID,
    ExpandConstant('{cm:SchposPuestoTitulo}'),
    ExpandConstant('{cm:SchposPuestoSub}'),
    ExpandConstant('{cm:SchposPuestoIntro}'));
  PaginaPuesto.Add(ExpandConstant('{cm:SchposPuestoNombre}'), False);
  PrecargarNombrePuestoDesdeDisco;
  AplicarEstiloSchpos;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (PaginaPuesto <> nil) and (CurPageID = PaginaPuesto.ID) then
  begin
    if Trim(PaginaPuesto.Values[0]) = '' then
    begin
      MsgBox(ExpandConstant('{cm:SchposPuestoVacio}'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Tipo: String;
begin
  if EsModoCliente then
    Tipo := 'CLIENTE (puesto de red)'
  else
    Tipo := 'SERVIDOR (base de datos en esta PC)';

  Result := ExpandConstant('{cm:SchposMemoTipo}') + ' ' + Tipo + NewLine;
  if (PaginaPuesto <> nil) and (Trim(PaginaPuesto.Values[0]) <> '') then
    Result := Result + ExpandConstant('{cm:SchposMemoPuesto}') + ' ' + Trim(PaginaPuesto.Values[0]) + NewLine;
  Result := Result + NewLine;
  if MemoDirInfo <> '' then
    Result := Result + MemoDirInfo + NewLine + NewLine;
  if MemoGroupInfo <> '' then
    Result := Result + MemoGroupInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then
    Result := Result + MemoTasksInfo + NewLine;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (PaginaPuesto <> nil) and (CurPageID = PaginaPuesto.ID) and (not NombrePuestoPrellenado) then
  begin
    if Trim(PaginaPuesto.Values[0]) = '' then
    begin
      if EsModoCliente then
        PaginaPuesto.Values[0] := 'CAJA-01'
      else
        PaginaPuesto.Values[0] := 'SERVIDOR';
    end;
    NombrePuestoPrellenado := True;
  end;
  if CurPageID = wpFinished then
  begin
    if EsModoCliente then
      WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:SchposFinCliente}')
    else
      WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:SchposFinServidor}');
    RestaurarBannerIzquierdo;
  end;
  AplicarEstiloSchpos;
  if (CurPageID = wpWelcome) or (CurPageID = wpFinished) then
    RestaurarBannerIzquierdo;
  if CurPageID = wpReady then
    OcultarBarrasMemo(WizardForm.ReadyMemo);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if Net48Installed or Net472OrHigherInstalled then
    Exit;
#ifdef Ndp48Bundled
  Exit;
#endif
  if MsgBox('No se detectó .NET Framework 4.7.2 o superior.' + #13#10 +
    'Instale .NET Framework 4.8 desde Microsoft y vuelva a ejecutar el instalador.' + #13#10#13#10 +
    '¿Desea continuar de todos modos?', mbConfirmation, MB_YESNO) = IDNO then
    Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CfgDir, CfgPath, CfgContent, ModoPath, Modo, LicSrc, LicDst, NombrePuesto: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    CfgDir := ExpandConstant('{commonappdata}\SCHPOS');
    if not DirExists(CfgDir) then
      CreateDir(CfgDir);
    Exec('icacls.exe', '"' + CfgDir + '" /grant *S-1-5-32-545:(OI)(CI)M /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    if EsModoCliente then
      Modo := 'Cliente'
    else
      Modo := 'Servidor';
    ModoPath := CfgDir + '\modo_red.cfg';
    if FileExists(ModoPath) then
      DeleteFile(ModoPath);
    SaveStringToFile(ModoPath, Modo, False);

    NombrePuesto := '';
    if PaginaPuesto <> nil then
      NombrePuesto := Trim(PaginaPuesto.Values[0]);
    if NombrePuesto = '' then
    begin
      if EsModoCliente then
        NombrePuesto := 'CAJA-01'
      else
        NombrePuesto := 'SERVIDOR';
    end;
    if FileExists(CfgDir + '\puesto.cfg') then
      DeleteFile(CfgDir + '\puesto.cfg');
    SaveStringToFile(CfgDir + '\puesto.cfg', NombrePuesto, False);

    CfgPath := CfgDir + '\conexion.cfg';
    if EsModoServidor and (not FileExists(CfgPath)) then
    begin
      CfgContent := 'Server=(LocalDB)\MSSQLLocalDB;Database=SchPosDB;Integrated Security=True;Encrypt=False;';
      SaveStringToFile(CfgPath, CfgContent, False);
    end;

    LicSrc := ExpandConstant('{app}\licencia.key');
    LicDst := CfgDir + '\licencia.key';
    if FileExists(LicSrc) and (not FileExists(LicDst)) then
      CopyFile(LicSrc, LicDst, False);

    SHChangeNotify($08000000, $0000, 0, 0);
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
end;
