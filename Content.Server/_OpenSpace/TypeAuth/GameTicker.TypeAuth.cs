using Content.Server._OpenSpace.TypeAuth;
using Robust.Shared.Player;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private TypeAuthManager _typeAuth = default!;

    private bool TryBlockTypeAuth(ICommonSession player)
    {
        if (!_typeAuth.IsTypeAuthBlocking(player.UserId))
            return false;
        _chatManager.DispatchServerMessage(player, Loc.GetString("typeauth-must-link-discord"));
        return true;
    }
}
