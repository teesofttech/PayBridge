#!/usr/bin/env python3
"""Plan and validate PayBridge sandbox integration-test runs."""

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Mapping, NamedTuple, Sequence


class Provider(NamedTuple):
    name: str
    required_secrets: tuple[str, ...]
    test_filter: str


class ProviderSummary(NamedTuple):
    provider: str
    passed: int
    failed: int
    skipped: int
    total: int


PROVIDERS = {
    "peachpayments": Provider(
        name="peachpayments",
        required_secrets=("PEACH_ENTITY_ID", "PEACH_ACCESS_TOKEN"),
        test_filter="FullyQualifiedName~PayBridge.SDK.Test.Integration.PeachPayments",
    ),
}


def create_plan(gateway: str, environment: Mapping[str, str]) -> list[Provider]:
    selected = gateway.strip().lower()
    if selected != "all" and selected not in PROVIDERS:
        supported = ", ".join(["all", *PROVIDERS])
        raise ValueError(f"Unsupported gateway '{gateway}'. Supported values: {supported}")

    candidates = list(PROVIDERS.values()) if selected == "all" else [PROVIDERS[selected]]
    configured = []
    missing_by_provider = {}
    for provider in candidates:
        missing = [
            secret
            for secret in provider.required_secrets
            if not environment.get(secret, "").strip()
        ]
        if missing:
            missing_by_provider[provider.name] = missing
        else:
            configured.append(provider)

    if selected != "all" and missing_by_provider:
        missing = ", ".join(missing_by_provider[selected])
        raise ValueError(f"Missing required secrets for {selected}: {missing}")

    if not configured:
        details = "; ".join(
            f"{name}: {', '.join(missing)}"
            for name, missing in missing_by_provider.items()
        )
        raise ValueError(f"No sandbox provider is configured. {details}")

    return configured


def read_trx(path: Path, provider: str) -> ProviderSummary:
    if not path.is_file():
        raise ValueError(f"TRX result file does not exist: {path}")

    root = ET.parse(path).getroot()
    counters = root.find(".//{*}Counters")
    if counters is None:
        raise ValueError(f"TRX result file has no Counters element: {path}")

    passed = int(counters.get("passed", "0"))
    failed = int(counters.get("failed", "0"))
    result_outcomes = [
        result.get("outcome", "")
        for result in root.findall(".//{*}UnitTestResult")
    ]
    skipped = max(
        int(counters.get("notExecuted", "0")),
        sum(outcome == "NotExecuted" for outcome in result_outcomes),
    )
    total = int(counters.get("total", str(passed + failed + skipped)))
    return ProviderSummary(provider, passed, failed, skipped, total)


def validate_summaries(summaries: Sequence[ProviderSummary]) -> None:
    if any(summary.failed > 0 for summary in summaries):
        raise ValueError("Integration run contains failed sandbox tests")
    if sum(summary.passed for summary in summaries) == 0:
        raise ValueError("Integration run completed with zero passing sandbox tests")


def markdown_summary(summaries: Sequence[ProviderSummary]) -> str:
    lines = [
        "## Sandbox integration results",
        "",
        "| Provider | Passed | Failed | Skipped | Total |",
        "|---|---:|---:|---:|---:|",
    ]
    lines.extend(
        f"| {item.provider} | {item.passed} | {item.failed} | "
        f"{item.skipped} | {item.total} |"
        for item in summaries
    )
    return "\n".join(lines) + "\n"


def write_github_output(plan: Sequence[Provider], path: Path) -> None:
    if len(plan) != 1:
        raise ValueError("The workflow currently supports one configured provider per job")
    with path.open("a", encoding="utf-8") as output:
        output.write(f"provider={plan[0].name}\n")
        output.write(f"test_filter={plan[0].test_filter}\n")


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    plan_parser = subparsers.add_parser("plan")
    plan_parser.add_argument("--gateway", required=True)
    plan_parser.add_argument("--github-output", type=Path)

    summary_parser = subparsers.add_parser("summarize")
    summary_parser.add_argument("--provider", required=True)
    summary_parser.add_argument("--trx", required=True, type=Path)
    summary_parser.add_argument("--github-summary", type=Path)

    args = parser.parse_args()
    try:
        if args.command == "plan":
            plan = create_plan(args.gateway, os.environ)
            if args.github_output:
                write_github_output(plan, args.github_output)
            else:
                print(plan[0].test_filter)
        else:
            summaries = [read_trx(args.trx, args.provider)]
            report = markdown_summary(summaries)
            if args.github_summary:
                with args.github_summary.open("a", encoding="utf-8") as output:
                    output.write(report)
            else:
                print(report, end="")
            validate_summaries(summaries)
    except (OSError, ET.ParseError, ValueError) as error:
        print(f"integration-test-gate: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
