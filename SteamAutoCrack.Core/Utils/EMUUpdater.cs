using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SteamAutoCrack.Core.Utils
{
    public class EMUUpdater
    {
        private const string GoldbergReleaseUrl = "https://api.github.com/repos/Detanup01/gbe_fork/releases";
        public static bool Downloading;
        private readonly ILogger _log;

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        static EMUUpdater()
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");
            }
        }

        private bool _bInited;
        private string _currentVersion = string.Empty;
        private string _latestVersion = string.Empty;
        private string _downloadUrl = string.Empty;
        private long _expectedSize = 0;
        private string _expectedSha256 = string.Empty;

        public EMUUpdater()
        {
            _log = Log.ForContext<EMUUpdater>();
        }

        public async Task Init()
        {
            _currentVersion = GetCurrentGoldbergVersion();
            _bInited = await FetchLatestReleaseInfo().ConfigureAwait(false);
        }

        public async Task<bool> Download(bool force = false)
        {
            if (Downloading)
            {
                _log.Information("Already Downloading Goldberg Emulator...");
                return false;
            }

            if (!_bInited)
            {
                _log.Error("EMUUpdater is not initialized or failed to get latest release information from GitHub.");
                return false;
            }

            Downloading = true;
            try
            {
                _log.Information("Goldberg version: Current: {Current}; Latest: {Latest}",
                    string.IsNullOrEmpty(_currentVersion) ? "None" : _currentVersion, _latestVersion);

                if (!force && _currentVersion.Equals(_latestVersion, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Information("Goldberg emulator already updated to latest version.");
                    return true;
                }

                if (!Directory.Exists(Config.Config.TempPath))
                    Directory.CreateDirectory(Config.Config.TempPath);

                var tempArchiveFile = Path.Combine(Config.Config.TempPath, "Goldberg.7z");

                _log.Information("Starting download Goldberg Emulator from {Url}...", _downloadUrl);
                await DownloadFileAsync(_downloadUrl, tempArchiveFile, _expectedSize).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(_expectedSha256))
                {
                    _log.Debug("Verifying downloaded file SHA256...");
                    var actualSha256 = ComputeSha256(tempArchiveFile);
                    _log.Debug("Downloaded SHA256: {Actual}, Expected: {Expected}", actualSha256, _expectedSha256);

                    if (!actualSha256.Equals(_expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(tempArchiveFile)) File.Delete(tempArchiveFile);
                        throw new InvalidDataException($"SHA256 mismatch! Expected: {_expectedSha256}, Actual: {actualSha256}");
                    }
                    _log.Debug("SHA256 verification passed.");
                }

                _log.Information("Extracting Goldberg Emulator...");
                await SafeExtractAndDeploy(tempArchiveFile).ConfigureAwait(false);

                var versionFile = Path.Combine(Config.Config.GoldbergPath, "version");
                await File.WriteAllTextAsync(versionFile, _latestVersion).ConfigureAwait(false);
                var oldCommitFile = Path.Combine(Config.Config.GoldbergPath, "commit_id");
                if (File.Exists(oldCommitFile))
                {
                    try { File.Delete(oldCommitFile); } catch { /* ignore */ }
                }

                _log.Information("Goldberg Emulator updated successfully to {Version}.", _latestVersion);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error occurred while updating Goldberg Emulator.");
                return false;
            }
            finally
            {
                Downloading = false;
            }
        }

        private async Task<bool> FetchLatestReleaseInfo()
        {
            try
            {
                _log.Information("Fetching latest release info from GitHub...");
                using var response = await _httpClient.GetAsync(GoldbergReleaseUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _log.Error("Failed to fetch GitHub release, status code: {StatusCode}", response.StatusCode);
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                JsonElement latestRelease;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    if (doc.RootElement.GetArrayLength() == 0)
                    {
                        _log.Error("No releases found.");
                        return false;
                    }
                    latestRelease = doc.RootElement[0];
                }
                else
                {
                    latestRelease = doc.RootElement;
                }

                _latestVersion = latestRelease.GetProperty("tag_name").GetString() ?? string.Empty;

                if (latestRelease.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        if (string.Equals(name, "emu-win-release.7z", StringComparison.OrdinalIgnoreCase))
                        {
                            _downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                            _expectedSize = asset.GetProperty("size").GetInt64();

                            if (asset.TryGetProperty("digest", out var digestProp))
                            {
                                var digest = digestProp.GetString() ?? string.Empty;
                                if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                                {
                                    _expectedSha256 = digest["sha256:".Length..].Trim();
                                }
                                else
                                {
                                    _expectedSha256 = digest.Trim();
                                }
                            }
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(_downloadUrl))
                {
                    _log.Error("Target asset 'emu-win-release.7z' not found in release.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception when fetching latest Goldberg version.");
                return false;
            }
        }

        private static async Task DownloadFileAsync(string url, string destinationPath, long expectedSize)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var fileStream = File.Create(destinationPath))
            {
                await using var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await downloadStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            var fileInfo = new FileInfo(destinationPath);
            if (expectedSize > 0 && fileInfo.Length != expectedSize)
            {
                if (fileInfo.Exists) fileInfo.Delete();
                throw new InvalidDataException($"Downloaded file size mismatch. Expected: {expectedSize} bytes, Actual: {fileInfo.Length} bytes.");
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private async Task SafeExtractAndDeploy(string archivePath)
        {
            await Task.Run(() =>
            {
                var stagingDir = Path.Combine(Config.Config.TempPath, "Goldberg_Staging_" + Guid.NewGuid().ToString("N")[..8]);
                try
                {
                    if (Directory.Exists(stagingDir))
                        Directory.Delete(stagingDir, true);
                    Directory.CreateDirectory(stagingDir);

                    using (var archive = SevenZipArchive.OpenArchive(archivePath, ReaderOptions.ForFilePath))
                    {
                        archive.WriteToDirectory(stagingDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }

                    var sourceDir = Directory.Exists(Path.Combine(stagingDir, "release"))
                        ? Path.Combine(stagingDir, "release")
                        : stagingDir;

                    var requiredFiles = new[]
                    {
                        Path.Combine(sourceDir, "regular", "x64", "steam_api64.dll"),
                        Path.Combine(sourceDir, "regular", "x86", "steam_api.dll")
                    };

                    foreach (var file in requiredFiles)
                    {
                        if (!File.Exists(file))
                        {
                            throw new FileNotFoundException($"Verification failed: essential emulator file '{Path.GetFileName(file)}' not found in extracted archive.");
                        }
                    }

                    if (!Directory.Exists(Config.Config.GoldbergPath))
                        Directory.CreateDirectory(Config.Config.GoldbergPath);

                    CopyDirectory(new DirectoryInfo(sourceDir), new DirectoryInfo(Config.Config.GoldbergPath));
                }
                finally
                {
                    if (Directory.Exists(stagingDir))
                    {
                        try { Directory.Delete(stagingDir, true); } catch { /* ignore */ }
                    }
                }
            }).ConfigureAwait(false);
        }

        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            foreach (var fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            foreach (var diSourceSubDir in source.GetDirectories())
            {
                if (diSourceSubDir.Name.Equals("release", StringComparison.OrdinalIgnoreCase))
                    continue;

                var nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyDirectory(diSourceSubDir, nextTargetSubDir);
            }
        }

        private string GetCurrentGoldbergVersion()
        {
            try
            {
                var path = Path.Combine(Config.Config.GoldbergPath, "version");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Config.Config.GoldbergPath, "commit_id");
                }
                return File.Exists(path) ? File.ReadLines(path).FirstOrDefault() ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to read current Goldberg version.");
                return string.Empty;
            }
        }
    }
}
