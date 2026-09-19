#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Pirate Development Team
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Vendor the third-party Pirate WebArcade games into
# Resources/_Pirate/WebUI/Arcade/. We redistribute these works; this script
# fetches the exact pinned bytes, the upstream license text, and records
# provenance. It never builds anything (see Scripts/arcade-games.manifest).
#
# Usage:
#   Scripts/sh/vendor-arcade-games.sh [id ...]
#
# With no ids, every entry in the manifest is (re)fetched. Existing dest
# folders are replaced.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
manifest="${repo_root}/Scripts/arcade-games.manifest"
arcade_dir="${repo_root}/Resources/_Pirate/WebUI/Arcade"
work="$(mktemp -d)"
trap 'rm -rf "${work}"' EXIT

die() { echo "error: $*" >&2; exit 1; }

[[ -f "${manifest}" ]] || die "manifest not found: ${manifest}"

wanted=("$@")
selected() {
    [[ ${#wanted[@]} -eq 0 ]] && return 0
    local id="$1" w
    for w in "${wanted[@]}"; do [[ "${w}" == "${id}" ]] && return 0; done
    return 1
}

# Fetch the repo's license file at the pinned ref into <dest>/LICENSE.txt.
# Best-effort: js13k entries sometimes carry a license not present at the
# repo root, so a miss is not fatal (PROVENANCE still names the license).
fetch_license() {
    local repo="$1" ref="$2" dest="$3" f
    for f in LICENSE LICENSE.txt LICENSE.md license license.md COPYING; do
        if curl -fsSL "https://raw.githubusercontent.com/${repo}/${ref}/${f}" \
                -o "${dest}/LICENSE.txt" 2>/dev/null; then
            [[ -s "${dest}/LICENSE.txt" ]] && return 0
        fi
    done
    rm -f "${dest}/LICENSE.txt"
    echo "   note: no license file found at ${repo}@${ref}"
    return 0
}

fetched=0
while IFS=$'\t' read -r id license kind repo ref subpath bundle dest; do
    [[ -z "${id}" || "${id}" == \#* ]] && continue
    selected "${id}" || continue
    [[ -n "${dest}" ]] || die "manifest row '${id}' missing dest"

    echo "== ${id} (${license}) -> ${dest}"
    dest_dir="${arcade_dir}/${dest}"
    rm -rf "${dest_dir}"
    mkdir -p "${dest_dir}"

    case "${kind}" in
        repo)
            url="https://github.com/${repo}/archive/${ref}.tar.gz"
            echo "   fetch ${url}"
            curl -fsSL "${url}" -o "${work}/src.tgz" || die "download failed: ${url}"
            rm -rf "${work}/extract"
            mkdir -p "${work}/extract"
            tar xzf "${work}/src.tgz" -C "${work}/extract"
            top="$(find "${work}/extract" -mindepth 1 -maxdepth 1 -type d | head -1)"
            [[ -n "${top}" ]] || die "archive had no top-level directory"
            srcdir="${top}/${subpath}"
            [[ -d "${srcdir}" ]] || die "subpath '${subpath}' not found in archive"
            cp -R "${srcdir}/." "${dest_dir}/"
            # Upstream dist often ships its own competition zip; we serve
            # index.html directly, so drop it to keep the tree lean.
            rm -f "${dest_dir}"/*.zip
            ;;
        zip)
            echo "   fetch ${bundle}"
            curl -fsSL "${bundle}" -o "${work}/src.zip" || die "download failed: ${bundle}"
            unzip -qo "${work}/src.zip" -d "${dest_dir}" || die "unzip failed: ${bundle}"
            # Strip a single wrapping directory if the zip used one.
            entries="$(find "${dest_dir}" -mindepth 1 -maxdepth 1 | wc -l | tr -d ' ')"
            only="$(find "${dest_dir}" -mindepth 1 -maxdepth 1 -type d)"
            if [[ "${entries}" == "1" && -n "${only}" && -z "$(find "${only}" -maxdepth 1 -type f)" ]]; then
                mv "${only}"/* "${dest_dir}/" && rmdir "${only}"
            fi
            ;;
        *)
            die "unknown kind '${kind}' for '${id}'"
            ;;
    esac

    [[ -f "${dest_dir}/index.html" ]] || die "${dest} has no index.html after fetch"

    # Per-game sanitization: strip third-party network scripts that make no
    # sense (and can't work) inside the offline, in-game CEF page. Recorded in
    # PROVENANCE so the exception is explicit.
    sanitize_note=""
    case "${id}" in
        stunts)
            # Plausible analytics beacon to p.jlopes.dev.
            if grep -q 'p\.jlopes\.dev' "${dest_dir}/index.html"; then
                sed -i.bak '/p\.jlopes\.dev/d' "${dest_dir}/index.html"
                rm -f "${dest_dir}/index.html.bak"
                sanitize_note="removed third-party analytics script (p.jlopes.dev)"
            fi
            ;;
    esac

    fetch_license "${repo}" "${ref}" "${dest_dir}"

    kind_line="kind:    ${kind}"
    if [[ "${kind}" == "repo" ]]; then
        kind_line="${kind_line} (subpath: ${subpath})"
    else
        kind_line="${kind_line} (${bundle})"
    fi
    exc_line=""
    if [[ -n "${sanitize_note}" ]]; then
        exc_line="Exception: ${sanitize_note}."
    fi

    cat > "${dest_dir}/PROVENANCE.txt" <<EOF
${dest} — vendored third-party WebArcade game
Fetched by Scripts/sh/vendor-arcade-games.sh.

id:      ${id}
license: ${license}
repo:    ${repo}
ref:     ${ref}
${kind_line}
fetched: $(date -u +%Y-%m-%dT%H:%M:%SZ)

This is a third-party work from the JS13kGames 2024 (or 2023) competition.
See the repository/source above for the original license text and copyright
holder. We do not claim authorship and we do not modify the code.${exc_line:+
${exc_line}}
EOF
    fetched=$((fetched + 1))
done < "${manifest}"

if [[ ${#wanted[@]} -gt 0 && ${fetched} -eq 0 ]]; then
    die "no manifest rows matched: ${wanted[*]}"
fi

echo "done: ${fetched} game(s) vendored into ${arcade_dir#${repo_root}/}/"
