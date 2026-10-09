using ClaudeOS.Core.Search;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// The scoped scan behind "open the Q3 budget": Desktop, Documents and Downloads, refreshed in the
/// background and ranked by <see cref="FileFinder"/>. The Windows Search index will replace it as
/// the first source (milliseconds, whole disk); this is the fallback and works with no setup.
/// </summary>
internal sealed class FileIndex
{
    private volatile IReadOnlyList<FileCandidate> _files = [];
    private DateTimeOffset _scannedAt = DateTimeOffset.MinValue;
    private int _scanning;

    public int Count => _files.Count;

    public static IEnumerable<string> Roots()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(profile, "Downloads"),
        }.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Starts a refresh if the last one is stale. Never blocks the caller.</summary>
    public void RefreshIfStale(TimeSpan maxAge)
    {
        if (DateTimeOffset.UtcNow - _scannedAt < maxAge || Interlocked.Exchange(ref _scanning, 1) == 1)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                _files = [.. FileFinder.Scan(Roots())];
                _scannedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be read just contributes nothing.
            }
            finally
            {
                Interlocked.Exchange(ref _scanning, 0);
            }
        });
    }

    /// <summary>The most recently changed files, newest first.</summary>
    public IReadOnlyList<string> Recent(int take) => [.. _files.OrderByDescending(f => f.Modified).Take(take).Select(f => f.Path)];

    public IReadOnlyList<FileMatch> Search(string query, int take = 6) => FileFinder.Rank(query, _files, DateTimeOffset.UtcNow, take);
}
