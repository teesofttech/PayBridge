using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class RepositorySecurityTests
{
    private static readonly HashSet<string> SensitiveSettingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AccessToken",
        "ApiKey",
        "ApiSecret",
        "ApiToken",
        "ClientSecret",
        "CompanyToken",
        "EncryptionKey",
        "Passkey",
        "Password",
        "PrivateKey",
        "SecretKey",
        "WebhookSecret",
        "WebhookSecretHash",
        "TerminalResourceKey"
    };

    [Fact]
    public void Sample_configuration_must_not_contain_committed_credentials()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = Directory
            .EnumerateFiles(repositoryRoot, "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path => FindCredentialViolations(repositoryRoot, path))
            .ToList();

        violations.Should().BeEmpty(
            "sample configuration must use empty or clearly marked placeholder values; " +
            "store developer credentials in .NET user-secrets or environment variables instead");
    }

    private static IEnumerable<string> FindCredentialViolations(string repositoryRoot, string filePath)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(filePath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

        return FindCredentialViolations(
                document.RootElement,
                Path.GetRelativePath(repositoryRoot, filePath),
                string.Empty)
            .ToList();
    }

    private static IEnumerable<string> FindCredentialViolations(
        JsonElement element,
        string relativeFilePath,
        string jsonPath)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                foreach (var violation in FindCredentialViolations(
                             item,
                             relativeFilePath,
                             $"{jsonPath}:{index}"))
                {
                    yield return violation;
                }

                index++;
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = string.IsNullOrEmpty(jsonPath)
                ? property.Name
                : $"{jsonPath}:{property.Name}";

            if (SensitiveSettingNames.Contains(property.Name) &&
                property.Value.ValueKind == JsonValueKind.String &&
                !IsSafePlaceholder(property.Value.GetString()))
            {
                yield return $"{relativeFilePath} -> {propertyPath}";
            }

            foreach (var violation in FindCredentialViolations(
                         property.Value,
                         relativeFilePath,
                         propertyPath))
            {
                yield return violation;
            }
        }
    }

    private static bool IsSafePlaceholder(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
               (value.StartsWith("${", StringComparison.Ordinal) &&
                value.EndsWith('}')) ||
               (value.StartsWith("__", StringComparison.Ordinal) &&
                value.EndsWith("__", StringComparison.Ordinal));
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PayBridge.SDK.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PayBridge repository root.");
    }
}
