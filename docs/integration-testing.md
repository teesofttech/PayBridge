# Sandbox integration testing

PayBridge's scheduled integration workflow must prove that at least one real
sandbox test passed. Missing credentials, a missing result file, failed tests,
or a result containing only skipped tests fail the job.

## Currently implemented provider suite

| Provider input | Test class | Required `integration` environment secrets |
|---|---|---|
| `peachpayments` | `PeachPaymentsIntegrationTests` | `PEACH_ENTITY_ID`, `PEACH_ACCESS_TOKEN` |

Add both secrets under **Settings → Environments → integration → Environment
secrets**. They remain scoped to jobs that reference the `integration`
environment and are not passed to build, reporting, or artifact-upload steps.

Manual runs expose only `all` and providers with implemented sandbox tests.
Selecting one provider applies an exact fully-qualified-name test filter. The
scheduled `all` run executes configured provider suites and fails during
preflight when none are configured.

Each run uploads the TRX file and writes a provider summary containing passed,
failed, skipped, and total counts. The tested result gate, rather than the test
reporting action, owns the final pass/fail decision.

## Local gate tests

```bash
python3 -m unittest scripts/tests/test_integration_test_gate.py
```

Run the preflight locally without printing credential values:

```bash
PEACH_ENTITY_ID="<sandbox-entity>" \
PEACH_ACCESS_TOKEN="<sandbox-token>" \
python3 scripts/integration_test_gate.py plan --gateway peachpayments
```
