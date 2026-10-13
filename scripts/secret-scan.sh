#!/usr/bin/env bash
# Minimal secret scan over tracked files (and untracked, non-ignored ones). Fails on likely credentials.
# Known throwaway demo values are allow-listed below; everything else matching is a finding.
set -uo pipefail
cd "$(dirname "$0")/.."

ALLOW='wms_dev_password|Demo!Pass123|Test!Pass12345|integration-test-signing-key|unit-test-key|Sup3r-long-pass|a-long-bootstrap-pass|secret-demo|wrong-password|change-me|<your|<set via|<jwt>|\$\{|\$\(|process\.env|placeholder|example'

PATTERNS=(
  '-----BEGIN [A-Z ]*PRIVATE KEY-----'
  'AKIA[0-9A-Z]{16}'
  'gh[pousr]_[A-Za-z0-9]{30,}'
  'github_pat_[A-Za-z0-9_]{30,}'
  'xox[baprs]-[A-Za-z0-9-]{10,}'
  'sk-[A-Za-z0-9]{32,}'
  'eyJ[A-Za-z0-9_-]{15,}\.eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{10,}'
  '(password|passwd|pwd|secret|api[_-]?key|token)[A-Za-z0-9_"'\'' ]*[:=][ ]*["'\''][^"'\'' ]{8,}["'\'']'
  'Password=[^;"'\'' ]{4,}'
)

files=$(git ls-files -co --exclude-standard | grep -vE '(package-lock\.json|\.webp$|\.png$|\.jpg$|\.svg$|\.ico$|\.postman_collection\.json$|/Migrations/.*Designer\.cs$|scripts/secret-scan\.sh$)')
found=0
for p in "${PATTERNS[@]}"; do
  hits=$(echo "$files" | xargs grep -nIE -- "$p" 2>/dev/null | grep -vE "$ALLOW" || true)
  if [ -n "$hits" ]; then
    echo "Possible secret (pattern: $p):"; echo "$hits"; found=1
  fi
done
if [ "$found" -eq 1 ]; then echo "secret-scan: FAILED"; exit 1; fi
echo "secret-scan: clean ($(echo "$files" | wc -l | tr -d ' ') files checked)"
