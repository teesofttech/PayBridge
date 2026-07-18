# Credential Rotation And History Purge

This runbook is used for security incident response when credentials are
accidentally committed to the repository.

It covers issue #60: rotate exposed payment credentials and purge them from
git history.

## 1. Immediate containment

1. Identify all exposed secrets and providers impacted.
2. Rotate each credential in the provider dashboard.
3. Revoke old credentials immediately after rollover validation.
4. Update application secrets in secure stores only:
   - production secret manager
   - CI/CD protected secrets
   - local `.NET user-secrets` for development
5. Never place replacement credentials in repository files.

## 2. Purge exposed values from git history

Coordinate with maintainers before history rewrite. This operation changes
commit SHAs and requires force-push.

Create a replacement file, for example `replacements.txt`:

```text
literal:sk_live_abc123==>YOUR_STRIPE_SECRET_KEY
regex:FLWSECK_(TEST|LIVE)-[A-Za-z0-9_-]+==>YOUR_FLUTTERWAVE_SECRET_KEY
```

Rewrite history with `git-filter-repo`:

```bash
pipx run git-filter-repo --replace-text replacements.txt
```

Then force-push safely:

```bash
git push --force-with-lease origin master
git push --force-with-lease --all
git push --force-with-lease --tags
```

## 3. Post-purge verification

1. Re-clone repository in a fresh directory.
2. Run secret scanning:

```bash
gitleaks dir . --redact --no-banner
```

3. Verify GitHub secret scanning and Dependabot alerts are clear.
4. Confirm applications can authenticate with rotated credentials.

## 4. Communication checklist

1. Open incident update with impacted systems and rotation completion status.
2. Ask all contributors to rebase or re-clone after history rewrite.
3. Close the incident only after verification steps pass.
