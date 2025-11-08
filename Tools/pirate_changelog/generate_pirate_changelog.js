// SPDX-FileCopyrightText: 2025 CrateTheDragon <drago.felcon@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

const fs = require("fs");
const yaml = require("js-yaml");
const { execSync } = require("child_process");

const CHANGELOG_PATH = "Resources/Changelog/pirate.yml";

const HeaderRegex = /^\s*(?::cl:|🆑) *([a-z0-9_\- ,]+)?\s+/im;
const EntryRegex = /^ *[*-]? *(add|remove|tweak|fix): *([^\n\r]+)\r?$/img;
const CommentRegex = /<!--.*?-->/gs;

function main() {
    const commitMessage = execSync("git log -1 --pretty=%B").toString();
    let body = commitMessage.replace(CommentRegex, "");
    let authorMath = HeaderRegex.exec(body);
    let author = authorMath?.[1] || execSync("git log -1 --pretty=%an").toString().trim();
    const changes = getChanges(body);

    if (!changes || changes.length === 0) {
        console.log("Changes not found.");
        return;
    }

    let data = { Name: "pirate", Order: 0, AdminOnly: false, Entries: [] };

    if (fs.existsSync(CHANGELOG_PATH)) {
        data = yaml.load(fs.readFileSync(CHANGELOG_PATH, "utf-8"));
    }

    data.Entries = data.Entries || [];
    const newId = data.Entries.length > 0 ? Math.max(...data.Entries.map(e => e.id)) + 1 : 1;
    const time = new Date().toISOString().replace("Z", ".0000000+00:00");
    const entry = { id: newId, author, time, changes };

    data.Entries.push(entry);
    data.Entries.sort((a, b) => a.id - b.id);

    fs.writeFileSync(
        CHANGELOG_PATH,
        yaml.dump(data, { lineWidth: 120 })
    );

    console.log(`Merge commit added in pirate.yml`);
}

function getChanges(body) {
    const matches = Array.from(body.matchAll(EntryRegex));
    const entries = [];

    matches.forEach(([_, typeRaw, message]) => {
        let type = typeRaw.charAt(0).toUpperCase() + typeRaw.slice(1).toLowerCase();
        entries.push({ type, message });
    });

    return entries;
}

main();