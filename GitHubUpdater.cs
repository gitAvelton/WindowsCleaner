using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace WindowsCleaner
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }

    public static class GitHubUpdater
    {
        private const string Owner = "gitAvelton";
        private const string Repo = "WindowsCleaner";

        private static readonly HttpClient _httpClient = new HttpClient();

        static GitHubUpdater()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsCleaner-App");
        }

        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var result = new UpdateInfo();
            try
            {
                string apiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                string json = await _httpClient.GetStringAsync(apiUrl);

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string cleanVersionStr = tagName.TrimStart('v', 'V');

                if (Version.TryParse(cleanVersionStr, out Version? latestVersion))
                {
                    Version currentVersion = GetCurrentVersion();
                    Version normalizedCurrent = new Version(currentVersion.Major, currentVersion.Minor, Math.Max(0, currentVersion.Build));
                    Version normalizedLatest = new Version(latestVersion.Major, latestVersion.Minor, Math.Max(0, latestVersion.Build));

                    if (normalizedLatest > normalizedCurrent)
                    {
                        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
                        {
                            string assetName = asset.GetProperty("name").GetString() ?? "";
                            if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                result.IsUpdateAvailable = true;
                                result.LatestVersion = tagName;
                                result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                result.HtmlUrl = root.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() ?? "" : "";
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Bei fehlender Internetverbindung still fehlschlagen
            }
            return result;
        }

        public static async Task DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            string tempInstallerPath = Path.Combine(Path.GetTempPath(), "WindowsCleaner-Setup-Update.exe");

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalBytesRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double percentage = (double)totalBytesRead / totalBytes * 100.0;
                        progress?.Report(percentage);
                    }
                }
            }

            progress?.Report(100.0);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempInstallerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
    }
}