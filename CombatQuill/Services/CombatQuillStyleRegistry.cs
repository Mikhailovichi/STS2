using CombatQuill.Networking;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace CombatQuill.Services;

internal static class CombatQuillStyleRegistry
{
    private static readonly Dictionary<ulong, CombatQuillStyle> Styles = [];

    private static INetGameService? _netService;

    public static event Action<ulong>? StyleChanged;

    public static void BindToNetService(INetGameService? netService)
    {
        if (ReferenceEquals(_netService, netService))
        {
            return;
        }

        if (_netService is not null)
        {
            _netService.UnregisterMessageHandler<CombatQuillStyleMessage>(HandleStyleMessage);
        }

        Styles.Clear();
        _netService = netService;

        if (_netService is not null)
        {
            _netService.RegisterMessageHandler<CombatQuillStyleMessage>(HandleStyleMessage);
        }

        UpdateLocalFromSettings(CombatQuillSettingsStore.Load(), broadcast: _netService is not null);
    }

    public static CombatQuillStyle GetStyle(Player player)
    {
        if (Styles.TryGetValue(player.NetId, out var style))
        {
            return style;
        }

        style = CombatQuillStyle.CreateFallback(player);
        Styles[player.NetId] = style;
        return style;
    }

    public static void UpdateLocalFromSettings(CombatQuillSettings settings, bool broadcast)
    {
        settings.Normalize();

        if (_netService is null)
        {
            return;
        }

        var style = CombatQuillStyle.FromSettings(settings);
        Styles[_netService.NetId] = style;

        if (broadcast)
        {
            _netService.SendMessage(new CombatQuillStyleMessage(style));
        }

        StyleChanged?.Invoke(_netService.NetId);
    }

    private static void HandleStyleMessage(CombatQuillStyleMessage message, ulong senderId)
    {
        Styles[senderId] = message.ToStyle();
        StyleChanged?.Invoke(senderId);
    }
}
