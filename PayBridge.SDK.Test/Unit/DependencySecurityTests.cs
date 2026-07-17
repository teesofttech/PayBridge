using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class DependencySecurityTests
{
    [Fact]
    public void Embedded_sqlite_version_must_include_the_security_fix()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";

        var versionText = command.ExecuteScalar().Should().BeOfType<string>().Subject;
        Version.Parse(versionText).Should().BeGreaterThanOrEqualTo(
            new Version(3, 50, 2),
            "CVE-2025-6965 is fixed in SQLite 3.50.2 and later");
    }
}
