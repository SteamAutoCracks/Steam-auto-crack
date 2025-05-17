using System.Text.Json;
using System.Text.Json.Serialization;
using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer.Composite;
using NinjaNye.SearchExtensions;
using Serilog;
using SQLite;

namespace SteamAutoCrack.Core.Utils;

[Table("steamapp")]
public class SteamApp
{
    [JsonPropertyName("appid")]
    [Column("appid")]
    [PrimaryKey]
    public uint? AppId { get; set; }

    [JsonPropertyName("name")]
    [Column("name")]
    public string? Name { get; set; }

    public override string ToString()
    {
        return $"{AppId}={Name}";
    }
}

public class AppList
{
    [JsonPropertyName("apps")] public List<SteamApp>? Apps { get; set; }
}

public class SteamAppsV2
{
    [JsonPropertyName("applist")] public AppList? AppList { get; set; }
}

public class SteamAppList
{
    private const int FuzzySearchScore = 80;

    private static readonly string steamapplisturl = "https://api.steampowered.com/ISteamApps/GetAppList/v2/";

    private static readonly ILogger _log = Log.ForContext<SteamAppList>();

    private static bool bInited;

    private static bool bDisposed;

    private static readonly string Database = Path.Combine(Config.Config.TempPath, "SteamAppList.db");

    public static SQLiteAsyncConnection? db;

    private static TaskCompletionSource<bool> _initializationTcs = new();

    public static async Task Initialize(bool forceupdate = false)
    {
        try
        {
            bDisposed = true;
            _log.Debug("Initializing Steam App list...");
            if (!Directory.Exists(Config.Config.TempPath))
                Directory.CreateDirectory(Config.Config.TempPath);

            _initializationTcs = new TaskCompletionSource<bool>();

            db = new SQLiteAsyncConnection(Database);
            await db.CreateTableAsync<SteamApp>().ConfigureAwait(false);
            var count = await db.Table<SteamApp>().CountAsync().ConfigureAwait(false);

            bool dbExistsWithData = File.Exists(Database) && count > 0;
            bool needsUpdate = DateTime.Now.Subtract(File.GetLastWriteTimeUtc(Database)).TotalDays >= 1 || count == 0 || forceupdate;
            if (bInited && !needsUpdate && !forceupdate)
            {
                _log.Debug("Already initialized Steam App list.");
                return;
            }

            // Make db firstly inited if there's data
            if (dbExistsWithData)
            {
                _log.Debug("Database exists with {count} apps. Marking as initialized.", count);
                bInited = true;
                _initializationTcs.TrySetResult(true);
            }

            if (needsUpdate)
            {
                while (true)
                {
                    try
                    {
                        _log.Information("Updating Steam App list...");
                        using var client = new HttpClient();
                        var response = await client.GetAsync(steamapplisturl).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var steamApps = DeserializeSteamApps(responseBody);
                        var appList = new HashSet<SteamApp>();
                        if (steamApps?.AppList?.Apps != null)
                        {
                            foreach (var app in steamApps.AppList.Apps)
                                appList.Add(app);
                        }

                        await db.InsertAllAsync(appList, "OR IGNORE").ConfigureAwait(false);
                        _log.Information("Updated Steam App list.");

                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Failed to initialize Steam App list, Retrying...", ex);
                    }
                }
            }
            else
            {
                _log.Information("Applist already updated to latest version.");
            }

            if (!dbExistsWithData)
            {
                bInited = true;
                _initializationTcs.TrySetResult(true);
            }

            var updatedCount = await db.Table<SteamApp>().CountAsync().ConfigureAwait(false);
            _log.Information("Initialized Steam App list, App Count: {count}", updatedCount);
            return;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to initialize Steam App list.", ex);
            bDisposed = false;
            return;
        }
    }

    public static async Task WaitForReady()
    {
        if (bDisposed == false)
        {
            _log.Error("Not initialized Steam App list.");
            throw new Exception("Not initialized Steam App list.");
        }

        _log.Debug("Waiting for Steam App list initialized...");
        await _initializationTcs.Task.ConfigureAwait(false);
    }

    private static SteamAppsV2? DeserializeSteamApps(string json)
    {
        var data = JsonSerializer.Deserialize<SteamAppsV2>(json);
        return data ?? new SteamAppsV2 { AppList = new AppList { Apps = new List<SteamApp>() } };
    }

    public static async Task<IEnumerable<SteamApp>> GetListOfAppsByName(string name)
    {
        var query = await db!.Table<SteamApp>().ToListAsync().ConfigureAwait(false);
        var SearchOfAppsByName = query.Search(x => x.Name)
            .SetCulture(StringComparison.OrdinalIgnoreCase)
            .ContainingAll(name.Split(' '));
        var listOfAppsByName = SearchOfAppsByName.ToList();
        if (uint.TryParse(name, out var appid))
        {
            var app = await GetAppById(appid).ConfigureAwait(false);
            var appToRemove = listOfAppsByName.Find(d => d.AppId == appid);
            if (appToRemove != null) listOfAppsByName.Remove(appToRemove);
            if (app != null) listOfAppsByName.Insert(0, app);
        }

        return listOfAppsByName;
    }

    public static async Task<IEnumerable<SteamApp>> GetListOfAppsByNameFuzzy(string name)
    {
        var query = await db!.Table<SteamApp>().ToListAsync().ConfigureAwait(false);
        var listOfAppsByName = new List<SteamApp>();
        var results = Process.ExtractTop(new SteamApp { Name = name }, query, x => x.Name?.ToLower(),
            ScorerCache.Get<WeightedRatioScorer>(), FuzzySearchScore);
        foreach (var item in results) listOfAppsByName.Add(item.Value);

        if (uint.TryParse(name, out var appid))
        {
            var app = await GetAppById(appid).ConfigureAwait(false);
            var appToRemove = listOfAppsByName.Find(d => d.AppId == appid);
            if (appToRemove != null) listOfAppsByName.Remove(appToRemove);
            if (app != null) listOfAppsByName.Insert(0, app);
        }

        return listOfAppsByName;
    }

    public static async Task<SteamApp> GetAppByName(string name)
    {
        _log?.Debug($"Trying to get app name for app: {name}");
        var app = await db!.Table<SteamApp>()
            .FirstOrDefaultAsync(x => x.Name != null && x.Name.Equals(name))
            .ConfigureAwait(false);
        if (app != null) _log?.Debug($"Successfully got app name for app: {app}");
        return app!;
    }

    public static async Task<SteamApp> GetAppById(uint appid)
    {
        _log?.Debug($"Trying to get app with ID {appid}");
        var app = await db!.Table<SteamApp>().FirstOrDefaultAsync(x => x.AppId.Equals(appid)).ConfigureAwait(false);
        if (app != null) _log?.Debug($"Successfully got app {app}");
        return app!;
    }
}