import importlib.util
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).parents[1] / "integration_test_gate.py"
WORKFLOW_PATH = Path(__file__).parents[2] / ".github/workflows/integration-tests.yml"
CI_WORKFLOW_PATH = Path(__file__).parents[2] / ".github/workflows/ci-tests.yml"
SPEC = importlib.util.spec_from_file_location("integration_test_gate", MODULE_PATH)
gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gate)


class IntegrationTestGateTests(unittest.TestCase):
    def test_workflow_uses_preflight_filter_and_result_gate(self):
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

        self.assertIn("integration_test_gate.py plan", workflow)
        self.assertIn("steps.preflight.outputs.test_filter", workflow)
        self.assertIn("integration_test_gate.py summarize", workflow)

    def test_manual_gateway_input_only_offers_implemented_provider_tests(self):
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

        self.assertIn("type: choice", workflow)
        self.assertIn("- peachpayments", workflow)
        self.assertNotIn("e.g. paystack, flutterwave", workflow)

    def test_pull_request_ci_runs_gate_unit_tests(self):
        workflow = CI_WORKFLOW_PATH.read_text(encoding="utf-8")

        self.assertIn("python3 -m unittest scripts/tests/test_integration_test_gate.py", workflow)

    def test_plan_rejects_unknown_provider(self):
        with self.assertRaisesRegex(ValueError, "Unsupported gateway"):
            gate.create_plan("paystack", {})

    def test_plan_rejects_selected_provider_with_missing_secrets(self):
        with self.assertRaisesRegex(ValueError, "PEACH_ACCESS_TOKEN"):
            gate.create_plan("peachpayments", {"PEACH_ENTITY_ID": "entity"})

    def test_all_selects_only_configured_provider_tests(self):
        plan = gate.create_plan(
            "all",
            {
                "PEACH_ENTITY_ID": "entity",
                "PEACH_ACCESS_TOKEN": "token",
            },
        )

        self.assertEqual(["peachpayments"], [provider.name for provider in plan])
        self.assertEqual(
            "FullyQualifiedName~PayBridge.SDK.Test.Integration.PeachPayments",
            plan[0].test_filter,
        )

    def test_all_rejects_run_when_no_provider_is_configured(self):
        with self.assertRaisesRegex(ValueError, "No sandbox provider"):
            gate.create_plan("all", {})

    def test_summary_reports_provider_counts(self):
        result_file = self._write_trx(passed=2, failed=0, skipped=1)

        summary = gate.read_trx(result_file, "peachpayments")

        self.assertEqual(2, summary.passed)
        self.assertEqual(0, summary.failed)
        self.assertEqual(1, summary.skipped)
        self.assertEqual(3, summary.total)

    def test_summary_rejects_zero_passed_tests(self):
        result_file = self._write_trx(passed=0, failed=0, skipped=2)

        summary = gate.read_trx(result_file, "peachpayments")

        with self.assertRaisesRegex(ValueError, "zero passing sandbox tests"):
            gate.validate_summaries([summary])

    def test_summary_rejects_failed_tests(self):
        result_file = self._write_trx(passed=1, failed=1, skipped=0)

        summary = gate.read_trx(result_file, "peachpayments")

        with self.assertRaisesRegex(ValueError, "failed sandbox tests"):
            gate.validate_summaries([summary])

    def test_summary_counts_xunit_skips_from_individual_results(self):
        result_file = self._write_trx(passed=0, failed=0, skipped=2, counter_skipped=0)

        summary = gate.read_trx(result_file, "peachpayments")

        self.assertEqual(2, summary.skipped)

    def _write_trx(self, passed, failed, skipped, counter_skipped=None):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        path = Path(directory.name) / "results.trx"
        results = "\n".join(
            ['    <UnitTestResult outcome="Passed" />'] * passed
            + ['    <UnitTestResult outcome="Failed" />'] * failed
            + ['    <UnitTestResult outcome="NotExecuted" />'] * skipped
        )
        reported_skipped = skipped if counter_skipped is None else counter_skipped
        path.write_text(
            f'''<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
{results}
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="{passed + failed + skipped}" executed="{passed + failed}"
      passed="{passed}" failed="{failed}" error="0" timeout="0"
      aborted="0" inconclusive="0" passedButRunAborted="0"
      notRunnable="0" notExecuted="{reported_skipped}" disconnected="0"
      warning="0" completed="0" inProgress="0" pending="0" />
  </ResultSummary>
</TestRun>''',
            encoding="utf-8",
        )
        return path


if __name__ == "__main__":
    unittest.main()
