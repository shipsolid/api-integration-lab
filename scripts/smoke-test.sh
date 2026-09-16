#!/usr/bin/env bash
set -euo pipefail

# This script prints only check names and status codes. Keep shell tracing disabled: command lines
# for HMAC and provider checks can contain secrets even when HTTP response bodies are harmless.
API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
GRAFANA_BASE_URL="${GRAFANA_BASE_URL:-http://localhost:3000}"
smoke_tmp_dir="$(mktemp -d)"
trap 'rm -rf "$smoke_tmp_dir"' EXIT

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "FAIL missing required command: $1" >&2
    exit 1
  fi
}

get_status() {
  local output_file="$1"
  local url="$2"
  curl --silent --show-error --output "$output_file" --write-out '%{http_code}' "$url"
}

expect_get() {
  local name="$1"
  local url="$2"
  local expected="$3"
  local status
  status="$(get_status "$smoke_tmp_dir/response.json" "$url")"
  if [[ "$status" != "$expected" ]]; then
    echo "FAIL $name (HTTP $status, expected $expected)" >&2
    return 1
  fi
  echo "PASS $name"
}

require_command curl
require_command openssl

expect_get "health" "$API_BASE_URL/health" 200
expect_get "swagger" "$API_BASE_URL/swagger/v1/swagger.json" 200
expect_get "public API" "$API_BASE_URL/api/public/posts?limit=1" 200

if [[ -n "${BASIC_AUTH_USERNAME:-}" && -n "${BASIC_AUTH_PASSWORD:-}" ]]; then
  expect_get "Basic Auth" "$API_BASE_URL/api/basic/profile" 200
else
  echo "SKIP Basic Auth: export BASIC_AUTH_USERNAME and BASIC_AUTH_PASSWORD"
fi

# The API owns the GitHub token. Once a token is supplied, accepting 401 as "tokenless" would hide
# an expired or misconfigured PAT, so the expected status follows the caller's explicit setup.
github_status="$(get_status "$smoke_tmp_dir/github.json" "$API_BASE_URL/api/github/profile")"
if [[ -n "${GITHUB_TOKEN:-}" ]]; then
  [[ "$github_status" == "200" ]] || {
    echo "FAIL GitHub profile (HTTP $github_status, configured token expected 200)" >&2
    exit 1
  }
  echo "PASS GitHub profile"
else
  [[ "$github_status" == "401" ]] || {
    echo "FAIL GitHub profile (HTTP $github_status, tokenless request expected 401)" >&2
    exit 1
  }
  echo "PASS GitHub profile (expected tokenless 401)"
fi

if [[ -n "${WEBHOOK_SECRET:-}" ]]; then
  webhook_body='{"eventType":"smoke.created","value":1}'
  webhook_timestamp="$(date +%s)"
  # The signature covers the exact timestamp, a literal dot, and the exact transmitted JSON bytes.
  webhook_digest="$(printf '%s.%s' "$webhook_timestamp" "$webhook_body" \
    | openssl dgst -sha256 -hmac "$WEBHOOK_SECRET" \
    | awk '{print $NF}')"
  webhook_status="$(curl --silent --show-error \
    --output "$smoke_tmp_dir/webhook-valid.json" \
    --write-out '%{http_code}' \
    --request POST \
    --header 'Content-Type: application/json' \
    --header "X-Webhook-Timestamp: $webhook_timestamp" \
    --header "X-Webhook-Signature-256: sha256=$webhook_digest" \
    --data-binary "$webhook_body" \
    "$API_BASE_URL/api/webhooks/events")"
  [[ "$webhook_status" == "202" ]] || {
    echo "FAIL HMAC valid signature (HTTP $webhook_status, expected 202)" >&2
    exit 1
  }
  echo "PASS HMAC valid signature"

  replay_status="$(curl --silent --show-error \
    --output "$smoke_tmp_dir/webhook-replay.json" \
    --write-out '%{http_code}' \
    --request POST \
    --header 'Content-Type: application/json' \
    --header "X-Webhook-Timestamp: $webhook_timestamp" \
    --header "X-Webhook-Signature-256: sha256=$webhook_digest" \
    --data-binary "$webhook_body" \
    "$API_BASE_URL/api/webhooks/events")"
  [[ "$replay_status" == "401" ]] || {
    echo "FAIL HMAC replay (HTTP $replay_status, expected 401)" >&2
    exit 1
  }
  echo "PASS HMAC identical delivery replay rejected"

  invalid_status="$(curl --silent --show-error \
    --output "$smoke_tmp_dir/webhook-invalid.json" \
    --write-out '%{http_code}' \
    --request POST \
    --header 'Content-Type: application/json' \
    --header "X-Webhook-Timestamp: $webhook_timestamp" \
    --header "X-Webhook-Signature-256: sha256=$(printf '0%.0s' {1..64})" \
    --data-binary "$webhook_body" \
    "$API_BASE_URL/api/webhooks/events")"
  [[ "$invalid_status" == "401" ]] || {
    echo "FAIL HMAC invalid signature (HTTP $invalid_status, expected 401)" >&2
    exit 1
  }
  echo "PASS HMAC invalid signature rejected"
else
  echo "SKIP HMAC checks: export WEBHOOK_SECRET used by the running API"
fi

if [[ -n "${MS_TENANT_ID:-}" && -n "${MS_CLIENT_ID:-}" && -n "${MS_CLIENT_SECRET:-}" ]]; then
  microsoft_status="$(curl --silent --show-error \
    --output "$smoke_tmp_dir/microsoft-login.json" \
    --write-out '%{http_code}' \
    --max-redirs 0 \
    "$API_BASE_URL/api/microsoft/login")"
  [[ "$microsoft_status" == "302" ]] || {
    echo "FAIL Microsoft login redirect (HTTP $microsoft_status, expected 302)" >&2
    exit 1
  }
  echo "INFO Microsoft login redirect is ready; finish consent in a browser"
else
  echo "SKIP Microsoft login: export MS_TENANT_ID, MS_CLIENT_ID, and MS_CLIENT_SECRET"
fi

expect_get "Grafana health" "$GRAFANA_BASE_URL/api/health" 200
