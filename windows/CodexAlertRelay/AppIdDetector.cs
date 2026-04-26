using System.Diagnostics;
using System.Text.Json;

namespace CodexAlertRelay;

public sealed class AppIdDetector
{
    public async Task<IReadOnlyList<AppIdCandidate>> DetectAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<AppIdCandidate>();
        candidates.AddRange(await DetectFromStartAppsAsync(cancellationToken));
        candidates.AddRange(DetectFromProcesses());

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AppId) || !string.IsNullOrWhiteSpace(candidate.Path))
            .GroupBy(candidate => candidate.AppId + "|" + candidate.Source + "|" + candidate.Path)
            .Select(group => group.First())
            .OrderBy(candidate => ConfidenceRank(candidate.Confidence))
            .ThenBy(candidate => candidate.AppId)
            .ToList();
    }

    private static async Task<IReadOnlyList<AppIdCandidate>> DetectFromStartAppsAsync(CancellationToken cancellationToken)
    {
        var command = """
            Get-StartApps |
              Where-Object { $_.Name -match 'Codex|OpenAI' -or $_.AppID -match 'Codex|OpenAI' } |
              Select-Object Name, AppID |
              ConvertTo-Json -Compress
            """;

        var output = await RunPowerShellAsync(command, cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var result = new List<AppIdCandidate>();
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                AddStartAppCandidate(result, item);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddStartAppCandidate(result, document.RootElement);
        }

        return result;
    }

    private static void AddStartAppCandidate(List<AppIdCandidate> result, JsonElement item)
    {
        var name = item.TryGetProperty("Name", out var nameProperty) ? nameProperty.GetString() ?? "" : "";
        var appId = item.TryGetProperty("AppID", out var appIdProperty) ? appIdProperty.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        var confidence = name.Equals("Codex", StringComparison.OrdinalIgnoreCase) ||
                         appId.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase)
            ? "high"
            : "medium";
        result.Add(new AppIdCandidate(appId, name, "Start menu app registration", confidence, ""));
    }

    private static IEnumerable<AppIdCandidate> DetectFromProcesses()
    {
        foreach (var process in Process.GetProcesses())
        {
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var processName = process.ProcessName;
            var looksLikeCodex = processName.Equals("Codex", StringComparison.OrdinalIgnoreCase) ||
                                 processName.Equals("codex", StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains(@"\Codex\", StringComparison.OrdinalIgnoreCase);
            if (!looksLikeCodex)
            {
                continue;
            }

            var derived = TryDeriveAppIdFromWindowsAppsPath(path);
            if (!string.IsNullOrWhiteSpace(derived))
            {
                yield return new AppIdCandidate(derived, "Codex", "Running process package path", "medium", path);
            }
            else
            {
                yield return new AppIdCandidate("", processName, "Running process", "low", path);
            }
        }
    }

    private static string TryDeriveAppIdFromWindowsAppsPath(string path)
    {
        var marker = @"WindowsApps\";
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return "";
        }

        var rest = path[(markerIndex + marker.Length)..];
        var firstSlash = rest.IndexOf('\\');
        if (firstSlash < 0)
        {
            return "";
        }

        var packageFolder = rest[..firstSlash];
        var publisherSeparator = packageFolder.LastIndexOf("__", StringComparison.Ordinal);
        if (publisherSeparator < 0)
        {
            return "";
        }

        var publisher = packageFolder[(publisherSeparator + 2)..];
        var prefixWithVersion = packageFolder[..publisherSeparator];
        var parts = prefixWithVersion.Split('_');
        if (parts.Length < 2)
        {
            return "";
        }

        var name = parts[0];
        return $"{name}_{publisher}!App";
    }

    private static async Task<string> RunPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start powershell.exe.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output.Trim();
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence.ToLowerInvariant() switch
        {
            "high" => 0,
            "medium" => 1,
            _ => 2
        };
    }
}
