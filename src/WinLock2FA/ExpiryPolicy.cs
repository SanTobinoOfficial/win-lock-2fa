namespace WinLock2FA;

/// <summary>
/// One-time expiry date after which the lock stops enforcing itself: at
/// logon it silently tries to remove its own scheduled task instead of
/// showing a question. Not renewed automatically - bump this manually if
/// the app is still wanted for a later school year.
/// </summary>
internal static class ExpiryPolicy
{
    public static readonly DateTime ExpiryDate = new(2027, 6, 26);

    public static bool HasExpired => DateTime.Now.Date >= ExpiryDate;
}
