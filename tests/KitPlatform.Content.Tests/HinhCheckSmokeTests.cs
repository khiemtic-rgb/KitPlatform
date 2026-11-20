using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Content.Tests;

public class HinhCheckSmokeTests
{
    [Fact]
    public void Checker_accepts_a_matching_plan_and_rejects_a_wrong_line_a_banned_picture_and_a_foreign_number()
    {
        var root = RepoRoot();
        var script = Path.Combine(root, "tools", "kit-video-library", "ngv-edu", "hinh_check.py");
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{script}\" --smoke",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("python did not start");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(60_000);
        Assert.True(process.HasExited);
        Assert.Equal(0, process.ExitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo");
    }
}
