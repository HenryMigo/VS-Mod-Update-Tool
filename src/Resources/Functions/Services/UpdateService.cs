using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;

namespace VSSuite.Resources.Functions.Services
{
    public class UpdateService
    {
        // Fallback to "0.0.0" if FileVersion returns null so currentVersionVSModUpdater itself is never null
        private static readonly string currentVersionVSModUpdater =
            FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion ?? "0.0.0";

        public static async Task CheckForUpdatesAsync(string jsonUrl)
        {
            try
            {
                using var client = new HttpClient();
                string response = await client.GetStringAsync(jsonUrl);
                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);

                if (updateInfo?.LatestVersionVSModUpdater == null || updateInfo.DownloadUrlVSModUpdater == null)
                {
                    await MessageService.ShowError("Failed to retrieve valid update information.");
                    return;
                }

                string latestVersion = updateInfo.LatestVersionVSModUpdater;
                string currentVersion = currentVersionVSModUpdater;

                int versionComparison = CompareVersions(currentVersion, latestVersion);

                if (versionComparison < 0)
                {
                    // New version available
                    bool userConfirmed = await MessageService.ShowYesNo("Check For Updates", $"A new version is available: {latestVersion}\n\nLatest Version: {latestVersion}\nYour Version: {currentVersion}\n\nWould you like to download the new version?");

                    if (userConfirmed)
                    {
                        UrlService.OpenUrlAsync(updateInfo.DownloadUrlVSModUpdater);
                    }
                }
                else if (versionComparison > 0)
                {
                    // Easter egg
                    await MessageService.ShowInfo("Check For Updates", $"You're a wizard, harry!\n\nLatest Version: {latestVersion}\nYour Version: {currentVersion}\n\nTell AriesLR he's a goofball and forgot to update the version number.");
                }
                else
                {
                    // Up to date
                    await MessageService.ShowInfo("Check For Updates", $"You are already using the latest version.\n\nLatest Version: {latestVersion}\nYour Version: {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                await MessageService.ShowError($"Failed to check for updates: {ex.Message}");
            }
        }

        public static async Task CheckForUpdatesAsyncSilent(string jsonUrl)
        {
            try
            {
                using var client = new HttpClient();
                string response = await client.GetStringAsync(jsonUrl);
                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);

                if (updateInfo?.LatestVersionVSModUpdater == null || updateInfo.DownloadUrlVSModUpdater == null)
                {
                    await MessageService.ShowError("Failed to retrieve valid update information.");
                    return;
                }

                string latestVersion = updateInfo.LatestVersionVSModUpdater;
                string currentVersion = currentVersionVSModUpdater;

                int versionComparison = CompareVersions(currentVersion, latestVersion);

                if (versionComparison < 0)
                {
                    bool userConfirmed = await MessageService.ShowYesNo("Check For Updates", $"A new version is available: {latestVersion}\n\nLatest Version: {latestVersion}\nYour Version: {currentVersion}\n\nWould you like to download the new version?");

                    if (userConfirmed)
                    {
                        UrlService.OpenUrlAsync(updateInfo.DownloadUrlVSModUpdater);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check for updates: {ex.Message}");
            }
        }

        private static int CompareVersions(string currentVersion, string latestVersion)
        {
            var currentParts = currentVersion.Split('.');
            var latestParts = latestVersion.Split('.');

            int maxLength = Math.Max(currentParts.Length, latestParts.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int currentPart = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                int latestPart = i < latestParts.Length ? int.Parse(latestParts[i]) : 0;

                if (currentPart < latestPart) return -1;
                if (currentPart > latestPart) return 1;
            }

            return 0;
        }

        public class UpdateInfo
        {
            public string? LatestVersionVSModUpdater { get; set; }
            public string? DownloadUrlVSModUpdater { get; set; }
        }
    }
}