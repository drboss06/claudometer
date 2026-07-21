using System.Diagnostics;
using System.Text.Json;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Resources;

namespace ClaudeUsageTray.Infrastructure;

internal sealed class CredentialsProvider
{
    private static readonly string[] OAuthContainerNames =
    [
        "claudeAiOauth",
        "claudeAiOAuth",
        "oauthAccount",
        "oauth"
    ];

    public async Task<ClaudeCredentials> GetAsync(CancellationToken cancellationToken)
    {
        var environmentToken = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(environmentToken))
        {
            return new ClaudeCredentials(
                environmentToken.Trim(),
                null,
                "CLAUDE_CODE_OAUTH_TOKEN",
                null);
        }

        foreach (var path in GetNativeCredentialPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var credentials = TryParse(json, $"Windows: {path}");
            if (credentials is not null)
            {
                return credentials;
            }
        }

        var wslCredentials = await TryReadFromDefaultWslAsync(cancellationToken);
        if (wslCredentials is not null)
        {
            return wslCredentials;
        }

        throw new CredentialsNotFoundException(Strings.CredentialsNotFound);
    }

    internal static ClaudeCredentials? TryParse(string json, string source)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var oauth = FindOAuthContainer(root);

            var accessToken = ReadString(oauth, "accessToken")
                ?? ReadString(oauth, "access_token")
                ?? ReadString(root, "accessToken")
                ?? ReadString(root, "access_token");

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            var expiresAt = ReadExpiration(oauth) ?? ReadExpiration(root);
            var subscriptionType = ReadString(oauth, "subscriptionType")
                ?? ReadString(root, "subscriptionType");

            return new ClaudeCredentials(accessToken, expiresAt, source, subscriptionType);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> GetNativeCredentialPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".claude", ".credentials.json");
        yield return Path.Combine(home, ".config", "claude", ".credentials.json");
    }

    private static async Task<ClaudeCredentials?> TryReadFromDefaultWslAsync(CancellationToken cancellationToken)
    {
        // Bound the WSL launch: a cold/wedged distro must not hang the refresh loop.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        var token = timeoutCts.Token;

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--exec");
            startInfo.ArgumentList.Add("sh");
            // Plain (non-login) shell: reading a file needs no profile scripts.
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("cat \"$HOME/.claude/.credentials.json\" 2>/dev/null");

            process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // Drain both pipes concurrently so a chatty stderr cannot deadlock the child.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var output = await stdoutTask;
            await stderrTask;

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? TryParse(output, "Default WSL: ~/.claude/.credentials.json")
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillQuietly(process);
            throw; // Genuine application shutdown.
        }
        catch (OperationCanceledException)
        {
            KillQuietly(process); // Our own 5s timeout: the launch is wedged.
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void KillQuietly(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Best effort.
        }
    }

    private static JsonElement FindOAuthContainer(JsonElement root)
    {
        foreach (var name in OAuthContainerNames)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(name, out var candidate)
                && candidate.ValueKind == JsonValueKind.Object)
            {
                return candidate;
            }
        }

        return root;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static DateTimeOffset? ReadExpiration(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "expiresAt", "expires_at" })
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                // Claude Code has used Unix milliseconds for this field.
                return number > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(number)
                    : DateTimeOffset.FromUnixTimeSeconds(number);
            }

            if (value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}

internal sealed class CredentialsNotFoundException(string message) : Exception(message);
