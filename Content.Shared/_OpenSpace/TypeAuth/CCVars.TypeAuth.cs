using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> TypeAuthEnabled =
        CVarDef.Create("typeauth.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TypeAuthBaseUrl =
        CVarDef.Create("typeauth.base_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TypeAuthApiSecret =
        CVarDef.Create("typeauth.api_secret", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
