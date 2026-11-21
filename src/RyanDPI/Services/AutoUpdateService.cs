using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace RyanDPI.Services
{
    public enum UpdateInstallMode
    {
        Installer,
        Portable
    }

    public sealed class UpdateCheckResult
    {
        public bool Success { get; init; }
        public bool UpdateAvailable { get; init; }
        public Version CurrentVersion { get; init; } = new(2, 1, 0);
        public Version? LatestVersion { get; init; }
        public string LatestVersionTag { get; init; } = string.Empty;
        public string ReleaseName { get; init; } = string.Empty;
        public string ReleasePageUrl { get; init; } = string.Empty;
        public string AssetName { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
        public long ExpectedSize { get; init; }
        public string? ExpectedSha256 { get; init; }
        public UpdateInstallMode InstallMode { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed class UpdateProgress
    {
        public string Title { get; init; } = "Updating RyanDPI...";
        public string StepLabel { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public double Percent { get; init; }
    }

    public sealed class AutoUpdateService
    {
        public const string GitHubOwner = "CallMeRyanYT";
        public const string GitHubRepo = "RyanDPI";
        public const string LatestReleaseApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        public const string ReleasesPageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";

        private static readonly string[] TrustedDownloadHosts =
        {
            "github.com",
            "objects.githubusercontent.com",
            "release-assets.githubusercontent.com",
            "github-releases.githubusercontent.com"
        };

        public static Version GetCurrentVersion()
        {
            var raw = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 1, 0);
            return NormalizeVersion(raw);
        }

        public static string GetCurrentVersionString()
        {
            var v = GetCurrentVersion();
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        public static Version NormalizeVersion(Version v)
        {
            return new Version(
                Math.Max(0, v.Major),
                Math.Max(0, v.Minor),
                Math.Max(0, v.Build));
        }

        public static Version? ParseReleaseVersion(string? tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return null;

            var cleaned = tagName.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[1..];
            }

            // Strip any pre-release or build metadata suffix
            var dashIdx = cleaned.IndexOfAny(new[] { '-', '+' });
            if (dashIdx > 0)
            {
                cleaned = cleaned[..dashIdx];
            }

            if (!Version.TryParse(cleaned, out var parsed))
            {
                return null;
            }

            return NormalizeVersion(parsed);
        }

        public static bool IsTrustedGitHubDownloadUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;

            var host = uri.Host;
            return TrustedDownloadHosts.Any(trusted =>
                host.Equals(trusted, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + trusted, StringComparison.OrdinalIgnoreCase));
        }

        public static string? ParseSha256Digest(string? digest)
        {
            if (string.IsNullOrWhiteSpace(digest)) return null;
            var trimmed = digest.Trim();
            if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["sha256:".Length..].Trim();
            }

            if (trimmed.Length != 64) return null;
            foreach (var c in trimmed)
            {
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex) return null;
            }

            return trimmed.ToLowerInvariant();
        }

        public static UpdateInstallMode DetectInstallMode(string? currentBaseDir = null)
        {
            try
            {
                var baseDir = NormalizeDirPath(currentBaseDir ?? AppDomain.CurrentDomain.BaseDirectory);
                var registeredPath = GetRegisteredInstallPath();
                if (!string.IsNullOrWhiteSpace(registeredPath) &&
                    baseDir.Equals(NormalizeDirPath(registeredPath), StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateInstallMode.Installer;
                }

                var defaultInstallDir = NormalizeDirPath(RyanDpiEngineService.InstallRootDir);
                if (baseDir.Equals(defaultInstallDir, StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateInstallMode.Installer;
                }
            }
            catch
            {
                // Fall back to portable mode if registry cannot be read
            }

            return UpdateInstallMode.Portable;
        }

        public static string? GetRegisteredInstallPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RyanDPI");
                var installPath = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    return installPath;
                }

                using var uninstKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8D4F62C9-1A7B-4C94-9E35-72A8B1F4209E}_is1");
                var uninstLoc = uninstKey?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(uninstLoc))
                {
                    return uninstLoc;
                }
            }
            catch
            {
                // Ignore registry read error
            }

            return null;
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
        {
            var currentVersion = GetCurrentVersion();
            var mode = DetectInstallMode();

            try
            {
                using var http = CreateHttpClient(TimeSpan.FromSeconds(10));
                using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
                req.Headers.Add("Accept", "application/vnd.github+json");
                req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        CurrentVersion = currentVersion,
                        InstallMode = mode,
                        ErrorMessage = $"GitHub API returned HTTP {(int)resp.StatusCode}."
                    };
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                return ParseReleaseJson(json, currentVersion, mode);
            }
            catch (OperationCanceledException)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    InstallMode = mode,
                    ErrorMessage = "Update check timed out or was cancelled."
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    InstallMode = mode,
                    ErrorMessage = ex.Message
                };
            }
        }

        public static UpdateCheckResult ParseReleaseJson(string json, Version currentVersion, UpdateInstallMode mode)
        {
            var dto = JsonSerializer.Deserialize<GitHubReleaseDto>(json);
            if (dto == null || string.IsNullOrWhiteSpace(dto.TagName))
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    InstallMode = mode,
                    ErrorMessage = "Invalid GitHub release payload."
                };
            }

            if (dto.Draft || dto.Prerelease)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    InstallMode = mode
                };
            }

            var latestVersion = ParseReleaseVersion(dto.TagName);
            if (latestVersion == null)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    InstallMode = mode,
                    ErrorMessage = $"Could not parse release tag '{dto.TagName}'."
                };
            }

            var isNewer = latestVersion > NormalizeVersion(currentVersion);
            if (!isNewer)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    LatestVersionTag = dto.TagName,
                    ReleaseName = dto.Name ?? dto.TagName,
                    ReleasePageUrl = dto.HtmlUrl ?? ReleasesPageUrl,
                    InstallMode = mode
                };
            }

            var (selectedAsset, resolvedMode) = SelectBestAsset(dto.Assets, mode);
            if (selectedAsset == null || !IsTrustedGitHubDownloadUrl(selectedAsset.BrowserDownloadUrl))
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    LatestVersionTag = dto.TagName,
                    InstallMode = mode,
                    ErrorMessage = "No valid installer or portable update package found in the GitHub release."
                };
            }

            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = true,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                LatestVersionTag = dto.TagName,
                ReleaseName = string.IsNullOrWhiteSpace(dto.Name) ? $"RyanDPI v{latestVersion}" : dto.Name,
                ReleasePageUrl = string.IsNullOrWhiteSpace(dto.HtmlUrl) ? ReleasesPageUrl : dto.HtmlUrl,
                AssetName = selectedAsset.Name ?? "RyanDPI-Update",
                DownloadUrl = selectedAsset.BrowserDownloadUrl!,
                ExpectedSize = selectedAsset.Size,
                ExpectedSha256 = ParseSha256Digest(selectedAsset.Digest),
                InstallMode = resolvedMode
            };
        }

        private static (GitHubAssetDto? Asset, UpdateInstallMode ResolvedMode) SelectBestAsset(
            List<GitHubAssetDto>? assets, UpdateInstallMode preferredMode)
        {
            if (assets == null || assets.Count == 0)
            {
                return (null, preferredMode);
            }

            var validAssets = assets
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && IsTrustedGitHubDownloadUrl(a.BrowserDownloadUrl))
                .ToList();

            var setupExe = validAssets.FirstOrDefault(a =>
                               a.Name!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                               a.Name!.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                           ?? validAssets.FirstOrDefault(a =>
                               a.Name!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            var portableZip = validAssets.FirstOrDefault(a =>
                                  a.Name!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                                  a.Name!.Contains("Portable", StringComparison.OrdinalIgnoreCase))
                              ?? validAssets.FirstOrDefault(a =>
                                  a.Name!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (preferredMode == UpdateInstallMode.Installer)
            {
                if (setupExe != null) return (setupExe, UpdateInstallMode.Installer);
                if (portableZip != null) return (portableZip, UpdateInstallMode.Portable);
            }
            else
            {
                if (portableZip != null) return (portableZip, UpdateInstallMode.Portable);
                if (setupExe != null) return (setupExe, UpdateInstallMode.Installer);
            }

            return (null, preferredMode);
        }

        public async Task<(bool Success, string? FilePath, string? ErrorMessage)> DownloadAndVerifyUpdateAsync(
            UpdateCheckResult update,
            IProgress<UpdateProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (!update.UpdateAvailable || string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                return (false, null, "No update download URL provided.");
            }

            if (!IsTrustedGitHubDownloadUrl(update.DownloadUrl))
            {
                return (false, null, "Blocked untrusted download host.");
            }

            var updateDir = Path.Combine(Path.GetTempPath(), "RyanDPI-Update");
            try
            {
                if (Directory.Exists(updateDir))
                {
                    try { Directory.Delete(updateDir, recursive: true); } catch { }
                }
                Directory.CreateDirectory(updateDir);

                var safeName = Path.GetFileName(update.AssetName);
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = update.InstallMode == UpdateInstallMode.Installer
                        ? $"RyanDPI-Setup-{update.LatestVersion}.exe"
                        : $"RyanDPI-Portable-{update.LatestVersion}.zip";
                }

                var partPath = Path.Combine(updateDir, safeName + ".part");
                var finalPath = Path.Combine(updateDir, safeName);

                progress?.Report(new UpdateProgress
                {
                    Title = LocalizationService.Tf("Update_UpdatingTitle", update.LatestVersion!),
                    StepLabel = LocalizationService.T("Update_DownloadingStep"),
                    Detail = LocalizationService.Tf("Update_DownloadingFile", safeName),
                    Percent = 5
                });

                using var http = CreateHttpClient(TimeSpan.FromMinutes(5));
                using var resp = await http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return (false, null, $"Download failed (HTTP {(int)resp.StatusCode}).");
                }

                // Verify final redirected URL is also a trusted GitHub host
                var finalUri = resp.RequestMessage?.RequestUri?.ToString();
                if (!string.IsNullOrWhiteSpace(finalUri) && !IsTrustedGitHubDownloadUrl(finalUri))
                {
                    return (false, null, "Download redirected to an untrusted host.");
                }

                var totalBytes = resp.Content.Headers.ContentLength ?? update.ExpectedSize;
                long receivedBytes = 0;

                await using (var netStream = await resp.Content.ReadAsStreamAsync(ct))
                await using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    var buffer = new byte[65536];
                    int read;
                    var lastReport = Stopwatch.StartNew();

                    while ((read = await netStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        receivedBytes += read;

                        if (lastReport.ElapsedMilliseconds >= 120 || (totalBytes > 0 && receivedBytes >= totalBytes))
                        {
                            lastReport.Restart();
                            var pct = totalBytes > 0
                                ? Math.Min(90, 5 + (receivedBytes * 85.0 / totalBytes))
                                : 50;
                            var mbDown = receivedBytes / (1024.0 * 1024.0);
                            var mbTotal = totalBytes > 0 ? $" / {totalBytes / (1024.0 * 1024.0):F1} MB" : "";
                            progress?.Report(new UpdateProgress
                            {
                                Title = LocalizationService.Tf("Update_UpdatingTitle", update.LatestVersion!),
                                StepLabel = totalBytes > 0 ? $"{(int)(receivedBytes * 100 / totalBytes)}%" : LocalizationService.T("Update_DownloadingStep"),
                                Detail = LocalizationService.Tf("Update_DownloadingPct", safeName, mbDown, mbTotal),
                                Percent = pct
                            });
                        }
                    }
                }

                progress?.Report(new UpdateProgress
                {
                    Title = LocalizationService.Tf("Update_UpdatingTitle", update.LatestVersion!),
                    StepLabel = LocalizationService.T("Update_VerifyingStep"),
                    Detail = LocalizationService.T("Update_VerifyingDetail"),
                    Percent = 94
                });

                var fileInfo = new FileInfo(partPath);
                if (!fileInfo.Exists || fileInfo.Length < 100_000)
                {
                    TryDelete(partPath);
                    return (false, null, "Downloaded update file is unexpectedly small or incomplete.");
                }

                if (update.ExpectedSize > 0 && fileInfo.Length != update.ExpectedSize)
                {
                    TryDelete(partPath);
                    return (false, null, $"Downloaded size mismatch (got {fileInfo.Length}, expected {update.ExpectedSize}).");
                }

                // Verify binary header magic bytes
                var header = new byte[2];
                await using (var fs = File.OpenRead(partPath))
                {
                    if (await fs.ReadAsync(header.AsMemory(0, 2), ct) != 2)
                    {
                        TryDelete(partPath);
                        return (false, null, "Could not read downloaded file header.");
                    }
                }

                bool isExe = safeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                bool isZip = safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                if (isExe && (header[0] != 0x4D || header[1] != 0x5A)) // 'M','Z'
                {
                    TryDelete(partPath);
                    return (false, null, "Downloaded installer is not a valid Windows executable.");
                }
                if (isZip && (header[0] != 0x50 || header[1] != 0x4B)) // 'P','K'
                {
                    TryDelete(partPath);
                    return (false, null, "Downloaded archive is not a valid ZIP file.");
                }

                // Verify SHA-256 digest when provided by GitHub Releases API
                if (!string.IsNullOrWhiteSpace(update.ExpectedSha256))
                {
                    var actualHash = await ComputeFileSha256Async(partPath, ct);
                    if (!actualHash.Equals(update.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDelete(partPath);
                        return (false, null, "SHA-256 checksum verification failed. Update aborted for safety.");
                    }
                    LogService.Info($"Auto-update SHA-256 verified: {actualHash}");
                }

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(partPath, finalPath);

                progress?.Report(new UpdateProgress
                {
                    Title = LocalizationService.Tf("Update_InstallingTitle", update.LatestVersion!),
                    StepLabel = LocalizationService.T("Update_RestartingStep"),
                    Detail = LocalizationService.T("Update_RestartingDetail"),
                    Percent = 100
                });

                return (true, finalPath, null);
            }
            catch (OperationCanceledException)
            {
                return (false, null, "Update download was cancelled.");
            }
            catch (Exception ex)
            {
                LogService.Error($"Update download failed: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Spawns a detached, self-cleaning updater script that waits for the current RyanDPI.exe
        /// process to exit, silently applies the Installer or Portable update, and relaunches the new version.
        /// </summary>
        public bool LaunchUpdaterAndPrepareExit(UpdateCheckResult update, string downloadedFilePath)
        {
            try
            {
                var updateDir = Path.GetDirectoryName(downloadedFilePath)
                                ?? Path.Combine(Path.GetTempPath(), "RyanDPI-Update");
                var scriptPath = Path.Combine(updateDir, "apply_update.ps1");
                var currentPid = Environment.ProcessId;
                var currentExe = Environment.ProcessPath
                                 ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RyanDPI.exe");
                var targetDir = NormalizeDirPath(AppDomain.CurrentDomain.BaseDirectory);
                var newVersionStr = update.LatestVersion?.ToString() ?? update.LatestVersionTag.TrimStart('v', 'V');
                var updaterLogPath = Path.Combine(LogService.LogsDirectory, "updater.log");

                string scriptContent = update.InstallMode == UpdateInstallMode.Installer
                    ? BuildInstallerUpdateScript(currentPid, downloadedFilePath, currentExe, newVersionStr, updateDir, updaterLogPath)
                    : BuildPortableUpdateScript(currentPid, downloadedFilePath, targetDir, currentExe, newVersionStr, updateDir, updaterLogPath);

                File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                // Release single-instance mutex BEFORE launching updater so Inno Setup AppMutex never blocks
                App.ReleaseSingleInstanceMutex();

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = updateDir
                };

                var proc = Process.Start(psi);
                if (proc == null)
                {
                    LogService.Error("Failed to start detached updater process.");
                    return false;
                }

                LogService.Info($"Launched detached auto-updater ({update.InstallMode}) for v{newVersionStr}. Exiting current instance...");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to launch auto-updater: {ex.Message}");
                return false;
            }
        }

        private static string BuildInstallerUpdateScript(
            int parentPid, string installerPath, string fallbackExe, string newVersion, string tempDir, string logPath)
        {
            return $@"
$ErrorActionPreference = 'Continue'
$logFile = '{EscapePsSingleQuoted(logPath)}'
function Write-ULog($msg) {{
    try {{
        $dir = Split-Path -Parent $logFile
        if (-not (Test-Path $dir)) {{ New-Item -ItemType Directory -Path $dir -Force | Out-Null }}
        Add-Content -Path $logFile -Value ""[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"" -Encoding UTF8
    }} catch {{}}
}}

Write-ULog 'Auto-updater started (Installer mode -> v{EscapePsSingleQuoted(newVersion)}). Waiting for PID {parentPid} to exit...'
try {{
    $proc = Get-Process -Id {parentPid} -ErrorAction SilentlyContinue
    if ($proc) {{ $proc.WaitForExit(15000) | Out-Null }}
}} catch {{}}
Start-Sleep -Milliseconds 600

$installer = '{EscapePsSingleQuoted(installerPath)}'
$argsList = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /SP-'
Write-ULog ""Running installer: $installer $argsList""

$exitCode = -1
try {{
    $p = Start-Process -FilePath $installer -ArgumentList $argsList -Wait -PassThru
    $exitCode = $p.ExitCode
    Write-ULog ""Installer finished with exit code: $exitCode""
}} catch {{
    Write-ULog ""Installer launch exception: $($_.Exception.Message)""
}}

$launchExe = '{EscapePsSingleQuoted(fallbackExe)}'
try {{
    $regPath = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\RyanDPI' -Name 'InstallPath' -ErrorAction SilentlyContinue).InstallPath
    if ($regPath) {{
        $candidate = Join-Path $regPath 'RyanDPI.exe'
        if (Test-Path $candidate) {{ $launchExe = $candidate }}
    }}
}} catch {{}}

if (Test-Path $launchExe) {{
    try {{ Start-Process -FilePath 'ie4uinit.exe' -ArgumentList '-show' -WindowStyle Hidden -ErrorAction SilentlyContinue }} catch {{}}
    if ($exitCode -eq 0) {{
        Write-ULog ""Relaunching updated app: $launchExe --updated {EscapePsSingleQuoted(newVersion)}""
        Start-Process -FilePath $launchExe -ArgumentList '--updated {EscapePsSingleQuoted(newVersion)}'
    }} else {{
        Write-ULog ""Installer did not succeed (exit=$exitCode); relaunching existing app.""
        Start-Process -FilePath $launchExe -ArgumentList '--no-update-check'
    }}
}}

Start-Sleep -Seconds 2
try {{ Remove-Item -Path '{EscapePsSingleQuoted(tempDir)}' -Recurse -Force -ErrorAction SilentlyContinue }} catch {{}}
";
        }

        private static string BuildPortableUpdateScript(
            int parentPid, string zipPath, string targetDir, string fallbackExe, string newVersion, string tempDir, string logPath)
        {
            return $@"
$ErrorActionPreference = 'Stop'
$logFile = '{EscapePsSingleQuoted(logPath)}'
function Write-ULog($msg) {{
    try {{
        $dir = Split-Path -Parent $logFile
        if (-not (Test-Path $dir)) {{ New-Item -ItemType Directory -Path $dir -Force | Out-Null }}
        Add-Content -Path $logFile -Value ""[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"" -Encoding UTF8
    }} catch {{}}
}}

Write-ULog 'Auto-updater started (Portable mode -> v{EscapePsSingleQuoted(newVersion)}). Waiting for PID {parentPid} to exit...'
try {{
    $proc = Get-Process -Id {parentPid} -ErrorAction SilentlyContinue
    if ($proc) {{ $proc.WaitForExit(15000) | Out-Null }}
}} catch {{}}
Start-Sleep -Milliseconds 700

$updatedOk = $false
$target = '{EscapePsSingleQuoted(targetDir)}'
try {{
    $extractDir = Join-Path '{EscapePsSingleQuoted(tempDir)}' 'extracted'
    if (Test-Path $extractDir) {{ Remove-Item -Path $extractDir -Recurse -Force }}
    Expand-Archive -Path '{EscapePsSingleQuoted(zipPath)}' -DestinationPath $extractDir -Force

    $exeFound = Get-ChildItem -Path $extractDir -Filter 'RyanDPI.exe' -Recurse | Select-Object -First 1
    if (-not $exeFound) {{ throw 'RyanDPI.exe not found inside portable archive.' }}

    $srcRoot = $exeFound.Directory.FullName
    Write-ULog ""Copying updated portable files from $srcRoot to $target""
    Copy-Item -Path (Join-Path $srcRoot '*') -Destination $target -Recurse -Force
    $updatedOk = $true
    Write-ULog 'Portable files updated successfully.'
}} catch {{
    Write-ULog ""Portable update failed: $($_.Exception.Message)""
}}

$launchExe = Join-Path $target 'RyanDPI.exe'
if (-not (Test-Path $launchExe)) {{ $launchExe = '{EscapePsSingleQuoted(fallbackExe)}' }}
if (Test-Path $launchExe) {{
    try {{ Start-Process -FilePath 'ie4uinit.exe' -ArgumentList '-show' -WindowStyle Hidden -ErrorAction SilentlyContinue }} catch {{}}
    if ($updatedOk) {{
        Start-Process -FilePath $launchExe -ArgumentList '--updated {EscapePsSingleQuoted(newVersion)}'
    }} else {{
        Start-Process -FilePath $launchExe -ArgumentList '--no-update-check'
    }}
}}

Start-Sleep -Seconds 2
try {{ Remove-Item -Path '{EscapePsSingleQuoted(tempDir)}' -Recurse -Force -ErrorAction SilentlyContinue }} catch {{}}
";
        }

        private static string EscapePsSingleQuoted(string input)
        {
            return (input ?? string.Empty).Replace("'", "''");
        }

        private static string NormalizeDirPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };
            var http = new HttpClient(handler)
            {
                Timeout = timeout
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"RyanDPI/{GetCurrentVersionString()}");
            return http;
        }

        private static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Ignore
            }
        }

        private sealed class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubAssetDto>? Assets { get; set; }
        }

        private sealed class GitHubAssetDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("digest")]
            public string? Digest { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}
