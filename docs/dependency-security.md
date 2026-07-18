# Dependency security

PayBridge treats high and critical NuGet advisories as release blockers. The
dependency security workflow audits direct and transitive packages on every
pull request to `master`, on security branch pushes, and every Monday.
Dependabot alerts and automated security updates are enabled at repository
level, while `.github/dependabot.yml` schedules weekly NuGet and GitHub Actions
version updates.

Run the same audit locally:

```bash
./scripts/check-vulnerable-packages.sh
```

For credential exposure response (rotation + history rewrite), follow:

- [Credential rotation and history purge](./credential-rotation-and-history-purge.md)

## July 2026 remediation

The .NET 8 dependency graph previously resolved vulnerable transitive versions
of `System.Text.Json` and `SQLitePCLRaw.lib.e_sqlite3`.

- Entity Framework Core packages were updated from 8.0.15 to the current 8.0.29
  servicing release.
- `System.Text.Json` is pinned to 8.0.6, after the patched 8.0.5 threshold for
  the applicable denial-of-service advisories.
- `SQLitePCLRaw.bundle_e_sqlite3` is pinned to 3.0.3 because EF Core 8.0.29 still
  declares bundle version 2.1.6. Bundle 3.0.3 uses SQLite 3.50.4.5, beyond the
  SQLite 3.50.2 remediation threshold.

These are servicing and transitive dependency changes; no PayBridge gateway API
or database model changes are expected. The SQLite bundle changes the packaged
native SQLite distribution, so SQLite initialization and database smoke tests
must remain part of release verification. SQL Server, PostgreSQL, and MySQL
provider versions are unchanged.
