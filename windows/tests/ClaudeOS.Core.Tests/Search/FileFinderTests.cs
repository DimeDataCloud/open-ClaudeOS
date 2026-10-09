using ClaudeOS.Core.Search;

namespace ClaudeOS.Core.Tests.Search;

public sealed class FileFinderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static FileCandidate F(string path, int daysOld = 10) => new(path, Now.AddDays(-daysOld), 1000);

    private static readonly FileCandidate[] Files =
    [
        F(@"C:\Users\me\Documents\Finance\Q3 Budget.xlsx", 3),
        F(@"C:\Users\me\Documents\Finance\Q3 Budget (old).xlsx", 200),
        F(@"C:\Users\me\Documents\Finance\Q2_budget_final.xlsx", 120),
        F(@"C:\Users\me\Downloads\q3-budget-notes.txt", 1),
        F(@"C:\Users\me\Documents\Taxes\2025 Return.pdf", 150),
        F(@"C:\Users\me\Desktop\passport scan.jpg", 400),
        F(@"C:\Users\me\Documents\Acme\Invoice 2026-09.pdf", 20),
        F(@"C:\Users\me\Documents\Initech\Invoice 2026-09.pdf", 19),
        F(@"C:\Users\me\Pictures\Screenshot 2026-10-08.png", 1),
    ];

    [Fact]
    public void The_obvious_file_wins_and_a_recent_one_beats_an_old_copy()
    {
        var hits = FileFinder.Rank("the Q3 budget", Files, Now);
        Assert.Equal(@"C:\Users\me\Documents\Finance\Q3 Budget.xlsx", hits[0].File.Path);
        Assert.True(hits[0].Score > hits[1].Score);
        Assert.DoesNotContain(hits, h => h.File.Path.Contains("Q2_budget"));
    }

    [Fact]
    public void Every_word_has_to_match_somewhere()
    {
        Assert.Empty(FileFinder.Rank("budget zebra", Files, Now));
    }

    [Fact]
    public void Folders_count_so_the_vendor_name_finds_its_invoice()
    {
        var hit = Assert.Single(FileFinder.Rank("invoice from Acme", Files, Now), h => h.File.Path.Contains("Acme"));
        Assert.Contains("Acme", hit.File.Path);
        Assert.Equal(@"C:\Users\me\Documents\Acme\Invoice 2026-09.pdf", FileFinder.Rank("invoice acme", Files, Now)[0].File.Path);
    }

    [Fact]
    public void A_kind_word_filters_by_extension()
    {
        var pdfs = FileFinder.Rank("pdf invoice", Files, Now);
        Assert.All(pdfs, h => Assert.EndsWith(".pdf", h.File.Path));
        Assert.Equal(2, pdfs.Count);
        Assert.Equal(@"C:\Users\me\Pictures\Screenshot 2026-10-08.png", FileFinder.Rank("latest screenshot", Files, Now)[0].File.Path);
    }

    [Fact]
    public void A_small_typo_is_forgiven_for_longer_words()
    {
        Assert.Contains(FileFinder.Rank("passport scna", Files, Now), h => h.File.Path.Contains("passport")); // 'scna' is a transposition of 'scan'
        Assert.Contains(FileFinder.Rank("pasport scan", Files, Now), h => h.File.Path.Contains("passport"));
    }

    [Fact]
    public void Camel_case_and_punctuation_split_into_words()
    {
        Assert.Equal(["q3", "budget", "final"], FileFinder.Tokenize("Q3_budget-final"));
        Assert.Equal(["quarterly", "report", "v2"], FileFinder.Tokenize("QuarterlyReport.v2"));
    }

    [Fact]
    public void Scanning_skips_noise_and_respects_limits()
    {
        var root = Path.Combine(Path.GetTempPath(), "claudeos-find-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules", "pkg"));
            File.WriteAllText(Path.Combine(root, "docs", "plan.md"), "x");
            File.WriteAllText(Path.Combine(root, "node_modules", "pkg", "index.js"), "x");
            var found = FileFinder.Scan([root]).Select(f => Path.GetFileName(f.Path)).ToList();
            Assert.Equal(["plan.md"], found);
            Assert.Single(FileFinder.Scan([root], maxFiles: 1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Ranking_ten_thousand_files_takes_milliseconds()
    {
        var many = Enumerable.Range(0, 10_000).Select(i => F($@"C:\Users\me\Documents\Project {i % 50}\Report {i} draft.docx", i % 400)).Concat(Files).ToList();
        FileFinder.Rank("q3 budget", many, Now);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var hits = FileFinder.Rank("q3 budget", many, Now);
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 150, $"{clock.ElapsedMilliseconds} ms");
        Assert.Contains("Q3 Budget.xlsx", hits[0].File.Path);
    }
}
