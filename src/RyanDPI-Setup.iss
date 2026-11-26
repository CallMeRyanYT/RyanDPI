; RyanDPI Unbound Windows Installer Script (EN / TR / PT-BR / RU / AR)
; Requires Inno Setup 6

#define MyAppName "RyanDPI"
#define MyAppVersion "3.1.3"
#define MyAppPublisher "RyanDPI"
#define MyAppURL "https://github.com/CallMeRyanYT/RyanDPI"
#define MyAppExeName "RyanDPI.exe"

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
english.WelcomeLabel2=This wizard will install [name/ver] (Unbound) on your computer.%n%n[name] unlocks blocked apps and websites worldwide (Turkey, Russia, Brazil Discord Screen Share, Iran, Indonesia, and global DPI ISPs) with zero gaming lag.%n%nClick Next to continue.
english.LaunchAfterInstall=Launch RyanDPI now
english.CreateDesktopShortcut=Create a desktop shortcut
english.UninstallProgress=Stopping RyanDPI services and restoring network settings...
english.DotNetMissing=RyanDPI requires Microsoft .NET 8.0 Desktop Runtime (x64), which was not detected on this PC.%n%nWould you like to open Microsoft's official download page now?

turkish.WelcomeLabel2=Bu sihirbaz bilgisayarınıza [name/ver] (Unbound) sürümünü kuracaktır.%n%n[name], Türkiye, Rusya, Brezilya ve dünya genelinde engelli tüm uygulama ve siteleri oyun pingini artırmadan açar.%n%nDevam etmek için İleri'ye tıklayın.
turkish.LaunchAfterInstall=RyanDPI'yı şimdi başlat
turkish.CreateDesktopShortcut=Masaüstü kısayolu oluştur
turkish.UninstallProgress=RyanDPI hizmetleri durduruluyor ve ağ ayarları geri yükleniyor...
turkish.DotNetMissing=RyanDPI için Microsoft .NET 8.0 Desktop Runtime (x64) gereklidir ancak bulunamadı.%n%nResmi indirme sayfasını şimdi açmak ister misiniz?

brazilianportuguese.WelcomeLabel2=Este assistente instalará o [name/ver] (Unbound) no seu computador.%n%nO [name] desbloqueia o Compartilhamento de Tela e Vídeo do Discord no Brasil (0 ms de lag em jogos) e contorna bloqueios DPI no mundo todo.%n%nClique em Avançar para continuar.
brazilianportuguese.LaunchAfterInstall=Iniciar o RyanDPI agora
brazilianportuguese.CreateDesktopShortcut=Criar atalho na área de trabalho
brazilianportuguese.UninstallProgress=Parando os serviços do RyanDPI e restaurando a rede...
brazilianportuguese.DotNetMissing=O RyanDPI requer o Microsoft .NET 8.0 Desktop Runtime (x64), que não foi encontrado.%n%nDeseja abrir a página oficial de download agora?

russian.WelcomeLabel2=Этот мастер установит [name/ver] (Unbound) на ваш компьютер.%n%n[name] разблокирует YouTube, Discord, Instagram, Roblox и заблокированные сайты по всему миру без задержек в играх.%n%nНажмите Далее, чтобы продолжить.
russian.LaunchAfterInstall=Запустить RyanDPI сейчас
russian.CreateDesktopShortcut=Создать ярлык на рабочем столе
russian.UninstallProgress=Остановка служб RyanDPI и восстановление настроек сети...
russian.DotNetMissing=Для работы RyanDPI требуется Microsoft .NET 8.0 Desktop Runtime (x64).%n%nОткрыть официальную страницу загрузки Microsoft?

arabic.WelcomeLabel2=سيقوم هذا المعالج بتثبيت [name/ver] (Unbound) على جهازك.%n%nيقوم [name] بفتح التطبيقات والمواقع المحجوبة عالمياً بدون أي زيادة في البنق أثناء اللعب.%n%nانقر التالي للمتابعة.
arabic.LaunchAfterInstall=تشغيل RyanDPI الآن
arabic.CreateDesktopShortcut=إنشاء اختصار على سطح المكتب
arabic.UninstallProgress=إيقاف خدمات RyanDPI واستعادة إعدادات الشبكة...
arabic.DotNetMissing=يتطلب RyanDPI وجود Microsoft .NET 8.0 Desktop Runtime (x64).%n%nهل تريد فتح صفحة التنزيل الرسمية الآن؟

[Setup]
ShowLanguageDialog=auto
ChangesAssociations=yes
AppId={{8D4F62C9-1A7B-4C94-9E35-72A8B1F4209E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppMutex=Global\RyanDPI_SingleInstance_Mutex
CloseApplications=yes
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
UsePreviousAppDir=yes
DirExistsWarning=no
OutputDir=..\dist
OutputBaseFilename=RyanDPI-Setup-Windows-{#MyAppVersion}
SetupIconFile=RyanDPI\Resources\ryandpi.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "RyanDPI\bin\Release\net8.0-windows\RyanDPI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "RyanDPI\bin\Release\net8.0-windows\RyanDPI.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "RyanDPI\bin\Release\net8.0-windows\RyanDPI.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "RyanDPI\bin\Release\net8.0-windows\RyanDPI.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "RyanDPI\bin\Release\net8.0-windows\res\*"; DestDir: "{app}\res"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "RyanDPI\bin\Release\net8.0-windows\Prerequisites\*"; DestDir: "{app}\Prerequisites"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "SOFTWARE\RyanDPI"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\RyanDPI"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
function IsDotNet8DesktopInstalled: Boolean;
var
  Key, DisplayName, Version: String;
  SubKeyNames: TArrayOfString;
  I: Integer;
begin
  Result := False;
  Key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  if RegGetSubkeyNames(HKLM, Key, SubKeyNames) then
  begin
    for I := 0 to GetArrayLength(SubKeyNames) - 1 do
    begin
      if RegQueryStringValue(HKLM, Key + '\' + SubKeyNames[I], 'DisplayName', DisplayName) then
      begin
        if (Pos('Microsoft Windows Desktop Runtime - 8.0', DisplayName) > 0) or
           (Pos('Microsoft Windows Desktop Runtime 8.0', DisplayName) > 0) then
        begin
          if RegQueryStringValue(HKLM, Key + '\' + SubKeyNames[I], 'DisplayVersion', Version) then
          begin
            if Pos('8.0', Version) = 1 then
            begin
              Result := True;
              Exit;
            end;
          end;
        end;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if (not WizardSilent) and (not IsDotNet8DesktopInstalled) then
  begin
    if MsgBox(CustomMessage('DotNetMissing'), mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;

procedure StopAndDeleteService(ServiceName: String);
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
  Exec('sc.exe', 'delete ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Flush Windows Explorer shell icon cache so Taskbar, Desktop & Start Menu show the latest icon immediately
    Exec(ExpandConstant('{sys}\ie4uinit.exe'), '-show', '', SW_HIDE, ewNoWait, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  EngineDir: String;
  LocalAppDir: String;
  ResetDnsCmd: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if Assigned(WizardForm) and Assigned(WizardForm.StatusLabel) then
      WizardForm.StatusLabel.Caption := CustomMessage('UninstallProgress');

    // Stop and remove RyanDPI and any legacy services
    StopAndDeleteService('ProxiFyreService');
    StopAndDeleteService('RyanDpiEngine');
    StopAndDeleteService('RyanDPI');
    StopAndDeleteService('dnscrypt-proxy');
    StopAndDeleteService('ByeDPI');
    StopAndDeleteService('GoodbyeDPI');
    StopAndDeleteService('zapret');
    StopAndDeleteService('winws1');
    StopAndDeleteService('winws2');
    StopAndDeleteService('WinDivert');
    StopAndDeleteService('WinDivert14');
    StopAndDeleteService('wiresock-client-service');
    StopAndDeleteService('WireSockAppService');
    StopAndDeleteService('WireSockConnectService');
    Exec('schtasks.exe', '/delete /tn "RyanDpiNetworkWatch" /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('schtasks.exe', '/delete /tn "WireSockRefresh" /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Remove firewall rules
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Engine TCP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Engine UDP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Engine In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Engine Out"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Geo Engine TCP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Geo Engine UDP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Geo Engine In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Geo Engine Out"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Router TCP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Router UDP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Router In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Router Out"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Encrypted DNS TCP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Encrypted DNS UDP-In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Encrypted DNS In"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh.exe', 'advfirewall firewall delete rule name="RyanDPI Encrypted DNS Out"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Ensure no lingering engine processes lock files before deleting {app}\engine
    Exec('taskkill.exe', '/F /IM ProxiFyre.exe /IM ciadpi.exe /IM usque.exe /IM dnscrypt-proxy.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Restore automatic DHCP DNS and clear DoH settings on physical + active non-virtual adapters
    ResetDnsCmd := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' +
      '$seen = @{}; $targets = @(); ' +
      'Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { if (-not $seen.ContainsKey($_.InterfaceIndex)) { $seen[$_.InterfaceIndex]=$true; $targets+=$_ } }; ' +
      'Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq ''Up'' -and $_.InterfaceDescription -notlike ''*Virtual*'' -and $_.InterfaceDescription -notlike ''*Hyper-V*'' -and $_.InterfaceDescription -notlike ''*WSL*'' -and $_.Name -notlike ''*vEthernet*'' -and $_.InterfaceDescription -notlike ''*TAP*'' -and $_.InterfaceDescription -notlike ''*TUN*'' -and $_.InterfaceDescription -notlike ''*VPN*'' } | ForEach-Object { if (-not $seen.ContainsKey($_.InterfaceIndex)) { $seen[$_.InterfaceIndex]=$true; $targets+=$_ } }; ' +
      'foreach ($a in $targets) { ' +
      'Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue; ' +
      'netsh interface ip set dns """$($a.Name)""" dhcp | Out-Null; ' +
      'netsh interface ipv6 set dns """$($a.Name)""" dhcp | Out-Null; ' +
      '$p = ''HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\'' + $a.InterfaceGuid + ''\DohInterfaceSettings''; ' +
      'if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue } }; ' +
      'Clear-DnsClientCache -ErrorAction SilentlyContinue; ipconfig /flushdns | Out-Null"';
    Exec('powershell.exe', ResetDnsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Clean up engine and local app data
    EngineDir := ExpandConstant('{app}\engine');
    if DirExists(EngineDir) then
      DelTree(EngineDir, True, True, True);

    LocalAppDir := ExpandConstant('{localappdata}\RyanDPI');
    if DirExists(LocalAppDir) then
      DelTree(LocalAppDir, True, True, True);
  end;
end;
