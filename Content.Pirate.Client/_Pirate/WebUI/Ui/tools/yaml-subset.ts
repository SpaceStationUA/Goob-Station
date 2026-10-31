/**
 * A YAML subset parser, for the bake.
 *
 * ## Why not a real one
 *
 * The obvious move is to add `yaml` or `js-yaml` as a devDependency. That is the
 * right call for a general tool and the wrong one here: this is a browser UI
 * package that ships to players, and a parser dependency is a dependency on
 * everyone's install and in everyone's lockfile, in exchange for reading one file
 * in a known shape.
 *
 * So this reads exactly the subset the galaxy prototype uses, and REFUSES
 * everything else. That word is the whole design. A permissive parser that
 * silently mis-reads a construct is how a border ends up in the wrong place with
 * no error anywhere; a parser that stops and tells you which line it choked on is
 * a tool you can trust with geometry.
 *
 * ## What it accepts
 *
 *   - a sequence of mappings at the top level (`- type: x` then indented keys)
 *   - nested mappings, one level or more, by indentation
 *   - sequences of scalars (`polygon:` then `- [1, 2]`)
 *   - flow sequences of numbers, on one line: `[1, 2.5, -3]`
 *   - `#` comments, whole-line and trailing
 *   - blank lines
 *
 * ## What it refuses
 *
 *   - block scalars (`|`, `>`), anchors, aliases, tags, multi-document streams
 *   - flow MAPPINGS (`{a: 1}`) -- easy to confuse with a sequence, and nothing
 *     here needs one
 *   - quoted strings, on the grounds that a polygon never contains one and
 *     supporting them would mean supporting escapes
 *   - a duplicate key in one mapping, which is always a mistake
 *
 * Every refusal names the line. `parseGalaxyYaml` is the only entry point and it
 * throws `YamlError`, which the bake prints and exits on.
 */

export class YamlError extends Error {
  constructor(
    message: string,
    readonly line: number,
    readonly file: string,
  ) {
    super(`${file}:${line}: ${message}`);
    this.name = "YamlError";
  }
}

/** One node of the parsed document. */
export type YamlValue =
  | { kind: "map"; entries: [string, YamlValue][]; line: number }
  | { kind: "seq"; items: YamlValue[]; line: number }
  | { kind: "scalar"; value: string; line: number };

interface Line {
  /** 1-based, for errors. */
  n: number;
  /** Indent in spaces. Tabs are rejected: they make indentation ambiguous. */
  indent: number;
  text: string;
}

function scan(src: string, file: string): Line[] {
  const out: Line[] = [];
  src.split(/\r?\n/).forEach((raw, i) => {
    const n = i + 1;
    // The tab test comes FIRST, on the raw line. An earlier version expanded tabs
    // to spaces and then checked the expanded line for tabs, which can never be
    // true -- the guard was dead code, and it was found by the test that asserts
    // a tab is refused rather than by reading it.
    //
    // And it matters: a tab is one column of indent or eight depending on who is
    // counting, so a file indented with tabs parses to a DIFFERENT shape from the
    // same file indented with spaces, silently.
    if (raw.includes("\t")) {
      throw new YamlError("tab character; indent with spaces", n, file);
    }
    const expanded = raw;
    const indent = expanded.length - expanded.trimStart().length;
    const text = expanded.trim();
    if (text === "" || text.startsWith("#")) return;
    out.push({ n, indent, text: stripComment(text) });
  });
  return out.filter((l) => l.text !== "");
}

/**
 * Drop a trailing comment.
 *
 * Only when the `#` starts a token, so a `#` inside a token survives. Nothing in
 * the galaxy prototype contains one, but silently truncating a value is worse
 * than not supporting the case.
 */
function stripComment(text: string): string {
  let inFlow = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === "[") inFlow = true;
    else if (c === "]") inFlow = false;
    else if (c === "#" && !inFlow && (i === 0 || /\s/.test(text[i - 1]))) {
      return text.slice(0, i).trimEnd();
    }
  }
  return text;
}

class Reader {
  private i = 0;
  constructor(
    private readonly lines: Line[],
    private readonly file: string,
  ) {}

  get done(): boolean {
    return this.i >= this.lines.length;
  }

  peek(): Line {
    return this.lines[this.i];
  }

  next(): Line {
    return this.lines[this.i++];
  }

  fail(line: Line | undefined, message: string): never {
    throw new YamlError(message, line?.n ?? this.lines.at(-1)?.n ?? 0, this.file);
  }

  /** A block at `indent`, given the cursor is on its first line. */
  parseBlock(indent: number): YamlValue {
    const first = this.peek();
    if (!first) this.fail(undefined, "unexpected end of file");
    if (first.indent !== indent) {
      this.fail(first, `expected indent ${indent}, found ${first.indent}`);
    }
    return first.text.startsWith("- ") || first.text === "-"
      ? this.parseSeq(indent)
      : this.parseMap(indent);
  }

  private parseSeq(indent: number): YamlValue {
    // PEEK, not next(). `parseBlock` peeks before dispatching here, so consuming
    // the dash up front left the loop looking at the first ITEM KEY -- indented,
    // not a dash, and not the right indent either -- and it failed on every
    // sequence in the file including the simplest one.
    const start = this.peek();
    const items: YamlValue[] = [];
    for (;;) {
      const l = this.peek();
      if (!l || l.indent < indent) break;
      if (l.indent > indent) this.fail(l, `unexpected indent ${l.indent} in a sequence`);
      if (!l.text.startsWith("- ") && l.text !== "-") break;
      this.next();
      const inline = l.text === "-" ? "" : l.text.slice(2).trim();
      items.push(this.parseItemBody(l, inline, indent + 2));
    }
    return { kind: "seq", items, line: start.n };
  }

  /**
   * The body of one `- ` item: either an inline value, or the indented block that
   * follows it.
   *
   * `- type: galaxy` is a mapping whose first key sits on the dash's own line, and
   * the rest are indented under it. That is the shape every prototype in the repo
   * uses, and getting it wrong is the classic YAML-off-by-one-indent bug.
   */
  private parseItemBody(dash: Line, inline: string, childIndent: number): YamlValue {
    if (inline === "") {
      const next = this.peek();
      if (!next || next.indent <= dash.indent) {
        return { kind: "scalar", value: "", line: dash.n };
      }
      return this.parseBlock(next.indent);
    }
    // `key: value` on the dash line starts a mapping whose remaining keys are
    // indented to the same column as `key`.
    const colon = findColon(inline);
    if (colon >= 0) {
      const key = inline.slice(0, colon).trim();
      const value = inline.slice(colon + 1).trim();
      const keyCol = dash.indent + 2;
      const entries: [string, YamlValue][] = [
        [key, this.scalarOrBlock(dash, value, keyCol)],
      ];
      const seen = new Set([key]);
      for (;;) {
        const l = this.peek();
        if (!l || l.indent !== keyCol) break;
        if (l.text.startsWith("- ")) break;
        this.next();
        const c = findColon(l.text);
        if (c < 0) this.fail(l, `expected "key: value", found "${l.text}"`);
        const k = l.text.slice(0, c).trim();
        if (seen.has(k)) this.fail(l, `duplicate key "${k}"`);
        seen.add(k);
        entries.push([k, this.scalarOrBlock(l, l.text.slice(c + 1).trim(), keyCol)]);
      }
      return { kind: "map", entries, line: dash.n };
    }
    return parseFlowScalar(dash, inline, this.file);
  }

  private parseMap(indent: number): YamlValue {
    // Peek, for the same reason parseSeq does.
    const start = this.peek();
    const entries: [string, YamlValue][] = [];
    const seen = new Set<string>();
    for (;;) {
      const l = this.peek();
      if (!l || l.indent < indent) break;
      if (l.indent > indent) this.fail(l, `unexpected indent ${l.indent}`);
      if (l.text.startsWith("- ")) break;
      this.next();
      const c = findColon(l.text);
      if (c < 0) this.fail(l, `expected "key: value", found "${l.text}"`);
      const k = l.text.slice(0, c).trim();
      if (seen.has(k)) this.fail(l, `duplicate key "${k}"`);
      seen.add(k);
      entries.push([k, this.scalarOrBlock(l, l.text.slice(c + 1).trim(), indent)]);
    }
    return { kind: "map", entries, line: start.n };
  }

  /** Either the inline value after a colon, or the block indented beneath it. */
  private scalarOrBlock(at: Line, inline: string, keyCol: number): YamlValue {
    if (inline !== "") return parseFlowScalar(at, inline, this.file);
    const next = this.peek();
    if (!next) return { kind: "scalar", value: "", line: at.n };
    if (next.indent > keyCol) return this.parseBlock(next.indent);
    // A sequence may sit at the SAME indent as its key, which is the style used
    // throughout Resources/Prototypes.
    if (next.indent === keyCol && (next.text.startsWith("- ") || next.text === "-")) {
      return this.parseSeq(keyCol);
    }
    return { kind: "scalar", value: "", line: at.n };
  }
}

function findColon(text: string): number {
  if (text.startsWith("[")) return -1;
  const c = text.indexOf(":");
  return c;
}

function parseFlowScalar(at: Line, text: string, file: string): YamlValue {
  if (text.startsWith("[")) {
    if (!text.endsWith("]")) {
      throw new YamlError("unterminated flow sequence", at.n, file);
    }
    const inner = text.slice(1, -1).trim();
    const items: YamlValue[] =
      inner === ""
        ? []
        : inner.split(",").map((part) => {
            const p = part.trim();
            if (p === "") {
              throw new YamlError("empty element in a flow sequence", at.n, file);
            }
            if (!/^-?\d+(\.\d+)?$/.test(p)) {
              throw new YamlError(
                `"${p}" is not a number; this parser only accepts numbers in a ` +
                  "flow sequence, because that is all a coordinate pair is",
                at.n,
                file,
              );
            }
            return { kind: "scalar" as const, value: p, line: at.n };
          });
    return { kind: "seq", items, line: at.n };
  }
  if (text.startsWith("{")) {
    throw new YamlError(
      "flow mappings are not supported; write the keys on their own lines",
      at.n,
      file,
    );
  }
  if (text.startsWith("|") || text.startsWith(">")) {
    throw new YamlError("block scalars are not supported", at.n, file);
  }
  if (text.startsWith("&") || text.startsWith("*") || text.startsWith("!")) {
    throw new YamlError("anchors, aliases and tags are not supported", at.n, file);
  }
  if (text.startsWith('"') || text.startsWith("'")) {
    throw new YamlError("quoted scalars are not supported", at.n, file);
  }
  if (text === "~" || text === "null") {
    return { kind: "scalar", value: "", line: at.n };
  }
  return { kind: "scalar", value: text, line: at.n };
}

/** Parse a document into a sequence of top-level mappings. */
export function parseYamlSequence(src: string, file: string): YamlValue {
  const lines = scan(src, file);
  if (lines.length === 0) {
    throw new YamlError("the file is empty", 1, file);
  }
  const base = Math.min(...lines.map((l) => l.indent));
  if (base !== 0) {
    throw new YamlError(
      `the document starts indented by ${base}; top-level keys must be at column 0`,
      lines.find((l) => l.indent === base)!.n,
      file,
    );
  }
  const r = new Reader(lines, file);
  if (!(r.peek().text.startsWith("- ") || r.peek().text === "-")) {
    throw new YamlError(
      "expected a sequence of `- type: ...` entries at the top level",
      r.peek().n,
      file,
    );
  }
  const doc = r.parseSeq(0);
  if (!r.done) {
    r.fail(r.peek(), `trailing content at indent ${r.peek().indent}`);
  }
  return doc;
}

/* ------------------------------------------------------------------ *
 * Typed accessors
 * ------------------------------------------------------------------ *
 *
 * Every one of these throws rather than returning a default. A bake that
 * defaults a missing polygon to `[]` produces an empty territory and a chart
 * with a hole in it, and the failure shows up as a visual bug a long way from the
 * cause.
 */

export function asMap(v: YamlValue, what: string): Map<string, YamlValue> {
  if (v.kind !== "map") throw new Error(`${what}: expected a mapping, found ${v.kind}`);
  return new Map(v.entries);
}

export function asSeq(v: YamlValue, what: string): YamlValue[] {
  if (v.kind !== "seq") throw new Error(`${what}: expected a sequence, found ${v.kind}`);
  return v.items;
}

export function asString(v: YamlValue, what: string): string {
  if (v.kind !== "scalar") throw new Error(`${what}: expected a value, found ${v.kind}`);
  return v.value;
}

export function asNumber(v: YamlValue, what: string): number {
  const s = asString(v, what);
  const n = Number(s);
  if (!Number.isFinite(n)) {
    throw new Error(`${what} (line ${v.line}): "${s}" is not a number`);
  }
  return n;
}

export function require<T>(
  m: Map<string, YamlValue>,
  key: string,
  what: string,
  read: (v: YamlValue, k: string) => T,
): T {
  const v = m.get(key);
  if (v === undefined) throw new Error(`${what}: missing required key "${key}"`);
  return read(v, `${what}.${key}`);
}
