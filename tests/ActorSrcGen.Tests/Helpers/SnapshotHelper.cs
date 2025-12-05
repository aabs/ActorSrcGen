using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ActorSrcGen.Tests.Helpers;

public static class SnapshotHelper
{
    public static string NormalizeLineEndings(string code)
    {
        return code.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static string FormatGeneratedCode(string code)
    {
        var normalized = NormalizeLineEndings(code);
        var lines = normalized.Split('\n');

        if (lines.Length > 0 && lines[0].StartsWith("// Generated on ", StringComparison.Ordinal))
        {
            normalized = string.Join("\n", lines.Skip(1));
        }

        return normalized.Trim();
    }

    public static Task VerifyGeneratedOutput(string code, string fileName, string extension = "cs")
    {
        var formatted = FormatGeneratedCode(code);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(tempPath, formatted);

        var settings = CreateSettings(fileName);
        return Verifier.VerifyFile(tempPath, settings);
    }

    public static VerifySettings CreateSettings(string fileName)
    {
        var settings = new VerifySettings();
        var directory = Path.GetDirectoryName(fileName);
        var name = Path.GetFileName(fileName);

        settings.UseFileName(string.IsNullOrWhiteSpace(name) ? fileName : name);

        var baseDirectory = Path.Combine("..", "Snapshots");
        var targetDirectory = string.IsNullOrWhiteSpace(directory)
            ? baseDirectory
            : Path.Combine(baseDirectory, directory);
        settings.UseDirectory(targetDirectory);

        return settings;
    }
}
