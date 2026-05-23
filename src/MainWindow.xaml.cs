using MahApps.Metro.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSSuite.Resources.Functions.Services;
using VSSuite.Resources.ViewModels;

namespace VSSuite
{
    public partial class MainWindow : MetroWindow
    {
        // Github Backend URLs
        private const string UpdateUrl = "https://raw.githubusercontent.com/AriesLR/VSSuite/refs/heads/main/docs/version/update.json";

        private const string GameVersionsUrl = "https://raw.githubusercontent.com/AriesLR/VSSuite/refs/heads/main/docs/version/gameversions.json";

        private const string ChangelogUrl = "https://raw.githubusercontent.com/AriesLR/VSSuite/refs/heads/main/docs/version/CHANGELOG.md";

        // Dependency URLs
        private const string WebView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        // Mod Browser URLs
        public static readonly string VSModRepoHomeUrl = "https://mods.vintagestory.at/list/mod";

        public static readonly Uri VSModRepoHomeUrlUri = new(VSModRepoHomeUrl);

        // Other URLs
        private const string GithubRepoUrl = "https://github.com/AriesLR/VSSuite";

        private const string BuyMeACoffeeUrl = "https://buymeacoffee.com/arieslr";

        private const string PatreonUrl = "https://www.patreon.com/c/arieslr/membership";

        public static string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version}";

        private static readonly string appConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AriesLR", "VSSuite");

        private static readonly string legacyConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AriesLR", "VSModUpdateTool");

        private static readonly string modlinksPath = Path.Combine(appConfigFolder, "ModLinks.json");

        private static readonly string appSettingsPath = Path.Combine(appConfigFolder, "AppSettings.json");

        private AppSettings CurrentSettings { get; set; } = new AppSettings();

        private bool _isLoaded = false;

        private bool _isBrowserInitialized = false;

        private string? selectedModPath;

        private int processedModsChecked;

        private ICollectionView? _modsView;

        public MainWindow()
        {
            // Ensure config directory exists
            if (!Directory.Exists(appConfigFolder))
                Directory.CreateDirectory(appConfigFolder);

            MigrateLegacyUserData();

            InitializeComponent();
            DataContext = new MainWindowViewModel();

            this.Loaded += (s, e) =>
            {
                this.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    InitializeAppSettings();

                    await LoadGameVersionsAsync();

                    await PromptOnStart();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            };
        }

        // ============ Title Bar Buttons ============

        // Open Github Repo
        private void OpenGithubRepo_Click(object sender, RoutedEventArgs e)
        {
            UrlService.OpenUrlAsync(GithubRepoUrl);
        }

        // ============ App Initialization ============

        // Init App Settings
        private void InitializeAppSettings()
        {
            _isLoaded = false;

            CurrentSettings = LoadAppSettings() ?? new AppSettings();

            // Populate full config if loading a legacy config
            if (string.IsNullOrWhiteSpace(CurrentSettings.DefaultGameVersion))
            {
                CurrentSettings.DefaultGameVersion = "1.22.x";
                CurrentSettings.SafeMode = true;
                CurrentSettings.CheckForUpdatesOnStartup = true;
                CurrentSettings.ShowIgnoreInTable = true;
                CurrentSettings.ShowModPagesInTable = true;
                CurrentSettings.ShowFileNamesInTable = true;
                CurrentSettings.ShowGameVersionInTable = true;

                SaveAppSettings(CurrentSettings);
            }

            if (!string.IsNullOrWhiteSpace(CurrentSettings.ModsFolderPath))
            {
                ModsFolderTextBox.Text = CurrentSettings.ModsFolderPath;
                selectedModPath = CurrentSettings.ModsFolderPath;
            }

            DefaultGameVersionDropdown.Text = CurrentSettings.DefaultGameVersion;
            SafeModeToggle.IsOn = CurrentSettings.SafeMode;
            CheckAppUpdatesToggle.IsOn = CurrentSettings.CheckForUpdatesOnStartup;

            ShowIgnoreToggle.IsOn = CurrentSettings.ShowIgnoreInTable;
            ShowModPagesToggle.IsOn = CurrentSettings.ShowModPagesInTable;
            ShowFileNamesToggle.IsOn = CurrentSettings.ShowFileNamesInTable;
            ShowGameVersionToggle.IsOn = CurrentSettings.ShowGameVersionInTable;

            IgnoreColumn.Visibility = CurrentSettings.ShowIgnoreInTable ? Visibility.Visible : Visibility.Collapsed;
            ModPagesColumn.Visibility = CurrentSettings.ShowModPagesInTable ? Visibility.Visible : Visibility.Collapsed;
            FileNameColumn.Visibility = CurrentSettings.ShowFileNamesInTable ? Visibility.Visible : Visibility.Collapsed;
            GameVersionColumn.Visibility = CurrentSettings.ShowGameVersionInTable ? Visibility.Visible : Visibility.Collapsed;

            _isLoaded = true;

            if (CurrentSettings.CheckForUpdatesOnStartup)
            {
                UpdateCheckOnStartup();
            }
        }

        // Check for Updates on Start - Silent
        private static async void UpdateCheckOnStartup()
        {
            await UpdateService.CheckForUpdatesAsyncSilent(UpdateUrl);
        }

        // Splash screen / folder browser
        private async Task PromptOnStart()
        {
            string? savedPath = CurrentSettings?.ModsFolderPath;

            if (!string.IsNullOrWhiteSpace(savedPath) && Directory.Exists(savedPath))
            {
                // Load saved path
                ModsFolderTextBox.Text = savedPath;
                await LoadModsFromFolderAsync(savedPath);
                selectedModPath = savedPath;
                return;
            }

            // If no valid saved path, prompt user to select folder
            bool userConfirmed = await MessageService.ShowBrowseCancel("Where are your mods?", "Please select the folder where your mods are stored.");

            if (userConfirmed)
            {
                await SelectModsFolderAsync();
            }
            else
            {
                selectedModPath = null;
            }
        }

        // ============ App Settings ============

        // Load AppSettings.json
        private static AppSettings? LoadAppSettings()
        {
            if (!File.Exists(appSettingsPath))
                return null;

            try
            {
                string json = File.ReadAllText(appSettingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json);
            }
            catch
            {
                return null;
            }
        }

        // Save AppSettings.json
        private static void SaveAppSettings(AppSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(appSettingsPath, json);
        }

        // ============ Button Clicks ============

        // Open Buy Me A Coffee Button
        private void OpenBuyMeACoffee_Click(object sender, RoutedEventArgs e)
        {
            UrlService.OpenUrlAsync(BuyMeACoffeeUrl);
        }

        // Open Patreon Button
        private void OpenPatreon_Click(object sender, RoutedEventArgs e)
        {
            UrlService.OpenUrlAsync(PatreonUrl);
        }

        // Check For App Updates Button
        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await UpdateService.CheckForUpdatesAsync(UpdateUrl);
        }

        // Clean Mods Folder Button
        private async void CleanModsFolder_Click(object sender, RoutedEventArgs e)
        {
            await CleanModsFolderAsync();
        }

        // Remove Duplicate Mods Button
        private async void RemoveDuplicateMods_Click(object sender, RoutedEventArgs e)
        {
            await RemoveDuplicateModsAsync();
        }

        // Refresh Mods Folder Button
        private async void RefreshMods_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(selectedModPath) && Directory.Exists(selectedModPath))
                await LoadModsFromFolderAsync(selectedModPath);
            else
                await MessageService.ShowError("No mods folder selected.");
        }

        // Browse Folder Button
        private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            await SelectModsFolderAsync();
        }

        // Check For Mod Updates Button
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForModUpdatesLoud();
        }

        // Updates All Mods Button
        private async void UpdateAllModsButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = await MessageService.ShowYesCancel("Confirm Update", "Are you sure you want to update all mods?");

            if (result)
            {
                await UpdateAllModsAsync();
            }
            else
            {
                // Cancel
            }
        }

        // Update Single Mod Button
        private async void UpdateModButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ModInfo mod)
            {
                try
                {
                    await UpdateSingleModAsync(mod);
                }
                catch (Exception ex)
                {
                    await MessageService.ShowError($"Failed to update {mod.Name}: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("Failed to get ModInfo from DataContext");
            }
        }

        // Open Mod Page Button
        private async void OpenModPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ModInfo mod)
            {
                if (!string.IsNullOrWhiteSpace(mod.ModPageUrl))
                {
                    try
                    {
                        UrlService.OpenUrlAsync($"{mod.ModPageUrl}");
                    }
                    catch (Exception ex)
                    {
                        await MessageService.ShowError($"Failed to open URL: {ex.Message}");
                    }
                }
            }
            else
            {
                Debug.WriteLine("Failed to get ModInfo from DataContext");
            }
        }

        // Copy Modlist to Clipboard with Discord Markdown
        private async void CopyModlistMarkdown_Click(object sender, RoutedEventArgs e)
        {
            if (ModsDataGrid.ItemsSource is List<ModInfo> mods && mods.Count > 0)
            {
                // Handle user not checking for updates first (need to check to get urls)
                if (mods.Any(m => string.IsNullOrWhiteSpace(m.ModPageUrl)))
                {
                    await CheckForModUpdatesSilent();

                    mods = (ModsDataGrid.ItemsSource as List<ModInfo>)!;
                }

                // Build modlist + markdown
                var modlistMarkdown = string.Join(Environment.NewLine,
                    mods
                        .Where(m => !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.ModPageUrl))
                        .Select(m => $"- [{m.Name}]({m.ModPageUrl})")
                );

                if (!string.IsNullOrWhiteSpace(modlistMarkdown))
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(modlistMarkdown);
                        await MessageService.ShowInfo("Success", $"Total Mods: {processedModsChecked}\n\nModlist was copied to clipboard using markdown formatting.");
                    }
                    catch (Exception ex)
                    {
                        await MessageService.ShowError($"Failed to copy to clipboard: {ex.Message}");
                    }
                }
                else
                {
                    await MessageService.ShowError("No mod(s) had valid names/URLs to copy.");
                }
            }
            else
            {
                Debug.WriteLine("ModsDataGrid.ItemsSource was empty or null");
            }
        }

        // Copy Modlist to Clipboard
        private async void CopyModlist_Click(object sender, RoutedEventArgs e)
        {
            if (ModsDataGrid.ItemsSource is List<ModInfo> mods && mods.Count > 0)
            {
                // Handle user not checking for updates first (need to check to get urls)
                if (mods.Any(m => string.IsNullOrWhiteSpace(m.ModPageUrl)))
                {
                    await CheckForModUpdatesSilent();

                    mods = (ModsDataGrid.ItemsSource as List<ModInfo>)!;
                }

                // Build modlist + format
                var modlistNoMarkdown = string.Join(Environment.NewLine,
                    mods
                        .Where(m => !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.ModPageUrl))
                        .Select(m => $"Mod: {m.Name} Mod Page: {m.ModPageUrl}")
                );

                if (!string.IsNullOrWhiteSpace(modlistNoMarkdown))
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(modlistNoMarkdown);
                        await MessageService.ShowInfo("Success", $"Total Mods: {processedModsChecked}\n\nModlist was copied to clipboard.");
                    }
                    catch (Exception ex)
                    {
                        await MessageService.ShowError($"Failed to copy to clipboard: {ex.Message}");
                    }
                }
                else
                {
                    await MessageService.ShowError("No mod(s) had valid names/URLs to copy.");
                }
            }
            else
            {
                Debug.WriteLine("ModsDataGrid.ItemsSource was empty or null");
            }
        }

        // ============ Check For Mod Updates ============

        // Check for mod updates loud
        private Task CheckForModUpdatesLoud() => CheckModUpdatesAsync(true);

        // Check for mod updates silent
        private Task CheckForModUpdatesSilent() => CheckModUpdatesAsync(false);

        // ============ Update Mods ============

        // Update All Mods
        private async Task UpdateAllModsAsync()
        {
            if (string.IsNullOrWhiteSpace(ModsFolderTextBox.Text) || !Directory.Exists(ModsFolderTextBox.Text))
            {
                await MessageService.ShowError("Mods folder is not selected or does not exist.");
                return;
            }

            if (ModsDataGrid.ItemsSource is not List<ModInfo> mods || mods.Count == 0)
            {
                await MessageService.ShowError("No mods loaded to update.");
                return;
            }

            var savedLinks = LoadSavedModLinks();
            using var client = new HttpClient();
            var updateResults = new List<string>();

            // HasVersionMismatch filter
            var modsToUpdate = mods.Where(m => m.HasUpdate && !m.HasVersionMismatch && !string.IsNullOrWhiteSpace(m.ModDownloadUrl)).ToList();

            if (modsToUpdate.Count == 0)
            {
                await MessageService.ShowInfo("Oops!", "You tried to update all mods when none of the mods required an update.");
                return;
            }

            int processed = 0;

            await MessageService.ShowProgress("Updating Mods", "Please wait...", async progress =>
            {
                foreach (var mod in modsToUpdate)
                {
                    var result = await UpdateModAsync(mod, ModsFolderTextBox.Text, savedLinks, client);

                    if (result.Success)
                        updateResults.Add(result.Message);
                    else
                        await MessageService.ShowError(result.Message);

                    processed++;
                    progress.Report((double)processed / modsToUpdate.Count);
                }

                ModsDataGrid.Items.Refresh();
                RefreshModCounters();
            });

            // Show mods downloaded
            if (updateResults.Count > 0)
            {
                string summary = string.Join("\n", updateResults);
                await MessageService.ShowModOutput("Updating Mods Complete", $"Finished updating {updateResults.Count} mod(s)!\n", summary);
            }
            else
            {
                await MessageService.ShowInfo("Oops!", "You tried to update all mods when none of the mods required an update.");
            }
        }

        // Update Single Mod
        private async Task UpdateSingleModAsync(ModInfo mod)
        {
            if (string.IsNullOrWhiteSpace(ModsFolderTextBox.Text) || !Directory.Exists(ModsFolderTextBox.Text))
            {
                await MessageService.ShowError("Mods folder is not selected or does not exist.");
                return;
            }

            if (mod == null || string.IsNullOrWhiteSpace(mod.ModDownloadUrl))
            {
                await MessageService.ShowError($"Invalid mod or missing download link for {mod?.Name ?? "Unknown"}.");
                return;
            }

            var savedLinks = LoadSavedModLinks();
            var updateResults = new List<string>();

            await MessageService.ShowProgress("Updating Mods", $"Downloading {mod.Name}...\n\nPlease wait...", async progress =>
            {
                try
                {
                    using var client = new HttpClient();

                    // Simulate download progress
                    for (int i = 0; i <= 100; i += 10)
                    {
                        await Task.Delay(20);
                        progress.Report(i / 100.0);
                    }

                    var result = await UpdateModAsync(mod, ModsFolderTextBox.Text, savedLinks, client);

                    if (result.Success)
                        updateResults.Add(result.Message);
                    else
                        await MessageService.ShowError(result.Message);

                    ModsDataGrid.Items.Refresh();

                    RefreshModCounters();

                    progress.Report(1.0);
                }
                catch (Exception ex)
                {
                    await MessageService.ShowError($"Failed to update {mod.Name}: {ex.Message}");
                }
            });

            // Show mods downloaded
            if (updateResults.Count > 0)
            {
                string summary = string.Join("\n", updateResults);
                await MessageService.ShowModOutput("Updating Mod Complete", "Finished updating mod!\n", summary);
            }
            else
            {
                await MessageService.ShowInfo("How?", "There shouldn't be any situation where you ever see this.\n\nThis means there were no mods to update on a single mod download.\n\nPlease make an issue on the github if you see this and tell me how you even managed to get here.");
            }
        }

        // ============ Check For Mod Update Helpers ============

        // Load Saved Mod Links From JSON
        private static Dictionary<string, ModLinkInfo> LoadSavedModLinks()
        {
            if (!File.Exists(modlinksPath))
                return [];

            string json = File.ReadAllText(modlinksPath);
            return JsonConvert.DeserializeObject<Dictionary<string, ModLinkInfo>>(json) ?? [];
        }

        // Save Mod Links To JSON
        private static void SaveModLinks(Dictionary<string, ModLinkInfo> links)
        {
            string json = JsonConvert.SerializeObject(links, Formatting.Indented);
            File.WriteAllText(modlinksPath, json);
        }

        // Is Version Greater helper using Nuget.Versioning
        private static bool IsVersionGreater(string v1, string v2)
        {
            if (NuGetVersion.TryParse(v1, out var version1) && NuGetVersion.TryParse(v2, out var version2))
            {
                return version1 > version2;
            }

            // If parsing fails, treat v1 as not greater, hopefully shouldn't happen
            return false;
        }

        // Mod update check logic
        private async Task CheckModUpdatesAsync(bool showMessage)
        {
            if (string.IsNullOrWhiteSpace(selectedModPath) || !Directory.Exists(selectedModPath))
            {
                if (showMessage)
                    await MessageService.ShowError("No mods folder selected.");
                return;
            }

            if (GameVersionDropdown.SelectedItem == null)
            {
                await MessageService.ShowError("Please wait for game versions to load or select a version.");
                return;
            }

            await LoadModsFromFolderAsync(selectedModPath);

            if (ModsDataGrid.ItemsSource is not List<ModInfo> mods || mods.Count == 0)
                return;

            var savedLinks = LoadSavedModLinks();
            var modsNeedingUpdate = new List<ModInfo>();
            var errors = new List<string>();

            var modsToCheck = mods.Where(m => !m.DisableUpdate).ToList();
            int totalModsChecked = modsToCheck.Count;
            processedModsChecked = 0;

            string selectedVersion = GameVersionDropdown.SelectedItem?.ToString() ?? "1.22.x";
            string selectedVersionPrefix = selectedVersion.Replace(".x", "");

            bool isSafeModeActive = CurrentSettings.SafeMode;

            await MessageService.ShowProgress("Checking for mod updates", "Please wait...", async progress =>
            {
                using var client = new HttpClient();

                var tasks = modsToCheck.Select(async mod =>
                {
                    string zipPath = Path.Combine(ModsFolderTextBox.Text, mod.FileName);

                    try
                    {
                        using var archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.GetEntry("modinfo.json")
                            ?? throw new InvalidDataException($"modinfo.json not found in {mod.FileName}");

                        using var reader = new StreamReader(entry.Open());
                        var json = await reader.ReadToEndAsync();
                        var root = JObject.Parse(json);

                        string modId = root.Properties()
                            .FirstOrDefault(p => string.Equals(p.Name, "ModID", StringComparison.OrdinalIgnoreCase))?
                            .Value?.ToString()
                            ?? throw new InvalidDataException($"ModID missing in {mod.FileName}");

                        string version = root.Properties()
                            .FirstOrDefault(p => string.Equals(p.Name, "version", StringComparison.OrdinalIgnoreCase))?
                            .Value?.ToString()
                            ?? throw new InvalidDataException($"Version missing in {mod.FileName}");

                        mod.Version = version;
                        mod.ModId = modId;

                        // Reset the tracking flags
                        mod.LatestVersion = version;
                        mod.HasVersionMismatch = false;

                        // Fetch API info
                        string apiUrl = $"https://mods.vintagestory.at/api/mod/{modId}";
                        string response = await client.GetStringAsync(apiUrl);
                        var apiData = JObject.Parse(response);

                        int assetId = apiData["mod"]?["assetid"]?.Value<int>() ?? 0;
                        if (assetId != 0)
                            mod.ModPageUrl = $"https://mods.vintagestory.at/show/mod/{assetId}";

                        string? chosenVersion = null;
                        string? chosenDownloadUrl = null;

                        if (apiData["mod"]?["releases"] is JArray releases && releases.Count > 0)
                        {
                            var orderedReleases = releases
                                .Select(r =>
                                {
                                    var raw = r["modversion"]?.ToString()?.TrimStart('v').Trim();
                                    return new
                                    {
                                        Release = r,
                                        Version = NuGetVersion.TryParse(raw, out var v) ? v : null
                                    };
                                })
                                .Where(x => x.Version != null)
                                .OrderByDescending(x => x.Version);

                            foreach (var item in orderedReleases)
                            {
                                var release = item.Release;
                                var parsedVersion = item.Version!;

                                // Safe Mode Filter
                                if (isSafeModeActive && parsedVersion.IsPrerelease)
                                {
                                    continue;
                                }

                                var tags = release["tags"]?.Select(t => t.ToString()) ?? [];
                                bool versionMatches = tags.Any(t =>
                                    t.Equals(selectedVersion, StringComparison.OrdinalIgnoreCase) ||
                                    t.StartsWith(selectedVersionPrefix, StringComparison.OrdinalIgnoreCase));

                                if (!versionMatches)
                                {
                                    // This release doesn't fit the chosen game version, but is it newer than what is installed
                                    // This can happen when a mod hasn't updated in awhile and a new game version comes out, but the mod is still in your list
                                    if (!mod.HasVersionMismatch && IsVersionGreater(parsedVersion.ToNormalizedString(), mod.Version))
                                    {
                                        mod.LatestVersion = parsedVersion.ToNormalizedString();
                                        mod.HasVersionMismatch = true;

                                        // Still save the url so single download can work
                                        chosenDownloadUrl = release["mainfile"]?.ToString();
                                    }
                                    continue;
                                }

                                // Fixed some broken logic that ignored prerelease versions
                                chosenVersion = parsedVersion.ToNormalizedString();
                                chosenDownloadUrl = release["mainfile"]?.ToString();
                                break;
                            }
                        }

                        // If a version matched the game version selected it overwrites our detour value
                        if (chosenVersion != null)
                        {
                            mod.LatestVersion = chosenVersion;
                            mod.HasVersionMismatch = false; // If it's a standard update, remove flag
                        }

                        if (chosenDownloadUrl != null)
                            mod.ModDownloadUrl = chosenDownloadUrl;

                        // Always add/update savedLinks
                        lock (savedLinks)
                        {
                            if (!savedLinks.ContainsKey(modId))
                                savedLinks[modId] = new ModLinkInfo();

                            savedLinks[modId].ModPageUrl = mod.ModPageUrl;
                            savedLinks[modId].ModDownloadUrl = mod.ModDownloadUrl ?? savedLinks[modId].ModDownloadUrl;
                            savedLinks[modId].DisableUpdate = mod.DisableUpdate;
                            savedLinks[modId].HasVersionMismatch = mod.HasVersionMismatch;
                        }

                        // Only mark mod for update if it's not classified as requiring a version mismatch update
                        if (!mod.HasVersionMismatch && !string.IsNullOrWhiteSpace(mod.LatestVersion) &&
                            IsVersionGreater(mod.LatestVersion, mod.Version))
                        {
                            lock (modsNeedingUpdate)
                                modsNeedingUpdate.Add(mod);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add($"Failed to check mod {mod.FileName}: {ex.Message}");
                        }
                    }
                    finally
                    {
                        int processed = Interlocked.Increment(ref processedModsChecked);
                        progress.Report((double)processed / totalModsChecked);
                    }
                });

                await Task.WhenAll(tasks);

                // Update UI Counts
                int totalCount = mods.Count;
                int updatesAvailableCount = modsNeedingUpdate.Count;
                int versionMismatchCount = mods.Count(m => m.HasVersionMismatch);
                int updatesDisabledCount = mods.Count(m => m.DisableUpdate);

                Dispatcher.Invoke(() =>
                {
                    TotalModsCountText.Text = totalCount.ToString();
                    UpdateCountText.Text = updatesAvailableCount.ToString();
                    VersionMismatchCountText.Text = versionMismatchCount.ToString();
                    DisabledCountText.Text = updatesDisabledCount.ToString();
                });

                ModsDataGrid.Items.Refresh();

                // Save updated links
                var sortedLinks = savedLinks.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                SaveModLinks(sortedLinks);
                ModsDataGrid.Items.Refresh();

                if (showMessage)
                {
                    string message = modsNeedingUpdate.Count > 0
                        ? $"Total Mods Checked: {totalModsChecked}\n\n{modsNeedingUpdate.Count} mod(s) need to be updated:\n\n- {string.Join("\n- ", modsNeedingUpdate.Select(m => m.Name))}"
                        : "All mods are up to date!";

                    await MessageService.ShowInfo("Update Check Complete", message);

                    if (errors.Count > 0)
                        await MessageService.ShowError(string.Join("\n", errors));
                }
            });
        }

        // ============ Mod Update Helpers ============

        // Save Disable Update Status
        private static void SaveDisableUpdateForMod(string modId, bool disabled)
        {
            try
            {
                var savedLinks =
                    (File.Exists(modlinksPath)
                        ? JsonConvert.DeserializeObject<Dictionary<string, ModLinkInfo>>(File.ReadAllText(modlinksPath))
                        : null)
                    ?? [];

                if (!savedLinks.ContainsKey(modId))
                    savedLinks[modId] = new ModLinkInfo();

                savedLinks[modId].DisableUpdate = disabled;

                File.WriteAllText(modlinksPath, JsonConvert.SerializeObject(savedLinks, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update DisableUpdate for {modId}: {ex.Message}");
            }
        }

        // Disable Update Helper
        private void DisableUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is ModInfo mod)
            {
                mod.DisableUpdate = cb.IsChecked == true;

                var savedLinks = LoadSavedModLinks();
                if (!string.IsNullOrEmpty(mod.ModId))
                {
                    if (!savedLinks.ContainsKey(mod.ModId))
                        savedLinks[mod.ModId] = new ModLinkInfo();

                    savedLinks[mod.ModId].DisableUpdate = mod.DisableUpdate;
                    savedLinks[mod.ModId].ModPageUrl = mod.ModPageUrl;
                    savedLinks[mod.ModId].ModDownloadUrl = mod.ModDownloadUrl;

                    SaveModLinks(savedLinks);
                }

                if (ModsDataGrid.ItemsSource is List<ModInfo> mods)
                {
                    int disabledCount = mods.Count(m => m.DisableUpdate);
                    DisabledCountText.Text = disabledCount.ToString();
                }
            }
        }

        private class ModUpdateResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public ModInfo Mod { get; set; } = null!;
        }

        // Extract the actual download URL (strip ?dl=...)
        private static string GetActualDownloadUrl(string url)
        {
            int qIndex = url.IndexOf("?dl=");
            return qIndex >= 0 ? url[..qIndex] : url;
        }

        // Extract the file name from the ?dl= parameter
        private static string GetFileNameFromDownloadUrl(string url)
        {
            int qIndex = url.IndexOf("?dl=");
            string fileName = qIndex >= 0 ? url[(qIndex + 4)..] : Path.GetFileName(url);

            // Clean invalid file name characters
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            return fileName;
        }

        // Download a file to temp folder using the stripped URL
        private static async Task<string> DownloadFileToTempAsync(string url, HttpClient client)
        {
            string actualUrl = GetActualDownloadUrl(url);
            string tempFilePath = Path.Combine(Path.GetTempPath(), GetFileNameFromDownloadUrl(url));

            using var response = await client.GetAsync(actualUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);

            return tempFilePath;
        }

        // Update Mod Logic
        private static async Task<ModUpdateResult> UpdateModAsync(ModInfo mod, string modsFolder, Dictionary<string, ModLinkInfo> savedLinks, HttpClient client)
        {
            string zipPath = Path.Combine(modsFolder, mod.FileName);
            string? modId = null;

            // Read modId from modinfo.json
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry("modinfo.json");
                if (entry != null)
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var root = JObject.Parse(json);
                    modId = root.Properties().FirstOrDefault(p => string.Equals(p.Name, "modId", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
                }
            }
            catch (Exception ex)
            {
                return new ModUpdateResult { Success = false, Message = $"Error reading modId from {mod.FileName}: {ex.Message}", Mod = mod };
            }

            if (string.IsNullOrEmpty(modId))
                return new ModUpdateResult { Success = false, Message = $"Could not read modId for {mod.Name}, skipping update.", Mod = mod };

            if (!savedLinks.TryGetValue(modId, out var linkInfo) || string.IsNullOrWhiteSpace(linkInfo.ModDownloadUrl))
                return new ModUpdateResult { Success = false, Message = $"No download link found for {mod.Name}, skipping update.", Mod = mod };

            string downloadUrl = savedLinks[modId].ModDownloadUrl;
            string tempFile;

            try
            {
                // Download the updated mod using helpers
                tempFile = await DownloadFileToTempAsync(downloadUrl, client);
            }
            catch (Exception ex)
            {
                return new ModUpdateResult { Success = false, Message = $"Failed to download {mod.Name}: {ex.Message}", Mod = mod };
            }

            try
            {
                // Delete the old mod modName.zip
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                // Move downloaded file to mods folder with correct name
                string destPath = Path.Combine(modsFolder, GetFileNameFromDownloadUrl(downloadUrl));
                File.Move(tempFile, destPath);

                // Store old mod version
                string oldVersion = mod.Version;

                // Update ModInfo model
                mod.FileName = Path.GetFileName(destPath);
                mod.Version = mod.LatestVersion;

                mod.HasVersionMismatch = false;

                // Record old/new versions for summary
                return new ModUpdateResult { Success = true, Message = $"- Updated **{modId}** from **{oldVersion}** to **{mod.LatestVersion}**", Mod = mod };
            }
            catch (Exception ex)
            {
                return new ModUpdateResult { Success = false, Message = $"Failed to replace {mod.Name}: {ex.Message}", Mod = mod };
            }
        }

        // ============ Mods Folder Helpers ============

        // Select Mods Folder
        private async Task<bool> SelectModsFolderAsync()
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select your Vintage Story mods folder";
            dialog.ShowNewFolderButton = false;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ModsFolderTextBox.Text = dialog.SelectedPath;
                await LoadModsFromFolderAsync(dialog.SelectedPath);
                selectedModPath = dialog.SelectedPath;

                CurrentSettings ??= new AppSettings();

                CurrentSettings.ModsFolderPath = dialog.SelectedPath;

                SaveAppSettings(CurrentSettings);

                return true;
            }
            return false;
        }

        // Load Mods Folder
        private async Task LoadModsFromFolderAsync(string folderPath)
        {
            var mods = new List<ModInfo>();
            var savedLinks = LoadSavedModLinks();

            string GetValue(JObject obj, string key)
            {
                return obj.Properties()
                    .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))?
                    .Value?.ToString() ?? "";
            }

            foreach (var zipFile in Directory.GetFiles(folderPath, "*.zip"))
            {
                try
                {
                    string fileName = Path.GetFileName(zipFile);
                    string modName = fileName;
                    string modId = "";
                    string version = "N/A";
                    string gameVersion = "N/A";
                    string description = "N/A";

                    using (var archive = ZipFile.OpenRead(zipFile))
                    {
                        var entry = archive.GetEntry("modinfo.json");

                        if (entry != null)
                        {
                            using var reader = new StreamReader(entry.Open());
                            string json = await reader.ReadToEndAsync();
                            var root = JObject.Parse(json);

                            modName = string.IsNullOrWhiteSpace(GetValue(root, "name")) ? fileName : GetValue(root, "name");
                            modId = root.Properties()
                                .FirstOrDefault(p => string.Equals(p.Name, "modId", StringComparison.OrdinalIgnoreCase))?
                                .Value?.ToString() ?? "";
                            version = string.IsNullOrWhiteSpace(GetValue(root, "version")) ? "N/A" : GetValue(root, "version");
                            description = string.IsNullOrWhiteSpace(GetValue(root, "description")) ? "N/A" : GetValue(root, "description");

                            if (root.Properties()
                                .FirstOrDefault(p => string.Equals(p.Name, "dependencies", StringComparison.OrdinalIgnoreCase))?.Value is JObject dependenciesToken)
                                gameVersion = string.IsNullOrWhiteSpace(dependenciesToken.Properties()
                                    .FirstOrDefault(p => string.Equals(p.Name, "game", StringComparison.OrdinalIgnoreCase))?.Value?.ToString())
                                    ? "N/A"
                                    : dependenciesToken.Properties()
                                        .FirstOrDefault(p => string.Equals(p.Name, "game", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "N/A";
                        }
                    }

                    ModLinkInfo? linkData = null;
                    bool hasSavedLink = !string.IsNullOrEmpty(modId) && savedLinks.TryGetValue(modId, out linkData);

                    var mod = new ModInfo
                    {
                        FileName = fileName,
                        Name = modName,
                        ModId = modId,
                        Version = version,
                        Game = gameVersion,
                        Description = description,

                        DisableUpdate = hasSavedLink && linkData!.DisableUpdate,
                        ModPageUrl = hasSavedLink ? linkData!.ModPageUrl : "",
                        ModDownloadUrl = hasSavedLink ? linkData!.ModDownloadUrl : ""
                    };

                    mods.Add(mod);
                }
                catch (Exception ex)
                {
                    await MessageService.ShowError($"Error reading {zipFile}: {ex.Message}");
                }
            }

            ModsDataGrid.ItemsSource = mods;

            _modsView = CollectionViewSource.GetDefaultView(ModsDataGrid.ItemsSource);
            _modsView.Filter = FilterModRows;

            Dispatcher.Invoke(() =>
            {
                TotalModsCountText.Text = mods.Count.ToString();
                DisabledCountText.Text = mods.Count(m => m.DisableUpdate).ToString();

                UpdateCountText.Text = "0";
                VersionMismatchCountText.Text = "0";
            });

            ModsDataGrid.Items.Refresh();
        }

        // ============ Game Version Helper ============
        public async Task LoadGameVersionsAsync()
        {
            using HttpClient client = new();
            try
            {
                string json = await client.GetStringAsync(GameVersionsUrl);
                var data = JsonConvert.DeserializeObject<GamesVersions>(json);

                if (data?.GameVersionsList != null)
                {
                    GameVersionDropdown.ItemsSource = data.GameVersionsList;
                    DefaultGameVersionDropdown.ItemsSource = data.GameVersionsList;

                    var settings = LoadAppSettings();
                    string savedDefault = settings?.DefaultGameVersion ?? "";

                    if (!string.IsNullOrEmpty(savedDefault) && data.GameVersionsList.Contains(savedDefault))
                    {
                        GameVersionDropdown.SelectedItem = savedDefault;
                        DefaultGameVersionDropdown.SelectedItem = savedDefault;
                    }
                    else if (data.GameVersionsList.Count > 0)
                    {
                        GameVersionDropdown.SelectedIndex = 0;
                        DefaultGameVersionDropdown.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageService.ShowError($"Failed to load game versions: {ex.Message}");

                var errorList = new List<string> { "Error loading versions" };
                GameVersionDropdown.ItemsSource = errorList;
                DefaultGameVersionDropdown.ItemsSource = errorList;
            }
        }

        // ============ Additional Features Below ============

        // Clean Mods Folder
        private async Task CleanModsFolderAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedModPath) || !Directory.Exists(selectedModPath))
            {
                await MessageService.ShowError("No mods folder selected.");
                return;
            }

            // Prompt user before reinstalling mods
            bool confirm = await MessageService.ShowYesCancel(
                "Clean Reinstall Mods",
                "This will clear the selected mods folder and completely redownload the EXACT versions of all currently loaded mods, this will also uncheck any of your ignored mods. Are you sure?"
            );
            if (!confirm) return;

            // Wipe data saved in ModLinks.json
            var blankRegistry = new Dictionary<string, ModLinkInfo>();
            SaveModLinks(blankRegistry);

            await LoadModsFromFolderAsync(selectedModPath);
            if (ModsDataGrid.ItemsSource is not List<ModInfo> activeMods || activeMods.Count == 0)
            {
                await MessageService.ShowError("No valid mods detected to reinstall.");
                return;
            }

            var modsToRestore = new List<ModInfo>();
            var errors = new List<string>();
            var reinstallationLog = new List<string>();

            int totalModsToScan = activeMods.Count;
            int processedScanCount = 0;

            // Parse modinfo.json within mod.zips
            await MessageService.ShowProgress("Mapping Current Versions", "Analyzing existing files...", async progress =>
            {
                using var client = new HttpClient();

                foreach (var mod in activeMods)
                {
                    string zipPath = Path.Combine(selectedModPath, mod.FileName);
                    try
                    {
                        using var archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.GetEntry("modinfo.json")
                            ?? throw new InvalidDataException("modinfo.json missing from archive.");

                        using var reader = new StreamReader(entry.Open());
                        var json = await reader.ReadToEndAsync();
                        var root = JObject.Parse(json);

                        string modId = root.Properties()
                            .FirstOrDefault(p => string.Equals(p.Name, "ModID", StringComparison.OrdinalIgnoreCase))?
                            .Value?.ToString()
                            ?? throw new InvalidDataException("ModID property missing from JSON schema.");

                        string currentVersion = root.Properties()
                            .FirstOrDefault(p => string.Equals(p.Name, "version", StringComparison.OrdinalIgnoreCase))?
                            .Value?.ToString()?.TrimStart('v').Trim()
                            ?? throw new InvalidDataException("Version property missing from JSON schema.");

                        string apiUrl = $"https://mods.vintagestory.at/api/mod/{modId}";
                        string response = await client.GetStringAsync(apiUrl);
                        var apiData = JObject.Parse(response);

                        string? specificDownloadUrl = null;

                        if (apiData["mod"]?["releases"] is JArray releases && releases.Count > 0)
                        {
                            // Find the download for the exact mod version
                            foreach (var release in releases)
                            {
                                var releaseVersion = release["modversion"]?.ToString()?.TrimStart('v').Trim();
                                if (string.Equals(releaseVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
                                {
                                    specificDownloadUrl = release["mainfile"]?.ToString();
                                    break;
                                }
                            }
                        }

                        // If the exact version wasn't found, fallback to whatever URL is known (if any)
                        if (string.IsNullOrWhiteSpace(specificDownloadUrl))
                        {
                            specificDownloadUrl = mod.ModDownloadUrl;
                        }

                        if (!string.IsNullOrWhiteSpace(specificDownloadUrl))
                        {
                            modsToRestore.Add(new ModInfo
                            {
                                ModId = modId,
                                Name = mod.Name ?? modId,
                                FileName = mod.FileName,
                                ModDownloadUrl = specificDownloadUrl
                            });
                        }
                        else
                        {
                            lock (errors) { errors.Add($"Could not find download link for version '{currentVersion}' of {mod.FileName} on the API. Did the mod get removed from mods.vintagestory.at?"); }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors) { errors.Add($"Failed analyzing {mod.FileName} before clean: {ex.Message}"); }
                    }
                    finally
                    {
                        processedScanCount++;
                        progress.Report((double)processedScanCount / totalModsToScan);
                    }
                }
            });

            if (modsToRestore.Count == 0)
            {
                await MessageService.ShowError("Aborting reinstall: No valid download targets could be matched via the API.");
                return;
            }

            // Delete all mod.zips
            await MessageService.ShowProgress("Cleaning Directory", "Deleting current mod files...", async progress =>
            {
                await Task.Run(() =>
                {
                    var modFiles = Directory.EnumerateFiles(selectedModPath, "*.*", SearchOption.TopDirectoryOnly)
                                            .Where(file => file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                    foreach (var filePath in modFiles)
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            lock (errors) { errors.Add($"Could not delete {Path.GetFileName(filePath)}: {ex.Message}"); }
                        }
                    }
                });
                progress.Report(1.0);
            });

            // Download mods
            using var downloadClient = new HttpClient();
            int processedCount = 0;

            await MessageService.ShowProgress("Restoring Mods", "Downloading clean copies of your original versions...", async progress =>
            {
                foreach (var mod in modsToRestore)
                {
                    try
                    {
                        string destinationPath = Path.Combine(selectedModPath, mod.FileName);

                        using var response = await downloadClient.GetAsync(mod.ModDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
                        using var streamToWriteTo = File.Create(destinationPath);

                        await streamToReadFrom.CopyToAsync(streamToWriteTo);

                        reinstallationLog.Add($"Successfully Restored: {mod.Name} ({mod.FileName})");
                    }
                    catch (Exception ex)
                    {
                        lock (errors) { errors.Add($"Failed to download {mod.FileName}: {ex.Message}"); }
                    }
                    finally
                    {
                        processedCount++;
                        progress.Report((double)processedCount / modsToRestore.Count);
                    }
                }
            });

            // Refresh UI and show summary
            await LoadModsFromFolderAsync(selectedModPath);
            ModsDataGrid.Items.Refresh();

            if (reinstallationLog.Count > 0)
            {
                string finalSummary = string.Join("\n", reinstallationLog);
                await MessageService.ShowModOutput("Clean Reinstall Finished", "Reinstallation process wrapped up:\n", finalSummary);
            }

            if (errors.Count > 0)
            {
                await MessageService.ShowError($"Some issues occurred during processing:\n{string.Join("\n", errors)}");
            }
        }

        // Remove Duplicate Mods
        public async Task RemoveDuplicateModsAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedModPath) || !Directory.Exists(selectedModPath))
            {
                await MessageService.ShowError("No mods folder selected.");
                return;
            }

            bool confirm = await MessageService.ShowYesCancel(
                "Remove Duplicate Mods",
                "This will scan your mods folder for duplicate mod files and delete the older version. Are you sure?"
            );
            if (!confirm) return;

            var discoveredMods = new List<ModScanResult>();
            var errors = new List<string>();
            var deletionLog = new List<string>();

            var zipFiles = Directory.GetFiles(selectedModPath, "*.zip", SearchOption.TopDirectoryOnly);
            int totalFiles = zipFiles.Length;
            int processedCount = 0;

            if (totalFiles == 0)
            {
                await MessageService.ShowError("No .zip files found in the selected folder.");
                return;
            }

            await MessageService.ShowProgress("Processing Mods Folder", "Scanning archives and purging duplicates...", async progress =>
            {
                await Task.Run(() =>
                {
                    foreach (var zipPath in zipFiles)
                    {
                        try
                        {
                            using var archive = ZipFile.OpenRead(zipPath);
                            var entry = archive.GetEntry("modinfo.json");
                            if (entry == null) continue;

                            using var reader = new StreamReader(entry.Open());
                            var json = reader.ReadToEnd();
                            var root = JObject.Parse(json);

                            string modId = (root.Properties()
                                .FirstOrDefault(p => string.Equals(p.Name, "ModID", StringComparison.OrdinalIgnoreCase))?
                                .Value?.ToString() ?? string.Empty).Trim();

                            string versionStr = (root.Properties()
                                .FirstOrDefault(p => string.Equals(p.Name, "version", StringComparison.OrdinalIgnoreCase))?
                                .Value?.ToString() ?? string.Empty).TrimStart('v', 'V').Trim();

                            if (!string.IsNullOrEmpty(modId) && !string.IsNullOrEmpty(versionStr))
                            {
                                if (NuGetVersion.TryParse(versionStr, out NuGetVersion? parsedVersion) && parsedVersion != null)
                                {
                                    discoveredMods.Add(new ModScanResult
                                    {
                                        FilePath = zipPath,
                                        ModId = modId.ToLowerInvariant(),
                                        Version = parsedVersion
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (errors) { errors.Add($"Error reading {Path.GetFileName(zipPath)}: {ex.Message}"); }
                        }
                        finally
                        {
                            processedCount++;
                            progress.Report(((double)processedCount / totalFiles) * 0.9);
                        }
                    }
                });

                var duplicateGroups = discoveredMods
                    .GroupBy(m => m.ModId)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateGroups.Count == 0)
                {
                    progress.Report(1.0);
                    return;
                }

                int filesDeletedCount = 0;

                // Delete duplicates
                await Task.Run(() =>
                {
                    foreach (var group in duplicateGroups)
                    {
                        var sortedGroup = group.OrderByDescending(m => m.Version).ToList();
                        var newestMod = sortedGroup[0];
                        var duplicatesToRemove = sortedGroup.Skip(1);

                        foreach (var oldMod in duplicatesToRemove)
                        {
                            try
                            {
                                if (File.Exists(oldMod.FilePath))
                                {
                                    File.Delete(oldMod.FilePath);
                                    filesDeletedCount++;
                                    lock (deletionLog)
                                    {
                                        deletionLog.Add($"Deleted older duplicate: {Path.GetFileName(oldMod.FilePath)} (Kept version: {newestMod.Version})");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                lock (errors) { errors.Add($"Could not delete {Path.GetFileName(oldMod.FilePath)}: {ex.Message}"); }
                            }
                        }
                    }
                });

                progress.Report(1.0);
            });

            // Refresh UI and show summary
            await LoadModsFromFolderAsync(selectedModPath);
            ModsDataGrid.Items.Refresh();

            if (deletionLog.Count > 0)
            {
                string finalSummary = string.Join("\n", deletionLog);
                await MessageService.ShowInfo("Cleanup Finished", $"Successfully removed {deletionLog.Count} duplicate mod files:\n{finalSummary}");
            }
            else
            {
                await MessageService.ShowInfo("Cleanup Finished", "No duplicate mods were found.");
            }

            if (errors.Count > 0)
            {
                await MessageService.ShowError($"Some file system problems occurred during execution:\n{string.Join("\n", errors)}");
            }
        }

        // ============ Mod Browser ============

        // Check if WebView2 is even installed
        private static async Task EnsureWebView2RuntimeAsync()
        {
            bool isRuntimeInstalled = false;

            try
            {
                string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                isRuntimeInstalled = true;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                // Runtime is not installed, will prompt user to download
            }
            catch (Exception ex)
            {
                await MessageService.ShowError($"Unexpected error tracking runtime: {ex.Message}");
            }

            // If it's installed let the app load normally
            if (isRuntimeInstalled) return;

            // Prompt the user for permission to handle the download
            bool proceedWithDownload = await MessageService.ShowYesCancel(
                "Missing Dependency",
                "This application requires the Microsoft WebView2 Runtime (~250MB) to display the mod browser.\n\n" +
                "Would you like to automatically download and run the installer now?\n\n" +
                $"If you prefer to install it manually, you can download the official Microsoft Evergreen Bootstrapper directly at: {WebView2Url}"
            );

            if (!proceedWithDownload)
            {
                await MessageService.ShowError("The mod browser cannot function without this dependency.\n\nThe application will now close, but you can restart it at any time to use all other features normally.");
                System.Windows.Application.Current.Shutdown(); // Close app if user declines
                return;
            }

            // Download and Run the Bootstrapper
            await DownloadAndRunWebView2BootstrapperAsync();
            System.Windows.Application.Current.Shutdown(); // Close app once the installer starts
        }

        // Download and Run Evergreen Bootstrapper for WebView2
        private static async Task DownloadAndRunWebView2BootstrapperAsync()
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

            try
            {
                // Download the installer
                await MessageService.ShowProgress("Downloading Dependency", "Fetching WebView2 installer...", async progress =>
                {
                    using var client = new HttpClient();
                    using var response = await client.GetAsync(WebView2Url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var streamToRead = await response.Content.ReadAsStreamAsync();
                    using var streamToWrite = File.Create(tempFilePath);

                    await streamToRead.CopyToAsync(streamToWrite);
                    progress.Report(1.0);
                });

                // Launch the installer package
                if (File.Exists(tempFilePath))
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = tempFilePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    await MessageService.ShowInfo(
                        "Installer Started",
                        "The WebView2 setup has been successfully launched.\n\n" +
                        "Please follow the prompts on your screen to complete the Microsoft installation.\n\n" +
                        "This application will now close to allow the setup process to proceed cleanly. Please relaunch the app once the installation finishes."
                    );

                    Process.Start(processStartInfo);
                }
            }
            catch (Exception ex)
            {
                await MessageService.ShowError($"Failed to process or run the WebView2 installer: {ex.Message}");
            }
        }

        // Initialize WebView2
        private async Task InitializeModBrowserAsync()
        {
            try
            {
                // Store the WebView2 cache in localappdata
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cacheFolder = Path.Combine(localAppData, "VSSuite", "WebView2_Cache");

                var environment = await CoreWebView2Environment.CreateAsync(null, cacheFolder);

                await ModWebView.EnsureCoreWebView2Async(environment);

                ModWebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
                ModWebView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
            }
            catch (Exception ex)
            {
                await MessageService.ShowError($"Failed to initialize browser engine: {ex.Message}");
            }
        }

        // Webview Flyout Event Handler
        private async void ModBrowserFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (ModBrowserFlyout.IsOpen)
            {
                // Keep WebView2 hidden when opening
                ModWebView.Visibility = System.Windows.Visibility.Collapsed;

                if (!_isBrowserInitialized)
                {
                    await EnsureWebView2RuntimeAsync();

                    await InitializeModBrowserAsync();

                    ModWebView.Source = VSModRepoHomeUrlUri;

                    _isBrowserInitialized = true;
                }

                await Task.Delay(500);

                // Display WebView2 after a short delay
                ModWebView.Visibility = System.Windows.Visibility.Visible;

                await Task.Delay(300);

                ForceWebViewRedraw();
            }
            else
            {
                // Hide WebView2 when closing
                ModWebView.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        // Webview Login/Mod Troubleshooting Button Hide Event Handler
        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                string targetSelectors = "#main-nav > a.strikethrough-when-readonly, #main-nav > a.external";

                string jsCode = $"let a=0, i=setInterval(()=>{{document.querySelectorAll('{targetSelectors}').forEach(b=>{{b.style.setProperty('display','none','important');b.style.setProperty('visibility','hidden','important');}});if(++a>50)clearInterval(i);}},100);";

                await ModWebView.ExecuteScriptAsync(jsCode);

                await ModWebView.ExecuteScriptAsync("document.body.style.overflowX = 'hidden';");
            }
        }

        // Redraw WebView
        private void ForceWebViewRedraw()
        {
            if (ModWebView != null && ModWebView.CoreWebView2 != null)
            {
                try
                {
                    if (ModWebView.GetType()
                        .GetProperty("CoreWebView2Controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                        .GetValue(ModWebView) is Microsoft.Web.WebView2.Core.CoreWebView2Controller controller)
                    {
                        controller.Bounds = new System.Drawing.Rectangle(
                            0,
                            0,
                            (int)ModWebView.ActualWidth,
                            (int)ModWebView.ActualHeight
                        );
                    }
                    else
                    {
                        var currentVisibility = ModWebView.Visibility;
                        ModWebView.Visibility = System.Windows.Visibility.Collapsed;
                        ModWebView.Visibility = currentVisibility;
                    }
                }
                catch
                {
                    ModWebView.InvalidateVisual();
                }
            }
        }

        // Address bar sync
        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            BrowserAddressBar.Text = ModWebView.Source.ToString();
        }

        // Address bar helper
        private void BrowserAddressBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string url = BrowserAddressBar.Text.Trim();
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                ModWebView.CoreWebView2?.Navigate(url);
            }
        }

        // Browser Go Back Button
        private void BrowserBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModWebView.CoreWebView2 != null && ModWebView.CoreWebView2.CanGoBack)
            {
                ModWebView.CoreWebView2.GoBack();
            }
        }

        // Browser Go Forward Button
        private void BrowserForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModWebView.CoreWebView2 != null && ModWebView.CoreWebView2.CanGoForward)
            {
                ModWebView.CoreWebView2.GoForward();
            }
        }

        // Browser Refresh Button
        private void BrowserRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ModWebView.CoreWebView2?.Reload();
        }

        // Browser Home Button
        private void BrowserHomeButton_Click(object sender, RoutedEventArgs e)
        {
            ModWebView.CoreWebView2?.Navigate(VSModRepoHomeUrl);
        }

        // Intercept downloads and force them into the selected mod path
        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string? targetFolder = selectedModPath;

            if (!string.IsNullOrWhiteSpace(targetFolder) && Directory.Exists(targetFolder))
            {
                string fileName = Path.GetFileName(e.ResultFilePath);
                string destinationPath = Path.Combine(targetFolder, fileName);

                e.ResultFilePath = destinationPath;
                e.Handled = true;

                // Hide WebView2 so progress and success dialogs display properly
                ModWebView.Visibility = System.Windows.Visibility.Collapsed;

                CoreWebView2DownloadOperation download = e.DownloadOperation;
                var downloadTaskSource = new TaskCompletionSource<bool>();
                IProgress<double>? progressReporter = null;

                download.StateChanged += async (s, args) =>
                {
                    switch (download.State)
                    {
                        case CoreWebView2DownloadState.InProgress:
                            if (progressReporter != null && download.TotalBytesToReceive.HasValue && download.TotalBytesToReceive.Value > 0)
                            {
                                double pct = (double)download.BytesReceived / download.TotalBytesToReceive.Value;
                                progressReporter.Report(pct);
                            }
                            break;

                        case CoreWebView2DownloadState.Completed:
                            downloadTaskSource.TrySetResult(true);

                            // Reload mods folder and update UI
                            await LoadModsFromFolderAsync(targetFolder);
                            RefreshModCounters();

                            // Mod has been downloaded prompt
                            await MessageService.ShowOk("Success!", $"{fileName} has been successfully downloaded.");

                            ModWebView.Visibility = System.Windows.Visibility.Visible;
                            break;

                        case CoreWebView2DownloadState.Interrupted:
                            downloadTaskSource.TrySetException(new Exception($"Download interrupted. Reason: {download.InterruptReason}"));

                            ModWebView.Visibility = System.Windows.Visibility.Visible;

                            await MessageService.ShowError($"Download failed or paused.\n\nReason: {download.InterruptReason}");
                            break;
                    }
                };

                _ = MessageService.ShowProgress(
                    "Downloading Mod",
                    $"Downloading {fileName} from the repository...",
                    async progress =>
                    {
                        progressReporter = progress;
                        await downloadTaskSource.Task;
                    });
            }
            else
            {
                e.Handled = false;
            }
        }

        // ============ UI Helpers ============

        // Refresh Mod Counters
        private void RefreshModCounters()
        {
            if (ModsDataGrid.ItemsSource is List<ModInfo> mods)
            {
                int totalCount = mods.Count;

                int updatesAvailableCount = mods.Count(m => m.HasUpdate && !m.HasVersionMismatch);
                int versionMismatchCount = mods.Count(m => m.HasVersionMismatch);
                int updatesDisabledCount = mods.Count(m => m.DisableUpdate);

                Dispatcher.Invoke(() =>
                {
                    TotalModsCountText.Text = totalCount.ToString();
                    UpdateCountText.Text = updatesAvailableCount.ToString();
                    VersionMismatchCountText.Text = versionMismatchCount.ToString();
                    DisabledCountText.Text = updatesDisabledCount.ToString();
                });
            }
        }

        // Filter Mod Table via Searchbar
        private bool FilterModRows(object item)
        {
            if (string.IsNullOrWhiteSpace(ModSearchTextBox.Text))
                return true;

            if (item is ModInfo mod)
            {
                return mod.Name.Contains(ModSearchTextBox.Text, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        // Searchbar Event Handler - TextChanged
        private void Searchbar_TextChanged(object sender, TextChangedEventArgs e)
        {
            _modsView?.Refresh();
        }

        // Searchbar Clear Search Button Click
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            ModSearchTextBox.Text = string.Empty;

            //ModSearchTextBox.Focus();
        }

        // ============ Changelog ============

        // Changelog Flyout Event Handler
        private async void ChangelogFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (ChangelogFlyout.IsOpen)
            {
                // Load Changelog
                await LoadChangelogAsync();
            }
            else
            {
                // Clear Changelog
                ChangelogRender.Markdown = "";
            }
        }

        // Load Changelog
        public async Task LoadChangelogAsync()
        {
            using var client = new HttpClient();
            try
            {
                string markdownContent = await client.GetStringAsync(ChangelogUrl);

                var headerColors = new Dictionary<int, string>
                {
                    { 1, "#FFBEC7B6" }, // Color for # H1
                    { 2, "#FFBEC7B6" }, // Color for ## H2
                    { 3, "#FFA0B0A0" }, // Color for ### H3
                    { 4, "#FF809080" }, // Color for #### H4
                    { 5, "#FF607060" }, // Color for ##### H5
                    { 6, "#FF405040" }  // Color for ###### H6
                };

                string processedMarkdown = VersionToColorRegex().Replace(markdownContent, match =>
                {
                    int depth = match.Groups[1].Value.Trim().Length;
                    string hashesAndSpace = match.Groups[1].Value;
                    string headerText = match.Groups[2].Value;

                    if (headerColors.TryGetValue(depth, out string? hexColor))
                    {
                        return $"{hashesAndSpace}%{{color:{hexColor}}}{headerText}%";
                    }

                    return match.Value;
                });

                ChangelogRender.Markdown = processedMarkdown;
            }
            catch (Exception ex)
            {
                ChangelogRender.Markdown = $"# Error\nFailed to load changelog: {ex.Message}";
            }
        }

        // ============ Legacy Data Migration (moving config files from pre-rebrand) ============
        private static void MigrateLegacyUserData()
        {
            try
            {
                if (!Directory.Exists(legacyConfigFolder)) return;

                if (!Directory.Exists(appConfigFolder))
                {
                    Directory.CreateDirectory(appConfigFolder);
                }

                string[] filesToMigrate = ["ModLinks.json", "AppSettings.json"];

                foreach (string fileName in filesToMigrate)
                {
                    string oldFilePath = Path.Combine(legacyConfigFolder, fileName);
                    string newFilePath = Path.Combine(appConfigFolder, fileName);

                    if (File.Exists(oldFilePath))
                    {
                        if (!File.Exists(newFilePath))
                        {
                            File.Move(oldFilePath, newFilePath);
                        }
                        else
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                }

                if (Directory.GetFiles(legacyConfigFolder).Length == 0 && Directory.GetDirectories(legacyConfigFolder).Length == 0)
                {
                    Directory.Delete(legacyConfigFolder);
                }
            }
            catch (Exception ex)
            {
                // Log the failure to debug output so it doesn't crash the app if a file is locked
                Debug.WriteLine($"[Migration Error]: Failed to complete folder migration: {ex.Message}");
            }
        }

        // ============ App Settings Event Handlers ============

        // Default Game Version Selection Changed Event Handler
        private void DefaultGameVersionDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            if (DefaultGameVersionDropdown.SelectedItem != null)
            {
                CurrentSettings.DefaultGameVersion = DefaultGameVersionDropdown.SelectedItem.ToString() ?? "";
                SaveAppSettings(CurrentSettings);
            }
        }

        // Safe Mode Event Handler
        private void SafeModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.SafeMode = SafeModeToggle.IsOn;

            SaveAppSettings(CurrentSettings);
        }

        // Check App Updates Event Handler
        private void CheckAppUpdatesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.CheckForUpdatesOnStartup = CheckAppUpdatesToggle.IsOn;

            SaveAppSettings(CurrentSettings);
        }

        // Show Ignore Column Event Handler
        private void ShowIgnoreToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.ShowIgnoreInTable = ShowIgnoreToggle.IsOn;
            IgnoreColumn.Visibility = CurrentSettings.ShowIgnoreInTable ? Visibility.Visible : Visibility.Collapsed;

            SaveAppSettings(CurrentSettings);
        }

        // Show Mod Pages Column Event Handler
        private void ShowModPagesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.ShowModPagesInTable = ShowModPagesToggle.IsOn;
            ModPagesColumn.Visibility = CurrentSettings.ShowModPagesInTable ? Visibility.Visible : Visibility.Collapsed;

            SaveAppSettings(CurrentSettings);
        }

        // Show File Names Column Event Handler
        private void ShowFileNamesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.ShowFileNamesInTable = ShowFileNamesToggle.IsOn;
            FileNameColumn.Visibility = CurrentSettings.ShowFileNamesInTable ? Visibility.Visible : Visibility.Collapsed;

            SaveAppSettings(CurrentSettings);
        }

        // Show Game Version Column Event Handler
        private void ShowGameVersionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CurrentSettings == null) return;

            CurrentSettings.ShowGameVersionInTable = ShowGameVersionToggle.IsOn;
            GameVersionColumn.Visibility = CurrentSettings.ShowGameVersionInTable ? Visibility.Visible : Visibility.Collapsed;

            SaveAppSettings(CurrentSettings);
        }

        // ============ Models ============

        // App Settings Model
        private class AppSettings
        {
            public string ModsFolderPath { get; set; } = "";

            public string DefaultGameVersion { get; set; } = "1.22.x"; // 1.22.x for now doesn't matter that much as the user can override it at any time

            public bool SafeMode { get; set; } = true; // True by default

            public bool CheckForUpdatesOnStartup { get; set; } = true; // True by default

            public bool ShowIgnoreInTable { get; set; } = true; // True by default
            public bool ShowModPagesInTable { get; set; } = true; // True by default
            public bool ShowFileNamesInTable { get; set; } = true; // True by default
            public bool ShowGameVersionInTable { get; set; } = true; // True by default
        }

        // Game Version Model
        public class GamesVersions
        {
            public List<string>? GameVersionsList { get; set; }
        }

        // Mod Scan Model
        private struct ModScanResult
        {
            public string FilePath { get; set; }
            public string ModId { get; set; }
            public NuGetVersion Version { get; set; }
        }

        // DataGrid Model
        public class ModInfo
        {
            public string FileName { get; set; } = "";
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string? Game { get; set; }
            public string? Description { get; set; }
            public string LatestVersion { get; set; } = "";
            public string ModPageUrl { get; set; } = "";
            public string ModDownloadUrl { get; set; } = "";

            public string ModId { get; set; } = "";

            private bool disableUpdate;

            public bool HasVersionMismatch { get; set; }

            public bool DisableUpdate
            {
                get => disableUpdate;
                set
                {
                    if (disableUpdate != value)
                    {
                        disableUpdate = value;
                        MainWindow.SaveDisableUpdateForMod(ModId, disableUpdate);
                    }
                }
            }

            public bool HasUpdate => !DisableUpdate &&
                         !string.IsNullOrWhiteSpace(LatestVersion) &&
                         !string.IsNullOrWhiteSpace(Version) &&
                         IsVersionGreater(LatestVersion, Version);
        }

        // ModLink Model
        private class ModLinkInfo
        {
            public string ModPageUrl { get; set; } = "";
            public string ModDownloadUrl { get; set; } = "";
            public bool DisableUpdate { get; set; } = false;
            public bool HasVersionMismatch { get; set; } = false;
        }

        [GeneratedRegex(@"^(#{1,6}\s+)(.+)$", RegexOptions.Multiline)]
        private static partial Regex VersionToColorRegex();

        // End of class
    }
}