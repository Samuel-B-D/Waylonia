using Basin;

namespace Waylonia;

internal static class ShellGlobals
{
    public const string LayerShell = "zwlr_layer_shell_v1";

    public const string SessionLock = "ext_session_lock_manager_v1";

    public static BasinServices WithoutLayerShell(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Without(LayerShell);
    }

    public static BasinServices WithoutSessionLock(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Without(SessionLock);
    }
}
