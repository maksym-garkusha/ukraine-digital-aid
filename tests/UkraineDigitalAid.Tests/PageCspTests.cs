using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UkraineDigitalAid.Tests;

/// <summary>
/// Політика CSP на сторінці дозволяє вбудовані скрипт і стиль лише за хешем.
/// Після будь-якої правки в них хеш змінюється, і без цього тесту сторінка мовчки зламалась би.
/// </summary>
public class PageCspTests
{
    private static readonly string Page = File.ReadAllText(PagePath());

    [Theory]
    [InlineData("script", "script-src")]
    [InlineData("style", "style-src")]
    public void Inline_block_hash_is_allowed_by_the_policy(string tag, string directive)
    {
        var blocks = Regex.Matches(Page, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        string expected = $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Assert.Single(blocks).Groups[1].Value)))}'";

        Assert.True(Directive(directive).Contains(expected),
            $"У site/index.html змінився <{tag}>: у CSP-директиві {directive} має стояти {expected}");
    }

    private static string[] Directive(string name)
    {
        var meta = Regex.Match(Page, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]+)\">");
        Assert.True(meta.Success, "На сторінці немає CSP");
        return meta.Groups[1].Value.Split(';', StringSplitOptions.TrimEntries)
            .Single(d => d.StartsWith(name + ' ', StringComparison.Ordinal))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string PagePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "UkraineDigitalAid.slnx")))
                return Path.Combine(dir.FullName, "site", "index.html");
        throw new FileNotFoundException("Не знайдено корінь репозиторію з UkraineDigitalAid.slnx");
    }
}
