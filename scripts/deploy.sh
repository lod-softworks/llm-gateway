#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
deployment_target="${DEPLOYMENT_TARGET:-/home/site/wwwroot}"

oryx build \
  "$repository_root/src/Lod.LlmGateway.Gateway" \
  --output "$deployment_target"
