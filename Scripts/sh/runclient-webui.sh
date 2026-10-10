# SPDX-FileCopyrightText: 2026 Pirate Development Team
# SPDX-License-Identifier: AGPL-3.0-or-later

#!/usr/bin/env bash
# Pirate: WebUI temporarily uses native placeholders; launch without a CEF module.

set -euo pipefail

cd "$(dirname "$0")/../../"

exec dotnet run --project Content.Pirate.Client -c Tools -p:BuildWebUI=false -- "$@"
