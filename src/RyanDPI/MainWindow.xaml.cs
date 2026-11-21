using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        private bool _lastNoticeIsSuccess = true;
        private List<SelfTestCheckResult> _lastSelfTestRaw = new();

        private readonly ObservableCollection<string> _excludedApps = new();
        private readonly ObservableCollection<SelfTestDisplayItem> _selfTestItems = new();

        public MainWindow()
        {
            InitializeComponent();

            _settings = RyanDpiSettings.Load();
            App.ApplyTheme(_settings.IsDarkTheme);
            UpdateThemeIcon();

            TxtAboutVersion.Text = $"RyanDPI v{AutoUpdateService.GetCurrentVersionString()} — System-Wide Zero-Speed-Loss DPI Bypass";

            ListExcludes.ItemsSource = _excludedApps;
            ListSelfTestResults.ItemsSource = _selfTestItems;

            LoadSettingsIntoUI();

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

        #region Window & Navigation Controls

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

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
            var mutedBrush = (Brush)FindResource("TextMutedBrush");
            var cardAltBrush = (Brush)FindResource("BgCardAltBrush");
            var borderBrush = (Brush)FindResource("BorderSubtleBrush");

            // Hero orb & headline
            if (status.IsFullyActive)
            {
                StatusOrbOuter.Background = successBg;
                StatusOrbOuter.BorderBrush = successBrush;
                StatusOrbIcon.Fill = successBrush;
                StatusOrbIcon.Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");

                TxtStatusHeadline.Text = "RyanDPI is Active";
                TxtStatusSubtitle.Text = "Every app & website is unlocked automatically at full native internet speed.";

                BtnPrimaryAction.Content = "Pause RyanDPI";
                BtnPrimaryAction.Style = (Style)FindResource("SuccessButtonStyle");
                TxtActionCaption.Text = "Running in background as a Windows service — stays active automatically after every restart.";
                PanelInstalledActions.Visibility = Visibility.Visible;
            }
            else if (status.IsInstalled)
            {
                StatusOrbOuter.Background = warnBg;
                StatusOrbOuter.BorderBrush = warnBrush;
                StatusOrbIcon.Fill = warnBrush;
                StatusOrbIcon.Data = Geometry.Parse("M13 2L4 14h7l-1 8 9-12h-7l1-8z");

                TxtStatusHeadline.Text = status.IsPartiallyRunning ? "RyanDPI Partially Active" : "RyanDPI is Paused";
                TxtStatusSubtitle.Text = status.IsPartiallyRunning
                    ? "One of the background services is stopped. Click below to start both cleanly."
                    : "RyanDPI is installed and ready. Click below to re-enable instant access.";

                BtnPrimaryAction.Content = "Enable RyanDPI";
                BtnPrimaryAction.Style = (Style)FindResource("PrimaryButtonStyle");
                TxtActionCaption.Text = "Enables local DPI bypass and system-wide transparent app routing with zero VPN slowdown.";
                PanelInstalledActions.Visibility = Visibility.Visible;
            }
            else
            {
                StatusOrbOuter.Background = cardAltBrush;
                StatusOrbOuter.BorderBrush = borderBrush;
                StatusOrbIcon.Fill = (Brush)FindResource("AccentCyanBrush");
                StatusOrbIcon.Data = Geometry.Parse("M13 2L4 14h7l-1 8 9-12h-7l1-8z");

                TxtStatusHeadline.Text = "Ready to Unlock Everything";
                TxtStatusSubtitle.Text = status.HasLegacyServices
                    ? $"Legacy DPI detected ({string.Join(", ", status.DetectedLegacyServices)}). RyanDPI will replace it cleanly."
                    : "One-click setup unlocks Discord, Roblox, Roblox Studio & every blocked app/site in Turkey.";

                BtnPrimaryAction.Content = "Install & Enable RyanDPI";
                BtnPrimaryAction.Style = (Style)FindResource("PrimaryButtonStyle");
                TxtActionCaption.Text = "100% local DPI bypass — no VPN servers, no bandwidth throttling, no app whitelisting needed.";
                PanelInstalledActions.Visibility = Visibility.Collapsed;
            }

            // 3 Health Pills
            SetPill(DotEngine, TxtEngineState, status.EngineState, "Running", "Stopped", "Not Set");
            SetPill(DotRouter, TxtRouterState, status.RouterState, "All Apps", "Paused", "Not Set");

            if (status.DnsProxyState == ComponentState.Running)
            {
                DotDns.Fill = successBrush;
                TxtDnsState.Text = "Encrypted";
            }
            else if (_settings.DnsMode == DnsProtectionMode.WindowsNativeDoH && status.IsInstalled)
            {
                DotDns.Fill = successBrush;
                TxtDnsState.Text = "Native DoH";
            }
            else if (status.DnsProxyState == ComponentState.Stopped)
            {
                DotDns.Fill = warnBrush;
                TxtDnsState.Text = "Stopped";
            }
            else
            {
                DotDns.Fill = mutedBrush;
                TxtDnsState.Text = _settings.DnsMode == DnsProtectionMode.KeepSystemDns ? "Custom" : "Default";
            }

            // Update Tools legacy button label if legacy services exist
            BtnCleanLegacy.Content = status.HasLegacyServices
                ? $"Clean Legacy DPI ({status.DetectedLegacyServices.Count})"
                : "Clean Legacy DPI Services";
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
            TxtProgressTitle.Text = "Installing RyanDPI...";
            TxtProgressStep.Text = "Starting...";
            TxtProgressDetail.Text = "Initializing installation...";

            var progress = new Progress<InstallProgressUpdate>(u =>
            {
                TxtProgressTitle.Text = u.Title;
                TxtProgressStep.Text = $"Step {u.StepIndex} / {u.TotalSteps}";
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
                "Completely remove RyanDPI services, firewall rules, and reset DNS back to automatic (DHCP)?",
                "Uninstall RyanDPI",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, showProgressCard: true);
            PanelNotice.Visibility = Visibility.Collapsed;
            TxtProgressTitle.Text = "Uninstalling RyanDPI";
            TxtProgressStep.Text = "Cleaning up...";
            TxtProgressDetail.Text = "Stopping services, removing firewall rules, and restoring automatic DNS...";
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
                "Restart your computer now? Make sure your work is saved.",
                "Restart PC",
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
            BtnRunSelfTest.Content = "Testing...";
            TxtSelfTestPlaceholder.Visibility = Visibility.Collapsed;
            _selfTestItems.Clear();

            try
            {
                var results = await _engine.RunSelfTestAsync();
                _lastSelfTestRaw = results;
                RenderSelfTestResults(results);
            }
            catch (Exception ex)
            {
                LogService.Error($"Self-test error: {ex.Message}");
            }
            finally
            {
                BtnRunSelfTest.Content = "Run Test";
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
                    LatencyText = r.Passed ? (r.LatencyMs <= 0 ? "<1 ms" : $"{r.LatencyMs} ms") : "Failed",
                    StatusBrush = r.Passed ? okBrush : failBrush
                });
            }
        }

        #endregion

        #region Settings Page

        private void LoadSettingsIntoUI()
        {
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
            UpdateProfileDescription();
            UpdateDnsDescription();
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
            UpdateProfileDescription();
        }

        private void UpdateProfileDescription()
        {
            if (TxtProfileDescription == null || PanelCustomArgs == null) return;
            var tag = (CmbProfile.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            PanelCustomArgs.Visibility = tag == nameof(BypassProfile.Custom) ? Visibility.Visible : Visibility.Collapsed;
            TxtProfileDescription.Text = tag switch
            {
                nameof(BypassProfile.ClassicByeDpi) =>
                    "Uses the lightweight zero-retransmit TLS record split strategy (--tlsrec 1+s --mod-http=h,d) with automatic split/disorder fallback and 128 KB full-speed buffers.",
                nameof(BypassProfile.StrictIsp) =>
                    "Starts immediately with TLS record fragmentation + TCP disorder + OOB + low-TTL fake packets for aggressive ISPs like Superonline or mobile carriers.",
                nameof(BypassProfile.Custom) =>
                    "Enter custom ciadpi.exe desync flags below. High-speed socket flags (-i 127.0.0.1 -p 1080 -c 4096 -b 131072) are prepended automatically.",
                _ =>
                    "6-stage adaptive strategy built for all Turkish ISPs (Türk Telekom, Superonline, Vodafone, Kablonet, TurkNet) with 128 KB full-speed buffers and 24-hour per-IP caching."
            };
        }

        private void CmbDnsMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDnsDescription();
        }

        private void UpdateDnsDescription()
        {
            if (TxtDnsDescription == null) return;
            var tag = (CmbDnsMode.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            TxtDnsDescription.Text = tag switch
            {
                nameof(DnsProtectionMode.WindowsNativeDoH) =>
                    "Uses Windows 11 built-in DNS-over-HTTPS with Google (8.8.8.8) and Quad9 (9.9.9.9) without running a local DNS proxy.",
                nameof(DnsProtectionMode.KeepSystemDns) =>
                    "Leaves your network adapter DNS untouched. Choose this if you already manage your own encrypted DNS (e.g. NextDNS, AdGuard, or router DoH).",
                _ =>
                    "Runs local encrypted DNS (DNSCrypt-Proxy) via Google, Cloudflare & Quad9 with a 4,096-entry cache. Defeats ISP DNS poisoning on both Windows 10 and 11."
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
                Title = "Select an application to exclude from RyanDPI",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
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

            var profileTag = (CmbProfile.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(BypassProfile.AutoUniversal);
            var dnsTag = (CmbDnsMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? nameof(DnsProtectionMode.EncryptedDnsProxy);

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

            SetBusy(true, showProgressCard: false);
            BtnSaveApplySettings.Content = "Applying...";
            try
            {
                var res = await _engine.ApplySettingsAndRestartAsync(_settings);
                ShowNotice(res.Message, res.Success);
                NavHome.IsChecked = true;
            }
            finally
            {
                BtnSaveApplySettings.Content = "Save & Apply Settings";
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
                "Reset DNS and DoH settings on all physical network adapters back to Automatic (DHCP) and flush the DNS cache?",
                "Reset DNS & Flush Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, showProgressCard: false);
            try
            {
                var ok = await _engine.Dns.ResetDnsToAutomaticAsync(removeDnsCryptService: true);
                ShowNotice(
                    ok ? "DNS & DoH reset to Automatic (DHCP) and DNS cache flushed." : "DNS reset finished with warnings. Check logs.",
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
                "Stop and remove any conflicting third-party DPI or tunneling services (ByeDPI, GoodbyeDPI, Zapret, WinDivert, WireSock) and Discord drover leftovers?\n\nYour active RyanDPI installation will not be affected.",
                "Clean Legacy DPI Services",
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
                TxtProgressTitle.Text = $"Finishing update to {versionLabel}...";
                TxtProgressStep.Text = "Syncing engine";
                TxtProgressDetail.Text = "Updating background RyanDPI engine binaries, config, and firewall rules...";
                ProgressInstall.Value = 65;

                try
                {
                    var refreshRes = await _engine.RefreshEngineAfterUpdateAsync(_settings);
                    ProgressInstall.Value = 100;
                    ShowNotice(
                        refreshRes.Success
                            ? $"RyanDPI automatically updated to {versionLabel} and refreshed background services!"
                            : $"Updated to {versionLabel}: {refreshRes.Message}",
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
                ShowNotice($"RyanDPI automatically updated to {versionLabel}!", isSuccess: true);
            }
        }

        private async Task CheckAndApplyUpdateAsync(bool isManualCheck)
        {
            var ct = _windowCts.Token;
            if (ct.IsCancellationRequested) return;

            if (isManualCheck)
            {
                SetBusy(true, showProgressCard: false);
                BtnCheckUpdates.Content = "Checking...";
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
                    BtnCheckUpdates.Content = "Check Updates";
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
                    BtnCheckUpdates.Content = "Check Updates";
                    SetBusy(false);
                    ShowNotice($"Could not check for updates: {check.ErrorMessage}", isSuccess: false);
                    NavHome.IsChecked = true;
                }
                return;
            }

            if (!check.UpdateAvailable)
            {
                LogService.Info($"RyanDPI v{check.CurrentVersion} is up to date.");
                if (isManualCheck)
                {
                    BtnCheckUpdates.Content = "Check Updates";
                    SetBusy(false);
                    ShowNotice($"You are on the latest version (RyanDPI v{check.CurrentVersion}).", isSuccess: true);
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
            BtnCheckUpdates.Content = "Updating...";
            TxtProgressTitle.Text = $"Updating RyanDPI to v{check.LatestVersion}...";
            TxtProgressStep.Text = "Starting";
            TxtProgressDetail.Text = $"Found new release v{check.LatestVersion}. Downloading update automatically...";
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
                    ShowNotice($"Automatic update to v{check.LatestVersion} failed: {err}", isSuccess: false);
                    return;
                }

                var launched = _updater.LaunchUpdaterAndPrepareExit(check, filePath);
                if (!launched)
                {
                    ShowNotice($"Downloaded v{check.LatestVersion}, but could not launch the installer automatically.", isSuccess: false);
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
                    BtnCheckUpdates.Content = "Check Updates";
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
