#!/usr/bin/env bash

set -euo pipefail

solution_path="${1:-PayBridge.SDK.sln}"

set +e
audit_output="$(
    dotnet list "$solution_path" package --vulnerable --include-transitive 2>&1
)"
audit_exit_code=$?
set -e

printf '%s\n' "$audit_output"

if ((audit_exit_code != 0)); then
    printf '\nNuGet vulnerability auditing could not complete.\n' >&2
    exit "$audit_exit_code"
fi

if grep -Eq '[[:space:]](High|Critical)[[:space:]]' <<<"$audit_output"; then
    printf '\nHigh or critical NuGet vulnerabilities were found.\n' >&2
    exit 1
fi

printf '\nNo high or critical NuGet vulnerabilities were found.\n'
