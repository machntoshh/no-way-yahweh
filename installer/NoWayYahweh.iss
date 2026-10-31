; No way? Yahweh! — Inno Setup script. Build with: tools\build-installer.ps1
; Per-user install (no admin). Upgrades install over the previous version and keep the user's data
; (%LOCALAPPDATA%\NoWayYahweh). Uninstalling runs the app's gate first: reflection + challenges.

#define AppVersion "1.0.0"   ; keep in sync with src/Directory.Build.props and extension/manifest.json

[Setup]
AppId={{8B1F7C2E-5D4A-4E8B-9C3F-2A6D1E0B7F45}
AppName=No way? Yahweh!
AppVersion={#AppVersion}
AppVerName=No way? Yahweh! {#AppVersion}
AppPublisher=No way? Yahweh!
AppComments=A pause before the temptation.
DefaultDirName={localappdata}\Programs\NoWayYahweh
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=NoWayYahweh-Setup-{#AppVersion}
SetupIconFile=..\src\NoWayYahweh.App\app.ico
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\NoWayYahweh.exe
UninstallDisplayName=No way? Yahweh!
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
ShowLanguageDialog=no

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\extension\*"; DestDir: "{app}\extension"; Excludes: "\_metadata";Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\docs\RECUPERACAO.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\PRIVACIDADE.md"; DestDir: "{app}\docs"; Flags: ignoreversion
; Licenses: ours, and the notices the bundled .NET runtime requires.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\tools\diagnose.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion

[Icons]
; File names cannot contain "?", so shortcuts use the full-width "？" (U+FF1F), which Windows allows.
Name: "{autoprograms}\No way？ Yahweh!"; Filename: "{app}\NoWayYahweh.exe"
Name: "{autoprograms}\No way？ Yahweh! - pasta da extensão"; Filename: "{app}\extension"
Name: "{autodesktop}\No way？ Yahweh!"; Filename: "{app}\NoWayYahweh.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NoWayYahweh.exe"; Parameters: "--background"; StatusMsg: "Registrando nos navegadores..."; Flags: runhidden waituntilterminated
Filename: "{app}\NoWayYahweh.exe"; Description: "Abrir o No way? Yahweh!"; Flags: postinstall nowait skipifsilent
Filename: "{app}\extension"; Description: "Abrir a pasta da extensão"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallDelete]
; Indexed rules the browser writes inside an unpacked extension folder.
Type: filesandordirs; Name: "{app}\extension\_metadata"

[Code]
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      'Falta um passo, no navegador:' + #13#10#13#10 +
      '1. Abra brave://extensions (ou chrome://extensions, edge://extensions).' + #13#10 +
      '2. Ative o "Modo do desenvolvedor".' + #13#10 +
      '3. Clique em "Carregar sem compactação" e escolha a pasta:' + #13#10 +
      '   ' + ExpandConstant('{app}\extension') + #13#10 +
      '4. Em "Detalhes", ative "Permitir em modo privado".' + #13#10#13#10 +
      'Se você já tinha a extensão carregada de outra pasta, remova-a antes.';
end;

// Uninstalling passes through the same pause as everything else: the app shows the reflection
// and challenges and returns 0 only if they are completed (and then cleans up its registry
// entries and data). Any other result cancels the uninstall and protection stays.
function InitializeUninstall(): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{app}\NoWayYahweh.exe'), '--uninstall-gate', '', SW_SHOW, ewWaitUntilTerminated, Code) and (Code = 0);
  if not Result then
    MsgBox('Desinstalação cancelada. A proteção continua ativa.', mbInformation, MB_OK);
end;
