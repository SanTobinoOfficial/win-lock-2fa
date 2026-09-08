namespace WinLock2FA;

/// <summary>
/// Central definition of the emergency/debug override code, shared by the
/// lock screen (force-close) and the main menu (required to disable
/// protection). This is NOT a secret - it's committed in this public repo's
/// source, so it gives zero protection against anyone who can read the
/// code. It exists purely as a convenience escape hatch for the person
/// building/running this on their own machine. Change it before relying on
/// this app for anything more than that.
/// </summary>
internal static class DebugCode
{
    public const string Value = "0000";
}
