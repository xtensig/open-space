using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._OpenSpace.TypeAuth;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._OpenSpace.TypeAuth;

public sealed class TypeAuthManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ILogManager _log = default!;

    private static readonly HttpClient Http = new();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private ISawmill _sawmill = default!;

    private bool _enabled;
    private string _baseUrl = "";
    private string _secret = "";

    private readonly HashSet<NetUserId> _confirmedLinked = new();
    private readonly HashSet<NetUserId> _pendingCheck = new();

    public void Initialize()
    {
        _sawmill = _log.GetSawmill("typeauth");
        _cfg.OnValueChanged(CCVars.TypeAuthEnabled, v => _enabled = v, true);
        _cfg.OnValueChanged(CCVars.TypeAuthBaseUrl, v => _baseUrl = v.TrimEnd('/'), true);
        _cfg.OnValueChanged(CCVars.TypeAuthApiSecret, v => _secret = v, true);

        _netManager.RegisterNetMessage<TypeAuthShowMessage>();
        _netManager.RegisterNetMessage<TypeAuthCheckMessage>(OnCheckMessage);
        _netManager.RegisterNetMessage<TypeAuthResultMessage>();

        _netManager.Connected += OnConnected;
        _netManager.Disconnect += OnDisconnected;
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        if (!_enabled)
            return;

        var userId = e.Channel.UserId;

        try
        {
            var discordId = await _db.GetPlayerDiscordIdAsync(userId);
            if (discordId != null)
            {
                _confirmedLinked.Add(userId);
                return;
            }

            var link = await GetAuthLinkAsync(userId.ToString());
            if (link == null)
            {
                _sawmill.Error($"Failed to get typeauthd link for user {userId}");
                return;
            }

            _netManager.ServerSendMessage(new TypeAuthShowMessage { AuthLink = link }, e.Channel);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TypeAuth error for user {userId}: {ex}");
        }
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs e)
    {
        _confirmedLinked.Remove(e.Channel.UserId);
        _pendingCheck.Remove(e.Channel.UserId);
    }

    private async void OnCheckMessage(TypeAuthCheckMessage message)
    {
        var userId = message.MsgChannel.UserId;

        if (!_pendingCheck.Add(userId))
            return;

        try
        {
            var (ok, discordId, error) = await CheckAsync(userId);

            if (ok && discordId != null)
            {
                _confirmedLinked.Add(userId);
                await _db.SetPlayerDiscordIdAsync(userId, discordId);
            }

            _netManager.ServerSendMessage(
                new TypeAuthResultMessage { Success = ok, ErrorMessage = error },
                message.MsgChannel);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TypeAuth check error for user {userId}: {ex}");
        }
        finally
        {
            _pendingCheck.Remove(userId);
        }
    }

    public bool IsTypeAuthBlocking(NetUserId userId)
    {
        if (!_enabled)
            return false;
        return !_confirmedLinked.Contains(userId);
    }

    private async Task<(bool Ok, string? DiscordId, string? Error)> CheckAsync(NetUserId userId)
    {
        try
        {
            var url = $"{_baseUrl}/api/identify?method=uid&id={Uri.EscapeDataString(userId.ToString())}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secret);

            using var cts = new CancellationTokenSource(RequestTimeout);
            using var response = await Http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                return (false, null, $"Not linked (HTTP {(int)response.StatusCode})");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
                return (false, null, "Unexpected response from auth server");

            var discordId = idProp.GetString();
            if (string.IsNullOrEmpty(discordId))
                return (false, null, "Empty discord id in response");

            return (true, discordId, null);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TypeAuth CheckAsync error: {ex}");
            return (false, null, "Auth server error");
        }
    }

    private async Task<string?> GetAuthLinkAsync(string uid)
    {
        try
        {
            var url = $"{_baseUrl}/api/link?uid={Uri.EscapeDataString(uid)}";
            using var cts = new CancellationTokenSource(RequestTimeout);
            using var response = await Http.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Error($"typeauthd link endpoint returned {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("link", out var linkProp))
                return null;

            return linkProp.GetString();
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TypeAuth GetAuthLinkAsync error: {ex}");
            return null;
        }
    }
}
