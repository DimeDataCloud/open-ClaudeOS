using Windows.Security.Credentials;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// The Claude API key, stored with Windows' own per-user encryption (the Credential Locker). It is
/// never written to the repository, the audit log or a settings file. If no key is stored,
/// <c>ANTHROPIC_API_KEY</c> is used, so a developer's environment keeps working.
/// </summary>
internal static class KeyVault
{
    private const string Resource = "open-ClaudeOS";
    private const string User = "anthropic";

    public static string? Get()
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, User);
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(credential.Password) ? Fallback() : credential.Password;
        }
        catch (Exception)
        {
            // Retrieve throws when nothing is stored yet.
            return Fallback();
        }
    }

    public static bool Has => !string.IsNullOrWhiteSpace(Get());

    public static void Set(string key)
    {
        var vault = new PasswordVault();
        Clear();
        vault.Add(new PasswordCredential(Resource, User, key.Trim()));
    }

    public static void Clear()
    {
        try
        {
            var vault = new PasswordVault();
            vault.Remove(vault.Retrieve(Resource, User));
        }
        catch (Exception)
        {
            // Nothing stored.
        }
    }

    private static string? Fallback() => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
}
