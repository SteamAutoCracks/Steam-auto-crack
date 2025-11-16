using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Serilog;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamAutoCrack.Core.Utils;

internal class HttpClientFactory
{
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = IPv4ConnectAsync
        });

        var assemblyVersion = typeof(HttpClientFactory).Assembly.GetName().Version?.ToString(3);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SteamAutoCrack", assemblyVersion));

        return client;
    }

    private static async ValueTask<Stream> IPv4ConnectAsync(SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        // By default, we create dual-mode sockets:
        // Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

internal class Steam3Session
{
    public delegate bool WaitCondition();

    private readonly ILogger _log;

    private readonly CancellationTokenSource abortedToken = new();

    private readonly CallbackManager callbacks;

    // input
    readonly SteamUser.LogOnDetails logonDetails;
    readonly SteamApps steamApps;

    private readonly object steamLock = new();
    private readonly PublishedFile steamPublishedFile;

    public bool bAborted;
    public bool bConnecting;
    public bool bDidDisconnect;
    public bool bExpectingDisconnectRemote;
    public bool bIsConnectionRecovery;
    private int connectionBackoff;
    private int seq; // more hack fixes

    public SteamClient steamClient;
    public SteamUser steamUser;

    public Steam3Session(SteamUser.LogOnDetails details, CancellationToken cancellationToken = default)
    {
        _log = Log.ForContext<Steam3Session>();
        logonDetails = details;

        var clientConfiguration = SteamConfiguration.Create(config =>
            config
                .WithHttpClientFactory(static purpose => HttpClientFactory.CreateHttpClient())
        );

        steamClient = new SteamClient(clientConfiguration);
        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamApps = steamClient.GetHandler<SteamApps>()!;
        var steamUnifiedMessages = steamClient.GetHandler<SteamUnifiedMessages>();
        steamPublishedFile = steamUnifiedMessages!.CreateService<PublishedFile>();

        callbacks = new CallbackManager(steamClient);

        callbacks.Subscribe<SteamClient.ConnectedCallback>(ConnectedCallback);
        callbacks.Subscribe<SteamClient.DisconnectedCallback>(DisconnectedCallback);
        callbacks.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);

        Connect(cancellationToken);
    }

    public bool IsLoggedOn { get; private set; }

    public Dictionary<uint, ulong> AppTokens { get; } = [];
    public Dictionary<uint, ulong> PackageTokens { get; } = [];
    public Dictionary<uint, byte[]> DepotKeys { get; } = [];

    public ConcurrentDictionary<(uint, string), TaskCompletionSource<SteamContent.CDNAuthToken>>
        CDNAuthTokens { get; } = [];

    public Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> AppInfo { get; } = [];
    public Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> PackageInfo { get; } = [];
    public Dictionary<string, byte[]> AppBetaPasswords { get; } = [];

    public async Task RequestAppInfo(uint appId, bool bForce = false)
    {
        if ((AppInfo.ContainsKey(appId) && !bForce) || bAborted)
            return;

        var appTokens = await steamApps.PICSGetAccessTokens([appId], []);

        if (appTokens.AppTokensDenied.Contains(appId))
            _log.Warning("Insufficient privileges to get access token for app {0}", appId);

        foreach (var token_dict in appTokens.AppTokens) AppTokens[token_dict.Key] = token_dict.Value;

        var request = new SteamApps.PICSRequest(appId);

        if (AppTokens.TryGetValue(appId, out var token)) request.AccessToken = token;

        var appInfoMultiple = await steamApps.PICSGetProductInfo([request], []);

        foreach (var appInfo in appInfoMultiple.Results!)
        {
            foreach (var app_value in appInfo.Apps)
            {
                var app = app_value.Value;

                _log.Debug("Got AppInfo for {0}", app.ID);
                AppInfo[app.ID] = app;
            }

            foreach (var app in appInfo.UnknownApps) AppInfo[app] = null!;
        }
    }

    public async Task<PublishedFileDetails> GetPublishedFileDetails(PublishedFileID pubFile)
    {
        var pubFileRequest = new CPublishedFile_GetDetails_Request();
        pubFileRequest.publishedfileids.Add(pubFile);

        var details = await steamPublishedFile.GetDetails(pubFileRequest);

        if (details.Result == EResult.OK) return details.Body.publishedfiledetails.FirstOrDefault()!;

        throw new Exception(
            $"EResult {(int)details.Result} ({details.Result}) while retrieving file details for pubfile {pubFile}.");
    }

    public bool WaitUntilCallback(Action submitter, WaitCondition waiter, CancellationToken cancellationToken = default)
    {
        while (!bAborted && !waiter())
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (steamLock)
            {
                submitter();
            }

            var seq = this.seq;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (steamLock)
                {
                    callbacks.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                }
            } while (!bAborted && this.seq == seq && !waiter());
        }

        return bAborted;
    }


    private void ResetConnectionFlags()
    {
        bExpectingDisconnectRemote = false;
        bDidDisconnect = false;
        bIsConnectionRecovery = false;
    }

    private void Connect(CancellationToken cancellationToken = default)
    {
        _log.Debug("Connecting to Steam3...");

        bAborted = false;
        bConnecting = true;
        connectionBackoff = 0;

        ResetConnectionFlags();

        steamClient.Connect();

        cancellationToken.Register(() =>
        {
            _log.Debug("Cancellation requested, disconnecting...");
            Abort(false);
        });
    }

    private void Abort(bool sendLogOff = true)
    {
        Disconnect(sendLogOff);
    }

    public void Disconnect(bool sendLogOff = true)
    {
        if (sendLogOff) steamUser.LogOff();

        bAborted = true;
        bConnecting = false;
        bIsConnectionRecovery = false;
        abortedToken.Cancel();
        steamClient.Disconnect();

        // flush callbacks until our disconnected event
        while (!bDidDisconnect) callbacks.RunWaitAllCallbacks(TimeSpan.FromMilliseconds(100));
    }

    private void Reconnect()
    {
        bIsConnectionRecovery = true;
        steamClient.Disconnect();
    }

    private void ConnectedCallback(SteamClient.ConnectedCallback connected)
    {
        _log.Debug("Connected to Steam3! Logging anonymously into Steam3...");
        bConnecting = false;
        connectionBackoff = 0;
        steamUser.LogOnAnonymous();
    }

    private void DisconnectedCallback(SteamClient.DisconnectedCallback disconnected)
    {
        bDidDisconnect = true;

        // When recovering the connection, we want to reconnect even if the remote disconnects us
        if (!bIsConnectionRecovery && (disconnected.UserInitiated || bExpectingDisconnectRemote))
        {
            _log.Debug("Disconnected from Steam");

            // Any operations outstanding need to be aborted
            bAborted = true;
        }
        else if (connectionBackoff >= 10)
        {
            _log.Error("Could not connect to Steam after 10 tries");
            Abort(false);
        }
        else if (!bAborted)
        {
            connectionBackoff += 1;

            if (bConnecting)
                _log.Warning("Connection to Steam failed. Trying again.");
            else
                _log.Warning("Lost connection to Steam. Reconnecting...");

            Thread.Sleep(1000 * connectionBackoff);

            // Any connection related flags need to be reset here to match the state after Connect
            ResetConnectionFlags();
            steamClient.Connect();
        }
    }

    private void LogOnCallback(SteamUser.LoggedOnCallback loggedOn)
    {
        if (loggedOn.Result == EResult.TryAnotherCM)
        {
            _log.Debug("Retrying Steam3 connection (TryAnotherCM)...");

            Reconnect();

            return;
        }

        if (loggedOn.Result == EResult.ServiceUnavailable)
        {
            _log.Error("Unable to login to Steam3: {0}", loggedOn.Result);
            Abort(false);

            return;
        }

        if (loggedOn.Result != EResult.OK)
        {
            _log.Error("Unable to login to Steam3: {0}", loggedOn.Result);
            Abort();

            return;
        }

        _log.Debug("Logged on to Steam3!");

        seq++;
        IsLoggedOn = true;
    }

    public class Credentials
    {
        public bool LoggedOn { get; set; }
        public ulong SessionToken { get; set; }

        public bool IsValid => LoggedOn;
    }
}