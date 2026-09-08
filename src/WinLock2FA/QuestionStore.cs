using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinLock2FA;

public class QuestionEntry
{
    public string Question { get; set; } = "";
    public string SaltBase64 { get; set; } = "";
    public string HashBase64 { get; set; } = "";
}

/// <summary>
/// Stores security question/answer pairs for the current Windows user only.
/// Answers are never stored in plain text: each is salted and hashed with
/// SHA-256, and the whole file is additionally encrypted with Windows DPAPI
/// (CurrentUser scope), so it can only be decrypted by the same Windows
/// account on the same machine.
/// </summary>
public static class QuestionStore
{
    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinLock2FA",
            "questions.dat");

    public static List<QuestionEntry> Load()
    {
        if (!File.Exists(FilePath))
            return new List<QuestionEntry>();

        try
        {
            var protectedBytes = File.ReadAllBytes(FilePath);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<List<QuestionEntry>>(json) ?? new List<QuestionEntry>();
        }
        catch
        {
            // Corrupt or unreadable (e.g. different user account) -> treat as empty.
            return new List<QuestionEntry>();
        }
    }

    public static void Save(List<QuestionEntry> entries)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(entries);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedBytes);
    }

    public static QuestionEntry CreateEntry(string question, string answer)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashAnswer(salt, answer);
        return new QuestionEntry
        {
            Question = question,
            SaltBase64 = Convert.ToBase64String(salt),
            HashBase64 = Convert.ToBase64String(hash),
        };
    }

    public static bool CheckAnswer(QuestionEntry entry, string answer)
    {
        var salt = Convert.FromBase64String(entry.SaltBase64);
        var expected = Convert.FromBase64String(entry.HashBase64);
        var actual = HashAnswer(salt, answer);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] HashAnswer(byte[] salt, string answer)
    {
        var normalized = answer.Trim().ToLowerInvariant();
        var input = new byte[salt.Length + Encoding.UTF8.GetByteCount(normalized)];
        Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
        Encoding.UTF8.GetBytes(normalized, 0, normalized.Length, input, salt.Length);
        return SHA256.HashData(input);
    }
}
