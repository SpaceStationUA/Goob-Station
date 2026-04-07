#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Goob Station contributors
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Post changelog changes introduced by a merged PR to a Discord webhook.

Designed for GitHub Actions and supports 2 sources of truth, both based on *repo
files* (not PR text):
1) PR diff mode (default): compares the merge commit to its first parent and
   only inspects changelog files touched by the PR.
2) Changelog-commit diff mode (optional): if `CHANGELOG_BASE_SHA` and
   `CHANGELOG_HEAD_SHA` are provided, it compares those SHAs and inspects
   changelog files changed in that range (useful when a post-merge bot commit
   appends entries to `Resources/Changelog/*.yml`).
"""

from __future__ import annotations

import os
import time
from collections import Counter
from dataclasses import dataclass
from typing import Any, Optional
from urllib.parse import quote

import requests
import yaml

GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")

# https://discord.com/developers/docs/resources/webhook
DISCORD_SPLIT_LIMIT = 2000

DEFAULT_CHANGELOG_DIR = "Resources/Changelog"
DEFAULT_CHANGELOG_EXT = ".yml"
DEFAULT_DISCORD_MESSAGE_DELAY_SECONDS = 2.0

TYPES_TO_EMOJI = {"Fix": "🐛", "Add": "🆕", "Remove": "❌", "Tweak": "⚒️"}
TYPES_TO_UA = {
    "Fix": "Виправлено",
    "Add": "Додано",
    "Remove": "Видалено",
    "Tweak": "Змінено",
}

ChangelogEntry = dict[str, Any]


@dataclass(frozen=True)
class ChangelogFileDelta:
    path: str
    added: list[ChangelogEntry]
    modified: list[ChangelogEntry]
    removed: list[ChangelogEntry]


def main() -> None:
    discord_webhook_url = os.environ.get("DISCORD_WEBHOOK_URL")
    if not discord_webhook_url:
        print("No DISCORD_WEBHOOK_URL set, skipping Discord send")
        return

    github_token = os.environ.get("GITHUB_TOKEN")
    if not github_token:
        raise RuntimeError("GITHUB_TOKEN is required")

    github_repository = os.environ.get("GITHUB_REPOSITORY")
    if not github_repository:
        raise RuntimeError("GITHUB_REPOSITORY is required")

    pr_number_raw = os.environ.get("PR_NUMBER")
    if not pr_number_raw:
        raise RuntimeError("PR_NUMBER is required")

    pr_number = int(pr_number_raw)

    changelog_dir = os.environ.get("CHANGELOG_DIR", DEFAULT_CHANGELOG_DIR).rstrip("/")
    changelog_ext = os.environ.get("CHANGELOG_EXT", DEFAULT_CHANGELOG_EXT)

    changelog_base_sha = (os.environ.get("CHANGELOG_BASE_SHA") or "").strip() or None
    changelog_head_sha = (os.environ.get("CHANGELOG_HEAD_SHA") or "").strip() or None

    message_delay_seconds = float(
        os.environ.get(
            "DISCORD_MESSAGE_DELAY_SECONDS", str(DEFAULT_DISCORD_MESSAGE_DELAY_SECONDS)
        )
    )

    gh = github_session(github_token)

    pr = github_get_json(gh, f"{GITHUB_API_URL}/repos/{github_repository}/pulls/{pr_number}")
    if not pr.get("merged") and not pr.get("merged_at"):
        print("PR is not merged, skipping")
        return

    pr_title = pr.get("title") or ""
    pr_url = pr.get("html_url") or f"https://github.com/{github_repository}/pull/{pr_number}"

    merge_sha = pr.get("merge_commit_sha")

    merge_base_sha = None
    if merge_sha:
        try:
            merge_base_sha = get_first_parent_sha(gh, github_repository, merge_sha)
        except Exception as e:
            print(f"Failed to resolve merge base SHA from merge commit: {e}")
            merge_base_sha = None

        if not merge_base_sha:
            print("Could not resolve merge base SHA, skipping PR diff mode.")

    pr_deltas: list[ChangelogFileDelta] = []
    if merge_sha and merge_base_sha:
        changed_files = list_pr_files(gh, github_repository, pr_number)
        changelog_files = [
            f
            for f in changed_files
            if is_changelog_file(
                f, changelog_dir=changelog_dir, changelog_ext=changelog_ext
            )
        ]

        for path in changelog_files:
            delta = diff_changelog_file(
                gh,
                github_repository,
                path,
                base_sha=merge_base_sha,
                head_sha=merge_sha,
            )
            if delta:
                pr_deltas.append(delta)
    else:
        print("Missing merge_commit_sha or merge base SHA, skipping PR diff mode.")

    changelog_commit_deltas: list[ChangelogFileDelta] = []
    if (
        changelog_base_sha
        and changelog_head_sha
        and changelog_base_sha != changelog_head_sha
    ):
        try:
            compare_files = list_compare_files(
                gh,
                github_repository,
                base_sha=changelog_base_sha,
                head_sha=changelog_head_sha,
            )
        except Exception as e:
            print(f"Failed to list compare files for CHANGELOG_BASE_SHA/HEAD_SHA: {e}")
            compare_files = []
        changelog_files = [
            f
            for f in compare_files
            if is_changelog_file(
                f, changelog_dir=changelog_dir, changelog_ext=changelog_ext
            )
        ]

        for path in changelog_files:
            delta = diff_changelog_file(
                gh,
                github_repository,
                path,
                base_sha=changelog_base_sha,
                head_sha=changelog_head_sha,
            )
            if delta:
                changelog_commit_deltas.append(delta)
    else:
        print(
            "No CHANGELOG_BASE_SHA/CHANGELOG_HEAD_SHA provided, skipping changelog-commit diff mode."
        )

    deltas = merge_deltas_by_path(pr_deltas + changelog_commit_deltas)
    if not deltas:
        print("No changelog Entries delta detected, skipping")
        return

    lines = build_message_lines(pr_number=pr_number, pr_title=pr_title, pr_url=pr_url, deltas=deltas)
    send_message_lines(
        discord_webhook_url, lines, message_delay_seconds=message_delay_seconds
    )


def github_session(token: str) -> requests.Session:
    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"
    return session


def github_get_json(session: requests.Session, url: str, *, params: Optional[dict[str, Any]] = None) -> Any:
    resp = session.get(url, params=params, timeout=30)
    resp.raise_for_status()
    return resp.json()


def get_first_parent_sha(
    session: requests.Session, github_repository: str, sha: str
) -> Optional[str]:
    """Return the first parent SHA of a commit, or None if unavailable."""

    data = github_get_json(
        session, f"{GITHUB_API_URL}/repos/{github_repository}/commits/{sha}"
    )
    parents = data.get("parents")
    if not isinstance(parents, list) or not parents:
        return None

    first = parents[0]
    if not isinstance(first, dict):
        return None

    parent_sha = first.get("sha")
    return str(parent_sha) if parent_sha else None


def list_compare_files(
    session: requests.Session,
    github_repository: str,
    *,
    base_sha: str,
    head_sha: str,
) -> list[str]:
    """List file paths changed between base..head using the Compare API.

    Note: GitHub may truncate file lists for very large comparisons.
    """

    data = github_get_json(
        session,
        f"{GITHUB_API_URL}/repos/{github_repository}/compare/{base_sha}...{head_sha}",
    )
    files = data.get("files")
    if not isinstance(files, list):
        return []

    out: list[str] = []
    for item in files:
        if not isinstance(item, dict):
            continue

        filename = item.get("filename")
        if filename:
            out.append(str(filename))

    return out


def list_pr_files(session: requests.Session, github_repository: str, pr_number: int) -> list[str]:
    files: list[str] = []
    page = 1

    while True:
        resp = session.get(
            f"{GITHUB_API_URL}/repos/{github_repository}/pulls/{pr_number}/files",
            params={"per_page": 100, "page": page},
            timeout=30,
        )
        resp.raise_for_status()
        batch = resp.json()
        if not batch:
            break

        for item in batch:
            filename = item.get("filename")
            if filename:
                files.append(str(filename))

        page += 1

    return files


def is_changelog_file(path: str, *, changelog_dir: str, changelog_ext: str) -> bool:
    prefix = f"{changelog_dir}/"
    return path.startswith(prefix) and path.endswith(changelog_ext)


def get_repo_file_text(session: requests.Session, github_repository: str, path: str, sha: str) -> Optional[str]:
    """Fetch a repo file as raw text at a given ref.

    Returns None if the file does not exist at that ref.
    """

    encoded_path = quote(path, safe="/")
    headers = {"Accept": "application/vnd.github.raw"}

    resp = session.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/contents/{encoded_path}",
        headers=headers,
        params={"ref": sha},
        timeout=30,
    )

    if resp.status_code == 404:
        return None

    resp.raise_for_status()
    return resp.text


def parse_yaml(text: Optional[str]) -> Any:
    if text is None:
        return {}

    # Some YAML files start with a UTF-8 BOM.
    text = text.lstrip("\ufeff")

    data = yaml.safe_load(text)
    return data if data is not None else {}


def extract_entries(data: Any) -> list[ChangelogEntry]:
    if isinstance(data, dict):
        entries = data.get("Entries")
        if isinstance(entries, list):
            return [e for e in entries if isinstance(e, dict)]
        return []

    return []


def diff_entries(
    old_entries: list[ChangelogEntry],
    new_entries: list[ChangelogEntry],
) -> tuple[list[ChangelogEntry], list[ChangelogEntry], list[ChangelogEntry]]:
    """Compute (added, modified, removed) entries by `id`.

    Notes:
    - Some changelog files contain duplicate `id` values. We treat `id` as a primary key
      only when it is unique in BOTH the old and new file; otherwise we skip "modified"
      detection for that `id` to avoid false positives.

    Ordering:
    - added/modified follow the order in `new_entries`
    - removed follows the order in `old_entries`
    """

    old_ids = [e.get("id") for e in old_entries if e.get("id") is not None]
    new_ids = [e.get("id") for e in new_entries if e.get("id") is not None]

    old_id_set = set(old_ids)
    new_id_set = set(new_ids)

    # Added: any new entry whose id did not exist in the base version.
    added: list[ChangelogEntry] = []
    for entry in new_entries:
        entry_id = entry.get("id")
        if entry_id is None:
            continue
        if entry_id not in old_id_set:
            added.append(entry)

    # Modified: only for IDs that are unique in BOTH old and new.
    old_counts = Counter(old_ids)
    new_counts = Counter(new_ids)

    old_unique_by_id: dict[Any, ChangelogEntry] = {}
    for entry in old_entries:
        entry_id = entry.get("id")
        if entry_id is None:
            continue
        if old_counts.get(entry_id, 0) == 1:
            old_unique_by_id[entry_id] = entry

    modified: list[ChangelogEntry] = []
    for entry in new_entries:
        entry_id = entry.get("id")
        if entry_id is None:
            continue
        if old_counts.get(entry_id, 0) != 1 or new_counts.get(entry_id, 0) != 1:
            continue

        old_entry = old_unique_by_id.get(entry_id)
        if old_entry is None:
            continue

        if old_entry != entry:
            modified.append(entry)

    # Removed: any id that existed before but not after.
    removed: list[ChangelogEntry] = []
    for entry in old_entries:
        entry_id = entry.get("id")
        if entry_id is None:
            continue

        if entry_id not in new_id_set:
            removed.append(entry)

    return added, modified, removed


def diff_changelog_file(
    session: requests.Session,
    github_repository: str,
    path: str,
    *,
    base_sha: str,
    head_sha: str,
) -> Optional[ChangelogFileDelta]:
    old_text = get_repo_file_text(session, github_repository, path, base_sha)
    new_text = get_repo_file_text(session, github_repository, path, head_sha)

    old_entries = extract_entries(parse_yaml(old_text))
    new_entries = extract_entries(parse_yaml(new_text))

    added, modified, removed = diff_entries(old_entries, new_entries)
    if not (added or modified or removed):
        return None

    return ChangelogFileDelta(path=path, added=added, modified=modified, removed=removed)


def merge_deltas_by_path(deltas: list[ChangelogFileDelta]) -> list[ChangelogFileDelta]:
    merged: dict[str, ChangelogFileDelta] = {}
    for delta in deltas:
        existing = merged.get(delta.path)
        if existing is None:
            merged[delta.path] = delta
            continue

        merged[delta.path] = ChangelogFileDelta(
            path=delta.path,
            added=existing.added + delta.added,
            modified=existing.modified + delta.modified,
            removed=existing.removed + delta.removed,
        )

    return list(merged.values())


def render_change_type_summary(entries: list[ChangelogEntry]) -> str:
    counts = count_change_types(entries)

    parts = []
    for change_type in ("Add", "Fix", "Remove", "Tweak"):
        count = counts.get(change_type, 0)
        if not count:
            continue

        emoji = TYPES_TO_EMOJI.get(change_type, "❓")
        parts.append(f"{emoji} {count}")

    return " • ".join(parts)


def count_change_types(entries: list[ChangelogEntry]) -> dict[str, int]:
    counts: dict[str, int] = {"Add": 0, "Fix": 0, "Remove": 0, "Tweak": 0}

    for entry in entries:
        changes = entry.get("changes")
        if not isinstance(changes, list):
            continue

        for change in changes:
            if not isinstance(change, dict):
                continue

            change_type = str(change.get("type") or "")
            if not change_type:
                continue

            counts[change_type] = counts.get(change_type, 0) + 1

    return counts


def build_message_lines(*, pr_number: int, pr_title: str, pr_url: str, deltas: list[ChangelogFileDelta]) -> list[str]:
    lines: list[str] = []

    title_line = f"🆑 **Оновлення списку змін — PR [#{pr_number}]({pr_url}) змерджено**\n"
    lines.append(title_line)

    if pr_title:
        # Keep as plain text; allowed_mentions prevents pings.
        lines.append(f"{pr_title}\n")

    lines.append("\n")

    for delta in deltas:
        entries_to_report = delta.added + delta.modified

        file_name = delta.path.rsplit("/", 1)[-1]
        lines.append(f"📄 **{file_name}** (`{delta.path}`)\n")

        # Short per-file summary by change type (Add/Fix/Remove/Tweak)
        summary = render_change_type_summary(entries_to_report)
        if summary:
            lines.append(f"Підсумок: {summary}\n")

        lines.extend(changelog_entries_to_message_lines(entries_to_report))

        if delta.removed:
            lines.append(
                f"⚠️ Зі списку змін прибрано {len(delta.removed)} запис(ів) у цьому файлі.\n"
            )

        lines.append("\n")

    return lines


def changelog_entries_to_message_lines(entries: list[ChangelogEntry]) -> list[str]:
    """Format changelog entries into message lines.

    Each entry is rendered as a separate block with its author.
    """

    message_lines: list[str] = []

    for i, entry in enumerate(entries):
        author = str(entry.get("author") or "Невідомо")
        changes = entry.get("changes")
        if not isinstance(changes, list):
            continue

        # Collect valid change lines for this entry.
        change_lines: list[str] = []
        for change in changes:
            if not isinstance(change, dict):
                continue

            change_type = str(change.get("type") or "")
            emoji = TYPES_TO_EMOJI.get(change_type, "❓")
            label = TYPES_TO_UA.get(change_type, change_type or "Невідомо")

            message = str(change.get("message") or "").strip()
            if not message:
                continue

            # If a single line is longer than the limit, it needs to be truncated.
            if len(message) > DISCORD_SPLIT_LIMIT:
                message = message[: DISCORD_SPLIT_LIMIT - 100].rstrip() + " [...]"

            change_lines.append(f"• {emoji} {label}: {message}\n")

        if not change_lines:
            continue

        if i > 0:
            message_lines.append("\n")

        message_lines.append(f"**{author}**:\n")
        message_lines.extend(change_lines)

    return message_lines


def get_discord_body(content: str) -> dict[str, Any]:
    return {
        "content": content,
        # Do not allow any mentions.
        "allowed_mentions": {"parse": []},
        # SUPPRESS_EMBEDS
        "flags": 1 << 2,
    }


def send_discord_webhook(discord_webhook_url: str, lines: list[str]) -> None:
    content = "".join(lines)
    body = get_discord_body(content)

    attempt = 0
    while True:
        attempt += 1
        resp = requests.post(discord_webhook_url, json=body, timeout=30)

        # Discord webhooks typically return 204 No Content.
        if resp.status_code in (200, 204):
            return

        # Rate limited.
        if resp.status_code == 429:
            retry_after = None
            try:
                data = resp.json()
                retry_after = data.get("retry_after")
            except Exception:
                retry_after = None

            if retry_after is None:
                retry_after = float(resp.headers.get("Retry-After", "1"))

            retry_after = float(retry_after)
            print(f"Discord rate limited (429), retrying after {retry_after} seconds")
            time.sleep(max(retry_after, 0.0))
            continue

        # Transient server-side errors: retry a few times.
        if resp.status_code in (500, 502, 503, 504) and attempt < 6:
            backoff = min(2 ** attempt, 30)
            print(f"Discord error {resp.status_code}, retrying after {backoff} seconds")
            time.sleep(backoff)
            continue

        resp.raise_for_status()


def send_message_lines(
    discord_webhook_url: str,
    message_lines: list[str],
    *,
    message_delay_seconds: float,
) -> None:
    """Chunk a list of lines into <=2000 char messages and send them."""

    chunk_lines: list[str] = []
    chunk_length = 0

    for line in message_lines:
        # If a single line is longer than the limit, it needs to be truncated.
        if len(line) > DISCORD_SPLIT_LIMIT:
            line = line[: DISCORD_SPLIT_LIMIT - 100].rstrip() + " [...]"

        line_length = len(line)
        new_chunk_length = chunk_length + line_length

        if new_chunk_length > DISCORD_SPLIT_LIMIT:
            if chunk_lines:
                print("Split changelog and sending to Discord")
                send_discord_webhook(discord_webhook_url, chunk_lines)
                time.sleep(max(message_delay_seconds, 0.0))

            chunk_lines = [line]
            chunk_length = line_length
            continue

        chunk_lines.append(line)
        chunk_length = new_chunk_length

    if chunk_lines:
        print("Sending final changelog to Discord")
        send_discord_webhook(discord_webhook_url, chunk_lines)


if __name__ == "__main__":
    main()
