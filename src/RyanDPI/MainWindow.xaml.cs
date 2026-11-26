using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using RyanDPI.Models;
using RyanDPI.Services;

namespace RyanDPI
{
    public partial class MainWindow : Window
    {
        private readonly RyanDpiEngineService _engine = new();
        private readonly AutoUpdateService _updater = new();
        private readonly CancellationTokenSource _windowCts = new();
        private RyanDpiSettings _settings;
        private RyanDpiStatus? _currentStatus;
        private bool _isBusy;
        private bool _isUpdatingLanguage;
        private bool _lastNoticeIsSuccess = true;
        private List<SelfTestCheckResult> _lastSelfTestRaw = new();

        private readonly ObservableCollection<string> _excludedApps = new();
        private readonly ObservableCollection<SelfTestDisplayItem> _selfTestItems = new();
        private IntPtr _hIconBig = IntPtr.Zero;
        private IntPtr _hIconSmall = IntPtr.Zero;

        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int SM_CXICON = 11;
        private const int SM_CXSMICON = 49;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateIconFromResourceEx(
            byte[] pbIconBits, uint cbIconBits, bool fIcon, uint dwVersion,
            int cxDesired, int cyDesired, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public MainWindow()
        {
            InitializeComponent();

            _settings = RyanDpiSettings.Load();
            LocalizationService.SetLanguage(_settings.Language);
            App.ApplyTheme(_settings.IsDarkTheme);
            UpdateThemeIcon();

            ListExcludes.ItemsSource = _excludedApps;
            ListSelfTestResults.ItemsSource = _selfTestItems;

            LoadSettingsIntoUI();
            ApplyLanguage();

            // First-startup Country / Region Picker (shown once for new users; always re-openable via top Region pill)
            if (!_settings.RegionConfigured && string.IsNullOrWhiteSpace(App.UpdatedToVersion))
            {
                OverlayRegionPicker.Visibility = Visibility.Visible;
            }

            TxtLogs.Text = LogService.GetRecentLogsText();
            LogService.LineLogged += OnLogLineReceived;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync();

            if (!string.IsNullOrWhiteSpace(App.UpdatedToVersion))
            {
                await HandlePostUpdateStartupAsync(App.UpdatedToVersion);
                return;
            }

            if (_settings.AutoUpdateOnStartup && !App.SkipAutoUpdateCheck)
            {
                await CheckAndApplyUpdateAsync(isManualCheck: false);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyNativeWindowIcons();
        }

        private void ApplyNativeWindowIcons()
        {
            // Explicitly push BIG (taskbar / Alt-Tab) and SMALL (window) HICONs via WM_SETICON
            // directly from the embedded ryandpi.ico so Windows 11 Taskbar never shows a stale cached icon.
            try
            {
                var sri = Application.GetResourceStream(new Uri("pack://application:,,,/RyanDPI;component/Resources/ryandpi.ico", UriKind.Absolute));
                if (sri == null) return;

                byte[] icoBytes;
                using (var ms = new MemoryStream())
                {
                    sri.Stream.CopyTo(ms);
                    icoBytes = ms.ToArray();
                }

                if (icoBytes.Length < 22) return;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int bigTarget = Math.Max(48, GetSystemMetrics(SM_CXICON));
                int smallTarget = Math.Max(16, GetSystemMetrics(SM_CXSMICON));

                _hIconBig = CreateBestIconFromIco(icoBytes, bigTarget);
                _hIconSmall = CreateBestIconFromIco(icoBytes, smallTarget);

                if (_hIconBig != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);
                }
                if (_hIconSmall != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
                }
            }
            catch
            {
                // Fall back cleanly to XAML Window.Icon
            }
        }

        private static IntPtr CreateBestIconFromIco(byte[] icoBytes, int targetSize)
        {
            int count = BitConverter.ToUInt16(icoBytes, 4);
            int bestIndex = -1;
            int bestScore = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int entryOff = 6 + i * 16;
                if (entryOff + 16 > icoBytes.Length) break;

                int w = icoBytes[entryOff] == 0 ? 256 : icoBytes[entryOff];
                // Prefer exact match or smallest frame >= targetSize
                int score = w >= targetSize ? (w - targetSize) : (1000 + (targetSize - w));
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return IntPtr.Zero;

            int off = 6 + bestIndex * 16;
            int dataLen = BitConverter.ToInt32(icoBytes, off + 8);
            int dataOff = BitConverter.ToInt32(icoBytes, off + 12);
            if (dataLen <= 0 || dataOff < 0 || dataOff + dataLen > icoBytes.Length) return IntPtr.Zero;

            var frameBytes = new byte[dataLen];
            Buffer.BlockCopy(icoBytes, dataOff, frameBytes, 0, dataLen);

            return CreateIconFromResourceEx(frameBytes, (uint)dataLen, true, 0x00030000, targetSize, targetSize, 0);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _windowCts.Cancel();
                _windowCts.Dispose();
            }
            catch
            {
                // Ignore cancellation disposal errors
            }
            if (_hIconBig != IntPtr.Zero) { DestroyIcon(_hIconBig); _hIconBig = IntPtr.Zero; }
            if (_hIconSmall != IntPtr.Zero) { DestroyIcon(_hIconSmall); _hIconSmall = IntPtr.Zero; }
            LogService.LineLogged -= OnLogLineReceived;
            base.OnClosed(e);
        }

        private void OnLogLineReceived(string line)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (TxtLogs.LineCount > 500)
                {
                    TxtLogs.Text = LogService.GetRecentLogsText();
                }
                else
                {
                    if (!string.IsNullOrEmpty(TxtLogs.Text))
                    {
                        TxtLogs.AppendText(Environment.NewLine);
                    }
                    TxtLogs.AppendText(line);
                }
                TxtLogs.ScrollToEnd();
            });
        }

        #region Window, Language & Navigation Controls

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnLanguageToggle_Click(object sender, RoutedEventArgs e)
        {
            PopulateLanguagePickerButtons();
            OverlayLanguagePicker.Visibility = Visibility.Visible;
        }

        private void BtnCloseLanguagePicker_Click(object sender, RoutedEventArgs e)
        {
            OverlayLanguagePicker.Visibility = Visibility.Collapsed;
        }

        private void PopulateLanguagePickerButtons()
        {
            GridLanguageOptions.Children.Clear();
            var current = LocalizationService.NormalizeLanguage(_settings.Language);
            var accentBrush = (Brush)TryFindResource("AccentCyanBrush") ?? Brushes.Crimson;
            var primaryText = (Brush)TryFindResource("TextPrimaryBrush") ?? Brushes.White;
            var secondaryText = (Brush)TryFindResource("TextSecondaryBrush") ?? Brushes.LightGray;
            var secondaryBtnStyle = TryFindResource("SecondaryButtonStyle") as Style;

            foreach (var opt in LocalizationService.AllLanguages)
            {
                var isSelected = opt.Code == current;
                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new TextBlock
                {
                    Text = opt.ShortLabel,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5,
                    Foreground = isSelected ? accentBrush : secondaryText,
                    Width = 28,
                    VerticalAlignment = VerticalAlignment.Center
                });
                panel.Children.Add(new TextBlock
                {
                    Text = opt.NativeName,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = primaryText,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var btn = new Button
                {
                    Content = panel,
                    Tag = opt.Code,
                    Style = secondaryBtnStyle,
                    Height = 40,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 0, 10, 0),
                    Margin = new Thickness(4)
                };
                if (isSelected)
                {
                    btn.BorderBrush = accentBrush;
                    btn.BorderThickness = new Thickness(1.4);
                }
                btn.Click += (_, _) => SelectLanguage(opt.Code);
                GridLanguageOptions.Children.Add(btn);
            }
        }

        public void SelectLanguage(string languageCode)
        {
            _settings.Language = LocalizationService.NormalizeLanguage(languageCode);
            _settings.Save();
            LocalizationService.SetLanguage(_settings.Language);
            OverlayLanguagePicker.Visibility = Visibility.Collapsed;
            ApplyLanguage();
        }

        private void BtnRegionPicker_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            OverlayRegionPicker.Visibility = Visibility.Visible;
        }

        private void BtnCloseRegionPicker_Click(object sender, RoutedEventArgs e)
        {
            _settings.RegionConfigured = true;
            _settings.Save();
            OverlayRegionPicker.Visibility = Visibility.Collapsed;
        }

        private async void BtnPickRegion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            if (!Enum.TryParse<RegionPreset>(tag, out var region)) return;

            _settings.ApplyRegionPreset(region);
            _settings.Save();

            OverlayRegionPicker.Visibility = Visibility.Collapsed;
            LoadSettingsIntoUI();
            ApplyLanguage();

            if (_currentStatus != null && _currentStatus.IsInstalled)
            {
                SetBusy(true, showProgressCard: false);
                try
                {
                    var res = await _engine.ApplySettingsAndRestartAsync(_settings);
                    ShowNotice(res.Message, res.Success);
                }
                finally
                {
                    SetBusy(false);
                    await RefreshStatusAsync();
                }
            }
            else if (_currentStatus != null)
            {
                RenderStatus(_currentStatus);
            }
        }

        private string GetRegionShortTag(RegionPreset region) => region switch
        {
            RegionPreset.Turkey => LocalizationService.T("Region_TurkeyTag"),
            RegionPreset.Russia => LocalizationService.T("Region_RussiaTag"),
            RegionPreset.Brazil => LocalizationService.T("Region_BrazilTag"),
            _ => LocalizationService.T("Region_GlobalTag")
        };

        public void ApplyLanguage()
        {
            _isUpdatingLanguage = true;
            try
            {
                // Title bar & Navigation
                TxtBrandBadge.Text = LocalizationService.T("Badge_AllAppsSpeed");
                BtnRegionPicker.Content = GetRegionShortTag(_settings.Region);
                BtnRegionPicker.ToolTip = LocalizationService.T("RegionButton_Tooltip");
                BtnLanguageToggle.Content = LocalizationService.T("LangButton_Text");
                BtnLanguageToggle.ToolTip = LocalizationService.T("LangButton_Tooltip");
                BtnThemeToggle.ToolTip = LocalizationService.T("ThemeButton_Tooltip");
                BtnMinimize.ToolTip = LocalizationService.T("Minimize_Tooltip");
                BtnClose.ToolTip = LocalizationService.T("Close_Tooltip");
                BtnRefreshStatus.ToolTip = LocalizationService.T("Refresh_Tooltip");

                // First-startup / Country Picker overlay
                TxtRegionPickerTitle.Text = LocalizationService.T("Region_PickerTitle");
                TxtRegionPickerSubtitle.Text = LocalizationService.T("Region_PickerSubtitle");
                TxtPickGlobalTitle.Text = LocalizationService.T("Region_GlobalTitle");
                TxtPickGlobalDesc.Text = LocalizationService.T("Region_GlobalDesc");
                TxtPickTurkeyTitle.Text = LocalizationService.T("Region_TurkeyTitle");
                TxtPickTurkeyDesc.Text = LocalizationService.T("Region_TurkeyDesc");
                TxtPickRussiaTitle.Text = LocalizationService.T("Region_RussiaTitle");
                TxtPickRussiaDesc.Text = LocalizationService.T("Region_RussiaDesc");
                TxtPickBrazilTitle.Text = LocalizationService.T("Region_BrazilTitle");
                TxtPickBrazilDesc.Text = LocalizationService.T("Region_BrazilDesc");
                BtnCloseRegionPicker.Content = LocalizationService.T("Region_PickerClose");
                BtnCloseLanguagePicker.Content = LocalizationService.T("Close_Tooltip");

                NavHome.Content = LocalizationService.T("Nav_Home");
                NavSettings.Content = LocalizationService.T("Nav_Settings");
                NavTools.Content = LocalizationService.T("Nav_Tools");

                // Health Strip headers
                TxtPillEngineTitle.Text = LocalizationService.T("Pill_EngineTitle");
                TxtPillRouterTitle.Text = LocalizationService.T("Pill_RouterTitle");
                TxtPillDnsTitle.Text = LocalizationService.T("Pill_DnsTitle");

                // Home controls
                BtnRestartEngine.Content = LocalizationService.T("Btn_Reinstall");
                BtnQuickUninstall.Content = LocalizationService.T("Btn_Uninstall");
                BtnRebootNow.Content = LocalizationService.T("Btn_RestartPc");

                // Self-Test card
                TxtSelfTestTitle.Text = LocalizationService.T("SelfTest_Title");
                TxtSelfTestSubtitle.Text = LocalizationService.T("SelfTest_Subtitle");
                if (!_isBusy)
                {
                    BtnRunSelfTest.Content = LocalizationService.T("SelfTest_Run");
                }
                TxtSelfTestPlaceholder.Text = LocalizationService.T("SelfTest_Placeholder");

                // Settings page — Region & Brazil Split Tunnel
                TxtRegionSectionTitle.Text = LocalizationService.T("Settings_RegionTitle");
                ItemRegionGlobal.Content = LocalizationService.T("Region_GlobalTitle");
                ItemRegionTurkey.Content = LocalizationService.T("Region_TurkeyTitle");
                ItemRegionRussia.Content = LocalizationService.T("Region_RussiaTitle");
                ItemRegionBrazil.Content = LocalizationService.T("Region_BrazilTitle");
                RefreshComboSelectionDisplay(CmbRegion);

                TxtBrazilSectionTitle.Text = LocalizationService.T("Settings_BrazilCardTitle");
                TxtBrazilNotice.Text = LocalizationService.T("Settings_BrazilNotice");
                ItemBrazilWarp.Content = LocalizationService.T("Brazil_TunnelWarp");
                ItemBrazilCustom.Content = LocalizationService.T("Brazil_TunnelCustom");
                RefreshComboSelectionDisplay(CmbBrazilTunnel);
                TxtCustomSocksEndpointLabel.Text = LocalizationService.T("Brazil_CustomEndpointLabel");
                TxtCustomSocksUserLabel.Text = LocalizationService.T("Brazil_CustomUserLabel");
                TxtCustomSocksPassLabel.Text = LocalizationService.T("Brazil_CustomPassLabel");
                TxtBrazilUdpTitle.Text = LocalizationService.T("Brazil_UdpTitle");
                TxtBrazilUdpDesc.Text = LocalizationService.T("Brazil_UdpDesc");
                TxtBrazilBrowsersTitle.Text = LocalizationService.T("Brazil_BrowsersTitle");
                TxtBrazilBrowsersDesc.Text = LocalizationService.T("Brazil_BrowsersDesc");

                // Settings page — DPI Profile
                TxtProfileSectionTitle.Text = LocalizationService.T("Settings_ProfileTitle");
                ItemProfileAuto.Content = LocalizationService.T("Profile_AutoUniversal");
                ItemProfileRussia.Content = LocalizationService.T("Profile_RussiaTspu");
                ItemProfileClassic.Content = LocalizationService.T("Profile_Classic");
                ItemProfileStrict.Content = LocalizationService.T("Profile_StrictIsp");
                ItemProfileCustom.Content = LocalizationService.T("Profile_Custom");
                RefreshComboSelectionDisplay(CmbProfile);
                TxtCustomArgsLabel.Text = LocalizationService.T("Settings_CustomArgsLabel");

                TxtDnsSectionTitle.Text = LocalizationService.T("Settings_DnsTitle");
                ItemDnsEncrypted.Content = LocalizationService.T("Dns_Encrypted");
                ItemDnsNativeDoH.Content = LocalizationService.T("Dns_NativeDoH");
                ItemDnsKeepSystem.Content = LocalizationService.T("Dns_KeepSystem");
                RefreshComboSelectionDisplay(CmbDnsMode);

                TxtNetworkSectionTitle.Text = LocalizationService.T("Settings_NetworkTitle");
                TxtBypassLanTitle.Text = LocalizationService.T("Settings_BypassLanTitle");
                TxtBypassLanDesc.Text = LocalizationService.T("Settings_BypassLanDesc");
                TxtTunnelUdpTitle.Text = LocalizationService.T("Settings_TunnelUdpTitle");
                TxtTunnelUdpDesc.Text = LocalizationService.T("Settings_TunnelUdpDesc");
                TxtAutoUpdateTitle.Text = LocalizationService.T("Settings_AutoUpdateTitle");
                TxtAutoUpdateDesc.Text = LocalizationService.T("Settings_AutoUpdateDesc");

                TxtExcludesSectionTitle.Text = LocalizationService.T("Settings_ExcludesTitle");
                TxtExcludesDesc.Text = LocalizationService.T("Settings_ExcludesDesc");
                TxtExcludeInput.ToolTip = LocalizationService.T("Settings_ExcludeTooltip");
                BtnAddExclude.Content = LocalizationService.T("Btn_Add");
                BtnBrowseExclude.Content = LocalizationService.T("Btn_Browse");
                TxtNoExcludes.Text = LocalizationService.T("Settings_NoExcludes");
                if (!_isBusy)
                {
                    BtnSaveApplySettings.Content = LocalizationService.T("Btn_SaveApply");
                }

                // Tools & Logs page
                TxtToolsSectionTitle.Text = LocalizationService.T("Tools_Title");
                BtnResetDns.Content = LocalizationService.T("Btn_ResetDns");
                BtnOpenLogs.Content = LocalizationService.T("Btn_OpenLogs");
                BtnToolsUninstall.Content = LocalizationService.T("Btn_UninstallFull");
                TxtLogsSectionTitle.Text = LocalizationService.T("Logs_Title");
                BtnCopyLogs.Content = LocalizationService.T("Btn_Copy");
                BtnClearLogs.Content = LocalizationService.T("Btn_Clear");
                TxtAboutVersion.Text = LocalizationService.Tf("About_TitleFmt", AutoUpdateService.GetCurrentVersionString());
                TxtAboutSubtitle.Text = LocalizationService.T("About_Subtitle");
                if (!_isBusy)
                {
                    BtnCheckUpdates.Content = LocalizationService.T("Btn_CheckUpdates");
                }

                UpdateRegionSectionVisibility();
                UpdateProfileDescription();
                UpdateDnsDescription();

                if (_currentStatus != null)
                {
                    RenderStatus(_currentStatus);
                }
                else
                {
                    TxtStatusHeadline.Text = LocalizationService.T("Status_CheckingHeadline");
                    TxtStatusSubtitle.Text = LocalizationService.T("Status_CheckingSubtitle");
                    TxtEngineState.Text = LocalizationService.T("State_Checking");
                    TxtRouterState.Text = LocalizationService.T("State_Checking");
                    TxtDnsState.Text = LocalizationService.T("State_Checking");
                    BtnCleanLegacy.Content = LocalizationService.T("Btn_CleanLegacy");
                }

                if (_lastSelfTestRaw.Count > 0)
                {
                    RenderSelfTestResults(_lastSelfTestRaw);
                }
            }
            finally
            {
                _isUpdatingLanguage = false;
            }
        }

        private static void RefreshComboSelectionDisplay(ComboBox combo)
        {
            var idx = combo.SelectedIndex;
            if (idx >= 0)
            {
                combo.SelectedIndex = -1;
                combo.SelectedIndex = idx;
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsDarkTheme = !_settings.IsDarkTheme;
            _settings.Save();
            App.ApplyTheme(_settings.IsDarkTheme);
            UpdateThemeIcon();

            // Re-apply semantic brushes from the newly loaded dictionary
            if (_currentStatus != null)
            {
                RenderStatus(_currentStatus);
            }
            if (PanelNotice.Visibility == Visibility.Visible)
            {
                ApplyNoticeColors(_lastNoticeIsSuccess);
            }
            if (_lastSelfTestRaw.Count > 0)
            {
                RenderSelfTestResults(_lastSelfTestRaw);
            }
        }

        private void UpdateThemeIcon()
        {
            IconSun.Visibility = _settings.IsDarkTheme ? Visibility.Visible : Visibility.Collapsed;
            IconMoon.Visibility = _settings.IsDarkTheme ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageHome == null || PageSettings == null || PageTools == null) return;

            PageHome.Visibility = NavHome.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PageTools.Visibility = NavTools.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Status & Busy State

        private async Task RefreshStatusAsync()
        {
            try
            {
                var status = await _engine.GetStatusAsync();
                _currentStatus = status;
                RenderStatus(status);
            }
            catch (Exception ex)
            {
                LogService.Error($"Status check failed: {ex.Message}");
            }
        }

        private void RenderStatus(RyanDpiStatus status)
        {
            var successBrush = (Brush)FindResource("SuccessBrush");
            var successBg = (Brush)FindResource("SuccessBgBrush");
            var warnBrush = (Brush)FindResource("WarningBrush");
            var warnBg = (Brush)FindResource("WarningBgBrush");
            var accentBrush = (Brush)FindResource("AccentCyanBrush");
            var mutedBrush = (Brush)FindResource("TextMutedBrush");
            var cardAltBrush = (Brush)FindResource("BgCardAltBrush");

            // Hero Shield-DPI emblem & headline
            if (status.IsFullyActive)
            {
                StatusOrbOuter.Background = successBg;
                StatusOrbOuter.BorderBrush = successBrush;
                StatusShieldPath.Stroke = successBrush;
                StatusShieldText.Foreground = successBrush;
                StatusShieldGlow.Color = (Color)FindResource("SuccessColor");

                TxtStatusHeadline.Text = LocalizationService.T("Status_ActiveHeadline");
                TxtStatusSubtitle.Text = _settings.Region == RegionPreset.Brazil
                    ? LocalizationService.T("Status_ActiveSubtitleBrazil")
                    : LocalizationService.T("Status_ActiveSubtitle");

                BtnPrimaryAction.Content = LocalizationService.T("Status_ActiveCta");
                BtnPrimaryAction.Style = (Style)FindResource("SuccessButtonStyle");
                TxtActionCaption.Text = LocalizationService.T("Status_ActiveCaption");
                PanelInstalledActions.Visibility = Visibility.Visible;
            }
            else if (status.IsInstalled)
            {
                StatusOrbOuter.Background = warnBg;
                StatusOrbOuter.BorderBrush = warnBrush;
                StatusShieldPath.Stroke = warnBrush;
                StatusShieldText.Foreground = warnBrush;
                StatusShieldGlow.Color = (Color)FindResource("WarningColor");

                TxtStatusHeadline.Text = status.IsPartiallyRunning
                    ? LocalizationService.T("Status_PartialHeadline")
                    : LocalizationService.T("Status_PausedHeadline");
                TxtStatusSubtitle.Text = status.IsPartiallyRunning
                    ? LocalizationService.T("Status_PartialSubtitle")
                    : LocalizationService.T("Status_PausedSubtitle");

                BtnPrimaryAction.Content = LocalizationService.T("Status_PausedCta");
                BtnPrimaryAction.Style = (Style)FindResource("PrimaryButtonStyle");
                TxtActionCaption.Text = LocalizationService.T("Status_PausedCaption");
                PanelInstalledActions.Visibility = Visibility.Visible;
            }
            else
            {
                StatusOrbOuter.Background = _settings.IsDarkTheme
                    ? new SolidColorBrush(Color.FromRgb(8, 6, 8))
                    : cardAltBrush;
                StatusOrbOuter.BorderBrush = accentBrush;
                StatusShieldPath.Stroke = accentBrush;
                StatusShieldText.Foreground = accentBrush;
                StatusShieldGlow.Color = (Color)FindResource("AccentCyanColor");

                TxtStatusHeadline.Text = LocalizationService.T("Status_ReadyHeadline");
                TxtStatusSubtitle.Text = status.HasLegacyServices
                    ? LocalizationService.Tf("Status_ReadyLegacySubtitle", string.Join(", ", status.DetectedLegacyServices))
                    : (_settings.Region == RegionPreset.Brazil
                        ? LocalizationService.T("Status_ReadySubtitleBrazil")
                        : LocalizationService.T("Status_ReadySubtitle"));

                BtnPrimaryAction.Content = LocalizationService.T("Status_ReadyCta");
                BtnPrimaryAction.Style = (Style)FindResource("PrimaryButtonStyle");
                TxtActionCaption.Text = LocalizationService.T("Status_ReadyCaption");
                PanelInstalledActions.Visibility = Visibility.Collapsed;
            }

            // 3 Health Pills
            SetPill(DotEngine, TxtEngineState, status.EngineState,
                LocalizationService.T("State_Running"),
                LocalizationService.T("State_Stopped"),
                LocalizationService.T("State_NotSet"));
            SetPill(DotRouter, TxtRouterState, status.RouterState,
                _settings.Region == RegionPreset.Brazil
                    ? LocalizationService.T("State_DiscordOnly")
                    : LocalizationService.T("State_AllApps"),
                LocalizationService.T("State_Paused"),
                LocalizationService.T("State_NotSet"));

            if (status.DnsProxyState == ComponentState.Running)
            {
                DotDns.Fill = successBrush;
                TxtDnsState.Text = LocalizationService.T("State_Encrypted");
            }
            else if (_settings.DnsMode == DnsProtectionMode.WindowsNativeDoH && status.IsInstalled)
            {
                DotDns.Fill = successBrush;
                TxtDnsState.Text = LocalizationService.T("State_NativeDoH");
            }
            else if (status.DnsProxyState == ComponentState.Stopped)
            {
                DotDns.Fill = warnBrush;
                TxtDnsState.Text = LocalizationService.T("State_Stopped");
            }
            else
            {
                DotDns.Fill = mutedBrush;
                TxtDnsState.Text = _settings.DnsMode == DnsProtectionMode.KeepSystemDns
                    ? LocalizationService.T("State_Custom")
                    : LocalizationService.T("State_Default");
            }

            // Update Tools legacy button label if legacy services exist
            BtnCleanLegacy.Content = status.HasLegacyServices
                ? LocalizationService.Tf("Btn_CleanLegacyCount", status.DetectedLegacyServices.Count)
                : LocalizationService.T("Btn_CleanLegacy");
        }

        private void SetPill(System.Windows.Shapes.Ellipse dot, TextBlock label, ComponentState state,
            string runningText, string stoppedText, string notInstalledText)
        {
            switch (state)
            {
                case ComponentState.Running:
                    dot.Fill = (Brush)FindResource("SuccessBrush");
                    label.Text = runningText;
                    break;
                case ComponentState.Stopped:
                    dot.Fill = (Brush)FindResource("WarningBrush");
                    label.Text = stoppedText;
                    break;
                default:
                    dot.Fill = (Brush)FindResource("TextMutedBrush");
                    label.Text = notInstalledText;
                    break;
            }
        }

        private void SetBusy(bool busy, bool showProgressCard = false)
        {
            _isBusy = busy;
            BtnPrimaryAction.IsEnabled = !busy;
            BtnRestartEngine.IsEnabled = !busy;
            BtnQuickUninstall.IsEnabled = !busy;
            BtnRunSelfTest.IsEnabled = !busy;
            BtnRefreshStatus.IsEnabled = !busy;
            BtnSaveApplySettings.IsEnabled = !busy;
            BtnResetDns.IsEnabled = !busy;
            BtnCleanLegacy.IsEnabled = !busy;
            BtnToolsUninstall.IsEnabled = !busy;
            BtnCheckUpdates.IsEnabled = !busy;

            PanelProgress.Visibility = (busy && showProgressCard) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowNotice(string message, bool isSuccess, bool showRebootButton = false)
        {
            _lastNoticeIsSuccess = isSuccess;
            ApplyNoticeColors(isSuccess);
            TxtNoticeMessage.Text = message;
            BtnRebootNow.Visibility = showRebootButton ? Visibility.Visible : Visibility.Collapsed;
            PanelNotice.Visibility = Visibility.Visible;
        }

        private void ApplyNoticeColors(bool isSuccess)
        {
            PanelNotice.Background = (Brush)FindResource(isSuccess ? "SuccessBgBrush" : "DangerBgBrush");
            PanelNotice.BorderBrush = (Brush)FindResource(isSuccess ? "SuccessBrush" : "DangerBrush");
            TxtNoticeMessage.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }

        private void BtnDismissNotice_Click(object sender, RoutedEventArgs e)
        {
            PanelNotice.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Home Page Actions

        private async void BtnRefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            await RefreshStatusAsync();
        }

        private async void BtnPrimaryAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            if (_currentStatus == null || !_currentStatus.IsInstalled)
            {
                await RunInstallAsync();
            }
            else if (_currentStatus.IsFullyActive)
            {
                SetBusy(true, showProgressCard: false);
                PanelNotice.Visibility = Visibility.Collapsed;
                try
                {
                    var res = await _engine.StopAsync();
                    ShowNotice(res.Message, res.Success);
                }
                finally
                {
                    SetBusy(false);
                    await RefreshStatusAsync();
                }
            }
            else
            {
                SetBusy(true, showProgressCard: false);
                PanelNotice.Visibility = Visibility.Collapsed;
                try
                {
                    var res = await _engine.StartAsync(_settings);
                    ShowNotice(res.Message, res.Success);
                }
                finally
                {
                    SetBusy(false);
                    await RefreshStatusAsync();
                }
            }
        }

        private async void BtnReinstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            await RunInstallAsync();
        }

        private async Task RunInstallAsync()
        {
            SetBusy(true, showProgressCard: true);
            PanelNotice.Visibility = Visibility.Collapsed;
            ProgressInstall.Value = 5;
            TxtProgressTitle.Text = LocalizationService.T("Progress_InstallingTitle");
            TxtProgressStep.Text = LocalizationService.T("Progress_Starting");
            TxtProgressDetail.Text = LocalizationService.T("Progress_Initializing");

            var progress = new Progress<InstallProgressUpdate>(u =>
            {
                TxtProgressTitle.Text = u.Title;
                TxtProgressStep.Text = LocalizationService.Tf("Progress_StepFmt", u.StepIndex, u.TotalSteps);
                TxtProgressDetail.Text = u.Detail;
                ProgressInstall.Value = u.Percent;
            });

            try
            {
                var result = await _engine.InstallAndEnableAsync(_settings, progress);
                ShowNotice(result.Message, result.Success, result.RequiresReboot);
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var confirm = MessageBox.Show(
                LocalizationService.T("Confirm_UninstallBody"),
                LocalizationService.T("Confirm_UninstallTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, showProgressCard: true);
            PanelNotice.Visibility = Visibility.Collapsed;
            TxtProgressTitle.Text = LocalizationService.T("Progress_UninstallingTitle");
            TxtProgressStep.Text = LocalizationService.T("Progress_CleaningUp");
            TxtProgressDetail.Text = LocalizationService.T("Progress_UninstallingDetail");
            ProgressInstall.Value = 50;

            try
            {
                var res = await _engine.UninstallAllAsync(resetDns: true);
                ProgressInstall.Value = 100;
                ShowNotice(res.Message, res.Success);
                NavHome.IsChecked = true;
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private void BtnRebootNow_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                LocalizationService.T("Confirm_RebootBody"),
                LocalizationService.T("Confirm_RebootTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                CommandRunner.Run("shutdown.exe", "/r /t 0", 5000);
            }
        }

        private async void BtnRunSelfTest_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            SetBusy(true, showProgressCard: false);
            BtnRunSelfTest.Content = LocalizationService.T("SelfTest_Testing");
            TxtSelfTestPlaceholder.Visibility = Visibility.Collapsed;
            _selfTestItems.Clear();

            try
            {
                var results = await _engine.RunSelfTestAsync(_settings);
                _lastSelfTestRaw = results;
                RenderSelfTestResults(results);
            }
            catch (Exception ex)
            {
                LogService.Error($"Self-test error: {ex.Message}");
            }
            finally
            {
                BtnRunSelfTest.Content = LocalizationService.T("SelfTest_Run");
                SetBusy(false);
            }
        }

        private void RenderSelfTestResults(List<SelfTestCheckResult> results)
        {
            _selfTestItems.Clear();
            var okBrush = (Brush)FindResource("SuccessBrush");
            var failBrush = (Brush)FindResource("DangerBrush");

            foreach (var r in results)
            {
                _selfTestItems.Add(new SelfTestDisplayItem
                {
                    Name = r.Name,
                    Summary = r.Summary,
                    LatencyText = r.Passed
                        ? (r.LatencyMs <= 0 ? "<1 ms" : $"{r.LatencyMs} ms")
                        : LocalizationService.T("SelfTest_Failed"),
                    StatusBrush = r.Passed ? okBrush : failBrush
                });
            }
        }

        #endregion

        #region Settings Page

        private void LoadSettingsIntoUI()
        {
            _isUpdatingLanguage = true;
            try
            {
                SelectComboByTag(CmbRegion, _settings.Region.ToString());
                SelectComboByTag(CmbBrazilTunnel, _settings.BrazilTunnel.ToString());
                TxtCustomSocksEndpoint.Text = _settings.CustomSocksEndpoint;
                TxtCustomSocksUser.Text = _settings.CustomSocksUsername;
                TxtCustomSocksPass.Text = _settings.CustomSocksPassword;
                ChkBrazilIncludeUdp.IsChecked = _settings.BrazilIncludeUdp;
                ChkBrazilIncludeBrowsers.IsChecked = _settings.BrazilIncludeBrowsers;

                SelectComboByTag(CmbProfile, _settings.Profile.ToString());
                SelectComboByTag(CmbDnsMode, _settings.DnsMode.ToString());
                TxtCustomArgs.Text = _settings.CustomStrategyArgs;
                ChkBypassLan.IsChecked = _settings.BypassLan;
                ChkTunnelUdp.IsChecked = _settings.TunnelUdp;
                ChkAutoUpdate.IsChecked = _settings.AutoUpdateOnStartup;

                _excludedApps.Clear();
                foreach (var app in _settings.ExcludedApps)
                {
                    _excludedApps.Add(app);
                }
                UpdateExcludesPlaceholder();
                UpdateRegionSectionVisibility();
                UpdateProfileDescription();
                UpdateDnsDescription();
            }
            finally
            {
                _isUpdatingLanguage = false;
            }
        }

        private void CmbRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguage) return;
            var tag = (CmbRegion.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (Enum.TryParse<RegionPreset>(tag, out var region))
            {
                // Auto-select sensible defaults when user switches region in Settings
                _isUpdatingLanguage = true;
                try
                {
                    if (region == RegionPreset.Russia)
                    {
                        SelectComboByTag(CmbProfile, nameof(BypassProfile.RussiaTspu));
                        SelectComboByTag(CmbDnsMode, nameof(DnsProtectionMode.EncryptedDnsProxy));
                        ChkTunnelUdp.IsChecked = true;
                    }
                    else if (region == RegionPreset.Brazil)
                    {
                        SelectComboByTag(CmbDnsMode, nameof(DnsProtectionMode.KeepSystemDns));
                    }
                    else
                    {
                        SelectComboByTag(CmbProfile, nameof(BypassProfile.AutoUniversal));
                        SelectComboByTag(CmbDnsMode, nameof(DnsProtectionMode.EncryptedDnsProxy));
                        ChkTunnelUdp.IsChecked = false;
                    }
                }
                finally
                {
                    _isUpdatingLanguage = false;
                }
            }
            UpdateRegionSectionVisibility();
            UpdateProfileDescription();
            UpdateDnsDescription();
        }

        private void CmbBrazilTunnel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguage) return;
            UpdateRegionSectionVisibility();
        }

        private void UpdateRegionSectionVisibility()
        {
            if (CardBrazilSplitTunnel == null || CardDpiProfile == null || TxtRegionDescription == null) return;
            var regionTag = (CmbRegion.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool isBrazil = regionTag == nameof(RegionPreset.Brazil);

            CardBrazilSplitTunnel.Visibility = isBrazil ? Visibility.Visible : Visibility.Collapsed;
            CardDpiProfile.Visibility = isBrazil ? Visibility.Collapsed : Visibility.Visible;

            var brTunnelTag = (CmbBrazilTunnel.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            PanelCustomSocks.Visibility = (isBrazil && brTunnelTag == nameof(BrazilTunnelMode.CustomSocks5))
                ? Visibility.Visible
                : Visibility.Collapsed;

            TxtRegionDescription.Text = regionTag switch
            {
                nameof(RegionPreset.Turkey) => LocalizationService.T("Region_TurkeyDesc"),
                nameof(RegionPreset.Russia) => LocalizationService.T("Region_RussiaDesc"),
                nameof(RegionPreset.Brazil) => LocalizationService.T("Region_BrazilDesc"),
                _ => LocalizationService.T("Region_GlobalDesc")
            };
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void CmbProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguage) return;
            UpdateProfileDescription();
        }

        private void UpdateProfileDescription()
        {
            if (TxtProfileDescription == null || PanelCustomArgs == null) return;
            var tag = (CmbProfile.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            PanelCustomArgs.Visibility = tag == nameof(BypassProfile.Custom) ? Visibility.Visible : Visibility.Collapsed;
            TxtProfileDescription.Text = tag switch
            {
                nameof(BypassProfile.RussiaTspu) => LocalizationService.T("Profile_Desc_Russia"),
                nameof(BypassProfile.ClassicByeDpi) => LocalizationService.T("Profile_Desc_Classic"),
                nameof(BypassProfile.StrictIsp) => LocalizationService.T("Profile_Desc_Strict"),
                nameof(BypassProfile.Custom) => LocalizationService.T("Profile_Desc_Custom"),
                _ => LocalizationService.T("Profile_Desc_Auto")
            };
        }

        private void CmbDnsMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguage) return;
            UpdateDnsDescription();
        }

        private void UpdateDnsDescription()
        {
            if (TxtDnsDescription == null) return;
            var tag = (CmbDnsMode.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            TxtDnsDescription.Text = tag switch
            {
                nameof(DnsProtectionMode.WindowsNativeDoH) => LocalizationService.T("Dns_Desc_NativeDoH"),
                nameof(DnsProtectionMode.KeepSystemDns) => LocalizationService.T("Dns_Desc_KeepSystem"),
                _ => LocalizationService.T("Dns_Desc_Encrypted")
            };
        }

        private void TxtExcludeInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnAddExclude_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnAddExclude_Click(object sender, RoutedEventArgs e)
        {
            var entry = TxtExcludeInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(entry)) return;

            if (!_excludedApps.Any(x => x.Equals(entry, StringComparison.OrdinalIgnoreCase)))
            {
                _excludedApps.Add(entry);
            }
            TxtExcludeInput.Clear();
            UpdateExcludesPlaceholder();
        }

        private void BtnBrowseExclude_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = LocalizationService.T("BrowseExclude_Title"),
                Filter = LocalizationService.T("BrowseExclude_Filter")
            };

            if (dlg.ShowDialog() == true)
            {
                var fileName = Path.GetFileName(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    !_excludedApps.Any(x => x.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    _excludedApps.Add(fileName);
                    UpdateExcludesPlaceholder();
                }
            }
        }

        private void BtnRemoveExclude_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string item)
            {
                _excludedApps.Remove(item);
                UpdateExcludesPlaceholder();
            }
        }

        private void UpdateExcludesPlaceholder()
        {
            if (TxtNoExcludes != null)
            {
                TxtNoExcludes.Visibility = _excludedApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void BtnSaveApplySettings_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var regionTag = (CmbRegion.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(RegionPreset.GlobalUniversal);
            var brTunnelTag = (CmbBrazilTunnel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(BrazilTunnelMode.BuiltInWarpMasque);
            var profileTag = (CmbProfile.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(BypassProfile.AutoUniversal);
            var dnsTag = (CmbDnsMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(DnsProtectionMode.EncryptedDnsProxy);

            var parsedRegion = Enum.TryParse<RegionPreset>(regionTag, out var r) ? r : RegionPreset.GlobalUniversal;
            var parsedBrTunnel = Enum.TryParse<BrazilTunnelMode>(brTunnelTag, out var bt) ? bt : BrazilTunnelMode.BuiltInWarpMasque;
            var customEndpoint = TxtCustomSocksEndpoint.Text.Trim();

            if (parsedRegion == RegionPreset.Brazil &&
                parsedBrTunnel == BrazilTunnelMode.CustomSocks5 &&
                !RyanDpiEngineService.TryParseEndpoint(customEndpoint, out _, out _))
            {
                ShowNotice(LocalizationService.T("Msg_CustomSocksInvalid"), isSuccess: false);
                return;
            }

            _settings.Region = parsedRegion;
            _settings.RegionConfigured = true;
            _settings.BrazilTunnel = parsedBrTunnel;
            _settings.CustomSocksEndpoint = customEndpoint;
            _settings.CustomSocksUsername = TxtCustomSocksUser.Text.Trim();
            _settings.CustomSocksPassword = TxtCustomSocksPass.Text;
            _settings.BrazilIncludeUdp = ChkBrazilIncludeUdp.IsChecked == true;
            _settings.BrazilIncludeBrowsers = ChkBrazilIncludeBrowsers.IsChecked == true;

            _settings.Profile = Enum.TryParse<BypassProfile>(profileTag, out var parsedProfile)
                ? parsedProfile
                : BypassProfile.AutoUniversal;
            _settings.DnsMode = Enum.TryParse<DnsProtectionMode>(dnsTag, out var parsedDns)
                ? parsedDns
                : DnsProtectionMode.EncryptedDnsProxy;
            _settings.CustomStrategyArgs = TxtCustomArgs.Text.Trim();
            _settings.BypassLan = ChkBypassLan.IsChecked == true;
            _settings.TunnelUdp = ChkTunnelUdp.IsChecked == true;
            _settings.AutoUpdateOnStartup = ChkAutoUpdate.IsChecked == true;
            _settings.ExcludedApps = _excludedApps.ToList();

            BtnRegionPicker.Content = GetRegionShortTag(_settings.Region);

            SetBusy(true, showProgressCard: false);
            BtnSaveApplySettings.Content = LocalizationService.T("Btn_Applying");
            try
            {
                var res = await _engine.ApplySettingsAndRestartAsync(_settings);
                ShowNotice(res.Message, res.Success);
                NavHome.IsChecked = true;
            }
            finally
            {
                BtnSaveApplySettings.Content = LocalizationService.T("Btn_SaveApply");
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        #endregion

        #region Tools & Logs Page

        private async void BtnResetDns_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var confirm = MessageBox.Show(
                LocalizationService.T("Confirm_ResetDnsBody"),
                LocalizationService.T("Confirm_ResetDnsTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, showProgressCard: false);
            try
            {
                var ok = await _engine.Dns.ResetDnsToAutomaticAsync(removeDnsCryptService: true);
                ShowNotice(
                    ok ? LocalizationService.T("Msg_DnsResetOk") : LocalizationService.T("Msg_DnsResetWarn"),
                    ok);
                NavHome.IsChecked = true;
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnCleanLegacy_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var confirm = MessageBox.Show(
                LocalizationService.T("Confirm_CleanLegacyBody"),
                LocalizationService.T("Confirm_CleanLegacyTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, showProgressCard: false);
            try
            {
                var res = await _engine.CleanupLegacyServicesAsync(includeCurrentRyanDpi: false);
                ShowNotice(res.Message, res.Success);
                NavHome.IsChecked = true;
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{LogService.LogsDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogService.Error($"Could not open logs folder: {ex.Message}");
            }
        }

        private void BtnCopyLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtLogs.Text);
                LogService.Info("Logs copied to clipboard.");
            }
            catch
            {
                // Ignore clipboard lock
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogService.ClearBuffer();
            TxtLogs.Clear();
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            await CheckAndApplyUpdateAsync(isManualCheck: true);
        }

        #endregion

        #region Auto-Updater

        private async Task HandlePostUpdateStartupAsync(string updatedVersion)
        {
            var versionLabel = updatedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? updatedVersion
                : $"v{updatedVersion}";

            LogService.Info($"RyanDPI launched after automatic update to {versionLabel}.");

            if (_currentStatus != null && (_currentStatus.IsInstalled ||
                _currentStatus.DetectedLegacyServices.Contains("RyanDPI", StringComparer.OrdinalIgnoreCase)))
            {
                SetBusy(true, showProgressCard: true);
                TxtProgressTitle.Text = LocalizationService.Tf("Update_FinishingTitle", versionLabel);
                TxtProgressStep.Text = LocalizationService.T("Update_SyncingStep");
                TxtProgressDetail.Text = LocalizationService.T("Update_SyncingDetail");
                ProgressInstall.Value = 65;

                try
                {
                    var refreshRes = await _engine.RefreshEngineAfterUpdateAsync(_settings);
                    ProgressInstall.Value = 100;
                    ShowNotice(
                        refreshRes.Success
                            ? LocalizationService.Tf("Update_UpdatedRefreshed", versionLabel)
                            : LocalizationService.Tf("Update_UpdatedWarn", versionLabel, refreshRes.Message),
                        refreshRes.Success);
                }
                finally
                {
                    SetBusy(false);
                    await RefreshStatusAsync();
                }
            }
            else
            {
                ShowNotice(LocalizationService.Tf("Update_UpdatedSimple", versionLabel), isSuccess: true);
            }
        }

        private async Task CheckAndApplyUpdateAsync(bool isManualCheck)
        {
            var ct = _windowCts.Token;
            if (ct.IsCancellationRequested) return;

            if (isManualCheck)
            {
                SetBusy(true, showProgressCard: false);
                BtnCheckUpdates.Content = LocalizationService.T("Btn_Checking");
            }

            UpdateCheckResult check;
            try
            {
                LogService.Info("Checking GitHub Releases for RyanDPI updates...");
                check = await _updater.CheckForUpdateAsync(ct);
            }
            catch
            {
                if (isManualCheck)
                {
                    BtnCheckUpdates.Content = LocalizationService.T("Btn_CheckUpdates");
                    SetBusy(false);
                }
                return;
            }

            if (ct.IsCancellationRequested) return;

            if (!check.Success)
            {
                LogService.Warn($"Update check skipped: {check.ErrorMessage}");
                if (isManualCheck)
                {
                    BtnCheckUpdates.Content = LocalizationService.T("Btn_CheckUpdates");
                    SetBusy(false);
                    ShowNotice(LocalizationService.Tf("Update_CheckFailed", check.ErrorMessage ?? ""), isSuccess: false);
                    NavHome.IsChecked = true;
                }
                return;
            }

            if (!check.UpdateAvailable)
            {
                LogService.Info($"RyanDPI v{check.CurrentVersion} is up to date.");
                if (isManualCheck)
                {
                    BtnCheckUpdates.Content = LocalizationService.T("Btn_CheckUpdates");
                    SetBusy(false);
                    ShowNotice(LocalizationService.Tf("Update_UpToDate", check.CurrentVersion), isSuccess: true);
                    NavHome.IsChecked = true;
                }
                return;
            }

            // If user already started an install/uninstall during silent startup check, don't interrupt it
            if (!isManualCheck && _isBusy)
            {
                LogService.Info($"Update v{check.LatestVersion} available, deferring while another task is running.");
                return;
            }

            LogService.Info($"New RyanDPI update detected: v{check.CurrentVersion} -> v{check.LatestVersion} ({check.AssetName}, mode={check.InstallMode}).");

            NavHome.IsChecked = true;
            PanelNotice.Visibility = Visibility.Collapsed;
            SetBusy(true, showProgressCard: true);
            BtnCheckUpdates.Content = LocalizationService.T("Btn_Updating");
            TxtProgressTitle.Text = LocalizationService.Tf("Update_UpdatingTitle", check.LatestVersion!);
            TxtProgressStep.Text = LocalizationService.T("Update_StartingStep");
            TxtProgressDetail.Text = LocalizationService.Tf("Update_StartingDetail", check.LatestVersion!);
            ProgressInstall.Value = 5;

            var progress = new Progress<UpdateProgress>(u =>
            {
                TxtProgressTitle.Text = u.Title;
                TxtProgressStep.Text = u.StepLabel;
                TxtProgressDetail.Text = u.Detail;
                ProgressInstall.Value = u.Percent;
            });

            try
            {
                var (ok, filePath, err) = await _updater.DownloadAndVerifyUpdateAsync(check, progress, ct);
                if (!ok || string.IsNullOrWhiteSpace(filePath))
                {
                    LogService.Error($"Auto-update download failed: {err}");
                    ShowNotice(LocalizationService.Tf("Update_Failed", check.LatestVersion!, err ?? ""), isSuccess: false);
                    return;
                }

                var launched = _updater.LaunchUpdaterAndPrepareExit(check, filePath);
                if (!launched)
                {
                    ShowNotice(LocalizationService.Tf("Update_LaunchFailed", check.LatestVersion!), isSuccess: false);
                    return;
                }

                // Exit immediately so the detached updater applies the update and relaunches the new version
                Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                // Window closed during download
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    BtnCheckUpdates.Content = LocalizationService.T("Btn_CheckUpdates");
                    SetBusy(false);
                }
            }
        }

        #endregion

        private sealed class SelfTestDisplayItem
        {
            public string Name { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
            public string LatencyText { get; init; } = string.Empty;
            public Brush StatusBrush { get; init; } = Brushes.Gray;
        }
    }
}
