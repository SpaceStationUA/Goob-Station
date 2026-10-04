#!/bin/bash
# SPDX-License-Identifier: AGPL-3.0-or-later
set -Eeuo pipefail
exec /usr/bin/python3 /home/alwyzon/ss14-hosting/run-server.py \
  --socket /home/alwyzon/ss14-hosting/run/pirate.sock \
  --log /home/alwyzon/ss14-hosting/logs/console.log \
  -- /usr/bin/ionice -c2 -n0 /usr/bin/dotnet bin/Robust.Server.dll "$@"
