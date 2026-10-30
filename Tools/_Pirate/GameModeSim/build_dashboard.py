"""Aggregate the game-mode simulator CSVs into one self-contained dashboard page.

    python Tools/_Pirate/GameModeSim/build_dashboard.py [output_dir ...] [--out DIR] [--sample N]

Reads rounds.csv / events.csv / timeline.csv / meta.json from the simulator output
(default Tools/_Pirate/GameModeSim/output), writes summary.json and dashboard.html to --out
(default: the first folder).

Several folders are merged: a mode re-run alone (`--presets X --out dir`)
replaces that mode from earlier folders. Rounds are seeded by mode id, so merged modes still play
the same worlds, as long as the runs share seed, rounds and scenarios (checked) and the population model.
Standard library only.
"""
import argparse
import csv
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).parent
_args = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
_args.add_argument('dirs', nargs='*', type=Path, default=[HERE / 'output'], help='simulator output folders, later ones win')
_args.add_argument('--out', type=Path, help='where to write the dashboard (default: the first folder)')
_args.add_argument('--sample', type=int, default=100, help='rounds per mode and setup in the round browser')
ARGS = _args.parse_args()
DIRS = ARGS.dirs
OUT = ARGS.out or DIRS[0]
ROUND_SAMPLE = ARGS.sample

BROWSER_EVENT_FIELDS = ['seq', 'source', 'rule', 'added', 'started', 'cost', 'budgetBefore', 'budgetAfter',
                        'target', 'spawned', 'blocked', 'unfilled', 'antags', 'connected', 'alive', 'ghosts', 'secAlive',
                        'arrivals']
BROWSER_ROUND_FIELDS = ['round', 'startBudget', 'endBudget', 'chaosVariation', 'roundstartAntags', 'midroundAntags',
                        'totalAntags', 'peakAliveAntags', 'deaths', 'secAtStart', 'firstMidroundAntagMin', 'tpStartPoints']

LABELS = {
    "SecretPlusMid": "Ентропія",
    "SecretPlusLow": "Турбулентність",
    "SecretSalt": "Редшифт",
    "SecretSalt14": "Редшифт 1.4",
    "SecretTP": "Секрет ТП",
    "Blueshift": "Блюшифт",
}
SLOTS = {"Blueshift": 1, "SecretSalt": 8, "SecretSalt14": 9, "SecretTP": 3, "SecretPlusMid": 4, "SecretPlusLow": 7}

# Match the real-log family split so simulated and observed counts are comparable.
MAJOR = {'Nukeops', 'Honkops', 'Revolutionary', 'Zombie', 'ZombieOutbreak', 'CosmicCult', 'BloodCult', 'Shadowling',
         'BlobGameMode', 'BlobRule', 'BlobMidround', 'BlobSpawn', 'XenomorphsInfestationRoundstart',
         'XenomorphsInfestation', 'Xenoborgs', 'Wizard', 'WizardMidround', 'DragonSpawn', 'SlaughterDemonMidround',
         'SlaughterDemonSummon', 'WraithMidround', 'HasturMidround', 'TerrorSpidersSpawn', 'ReplicatorSpawn',
         'BingleSpawn', 'Letoferol', 'Gangwars'}
MINOR = {'Thief', 'OopsAllThieves', 'Devil', 'BorerSpawn', 'Fugitive', 'FloorGoblinMidRound', 'FloorGoblinSpawn'}
CREW = {'Traitor', 'SleeperAgents', 'TraitorMidround', 'Changeling', 'Heretic', 'Vampire', 'CorporateAgent',
        'BloodBrothers', 'Conspirators', 'MalfAi', 'MalfAiMidround', 'MalfAiDoomsdayMidround', 'SiliconLiberation', 'TraitorReinforcement',
        'VampireMidround'}
FAMILIES = ['Великі / ескалаційні', 'Екіпаж / приховані', 'Зовнішні / бойові', 'Дрібні / умовні']


COPY_PREFIX = re.compile(r'^(Blueshift|SecretSalt(?=[A-Z]))')


def canon(rule):
    if not COPY_PREFIX.match(rule):
        return rule
    return re.sub(r'(Late|Wave\d+)$', '', COPY_PREFIX.sub('', rule)) or rule


def family(rule):
    n = re.sub(r'(Late|Wave\d+)$', '', COPY_PREFIX.sub('', rule.removeprefix('Token')))
    if n in MAJOR:
        return FAMILIES[0]
    if n in CREW:
        return FAMILIES[1]
    if n in MINOR:
        return FAMILIES[3]
    return FAMILIES[2]


def num(v):
    return float(v) if v not in ('', None) else None


def pct(values, q):
    if not values:
        return None
    values = sorted(values)
    k = (len(values) - 1) * q
    lo, hi = int(k), min(int(k) + 1, len(values) - 1)
    return values[lo] + (values[hi] - values[lo]) * (k - lo)


def r2(v):
    return None if v is None else round(v, 2)


LEVER_METRICS = [
    ('totalAntags', 'Антагоністів за раунд', ''),
    ('midroundAntags', 'Антагоністів посеред раунду', ''),
    ('roundstartAntags', 'Антагоністів на початку раунду', ''),
    ('peakAliveAntags', 'Найбільше живих антагоністів', ''),
    ('major', 'Раунди з великою загрозою', '%'),
    ('quietMax', 'Найдовший час без нового антагоніста', ' хв'),
    ('firstMidroundAntag', 'Перший антагоніст посеред раунду', ' хв'),
    ('deaths', 'Смертей за раунд', ''),
    ('secAtStart', 'Співробітників СБ на початку раунду', ''),
    ('tpStartPoints', 'Очки ТП на початку раунду', ''),
]
LEVER_BINS = 5
PARAM_LABELS = {
    'joinWindowMinutes': ('Час на пізній вхід', 'хв'),
    'joinCurve': ('Крива часу входу', 'крива'),
    'observers': ('Спостерігачі', 'гравців'),
    'crewDeathPerMinute': ('Смертність екіпажу', 'за хв'),
    'crewDeathPerAntagPerMinute': ('Смерті екіпажу на антагоніста', 'за хв'),
    'antagDeathPerMinute': ('Смертність антагоністів', 'за хв'),
    'antagDeathPerSecPerMinute': ('Смерті антагоністів на співробітника СБ', 'за хв'),
    'revivalChance': ('Шанс воскресіння', 'частка'),
    'antagOptInChance': ('Бажання грати антагоністом', 'частка'),
    'ghostRoleTakeChance': ('Взяті ролі привидів', 'частка'),
    'securityWeightMultiplier': ('Вага посад СБ', '×'),
    'glimmerStart': ('Мерехтіння на початку', ''),
    'glimmerPerMinute': ('Зростання мерехтіння', 'за хв'),
    'ghostRoleTakeMedianMinutes': ('Очікування ролі привида', 'медіана хв'),
}


def preset_slots(presets):
    used = {SLOTS[p] for p in presets if p in SLOTS}
    free = [i for i in range(1, 10) if i not in used]
    return [{'id': p, 'label': LABELS.get(p, p), 'slot': SLOTS.get(p) or free.pop(0)} for p in presets]


def camel(obj):
    # Older meta.json files used PascalCase keys.
    if isinstance(obj, dict):
        return {(k[:1].lower() + k[1:] if k[:1].isupper() and not k.isupper() else k): camel(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [camel(v) for v in obj]
    return obj


# Reject incompatible runs; warn on population differences to avoid silently mixing worlds.
MERGE_KEYS = ('seed', 'rounds', 'scenarios')
MERGE_SKIP = {'presets', 'outputDir', 'extraPrototypes', *MERGE_KEYS}


def load_runs(dirs):
    metas = [camel(json.loads((d / 'meta.json').read_text(encoding='utf-8'))) for d in dirs]
    owner, order = {}, []
    for d, m in zip(dirs, metas):
        for p in m['config']['presets']:
            if p not in owner:
                order.append(p)
            owner[p] = d
    meta = metas[0]
    if len(dirs) == 1:
        return meta, owner

    base = meta['config']
    for d, m in zip(dirs[1:], metas[1:]):
        cfg = m['config']
        for k in MERGE_KEYS:
            if cfg.get(k) != base.get(k):
                sys.exit(f"cannot merge {d}: '{k}' differs from {dirs[0]}")
        diff = sorted(k for k in set(cfg) | set(base) if k not in MERGE_SKIP and cfg.get(k) != base.get(k))
        if diff:
            print(f"warning: {d} uses a different population model ({', '.join(diff)}); its modes see other worlds")

    infos = {i['id']: (d, i) for d, m in zip(dirs, metas) for i in m['presets']}
    merged = dict(meta)
    merged['config'] = {**base, 'presets': order}
    merged['presets'] = [i for d, i in infos.values() if owner.get(i['id']) == d]
    merged['warnings'] = [w for m in metas for w in m.get('warnings') or []]
    merged['generated'] = max(m['generated'] for m in metas)
    merged['seconds'] = sum(m['seconds'] for m in metas)
    return merged, owner


def read_rows(name, owner):
    for d in dict.fromkeys(owner.values()):
        for row in csv.DictReader(open(d / name, encoding='utf-8')):
            if owner.get(row['preset']) == d:
                yield row


def main():
    meta, owner = load_runs(DIRS)
    config = meta['config']
    scenarios = {s['name']: s for s in config['scenarios']}
    ranges = dict(config.get('randomize') or {})
    randomized = [p for p in (meta.get('randomized') or []) if p in ranges]

    rounds = defaultdict(list)
    for row in read_rows('rounds.csv', owner):
        rounds[(row['preset'], row['scenario'])].append(row)

    arrivals = defaultdict(list)          # (p, s, round) -> [minute]
    major_rounds = defaultdict(set)       # (p, s) -> {round}
    first_major = defaultdict(dict)       # (p, s) -> {round: minute}
    family_antags = defaultdict(Counter)  # (p, s) -> family -> antags
    label_antags = defaultdict(Counter)   # (p, s) -> antag label -> antags
    rule_rounds = defaultdict(lambda: defaultdict(set))
    rule_count = defaultdict(Counter)
    rule_antags = defaultdict(Counter)
    rule_first = defaultdict(lambda: defaultdict(dict))
    rule_source = {}
    rule_type = {}
    raw_source = {}
    raw_type = {}
    rule_cost = defaultdict(lambda: defaultdict(list))
    bucket_antags = defaultdict(Counter)  # (p, s) -> 10-min bucket -> antags spawned midround
    bucket_events = defaultdict(Counter)  # (p, s) -> bucket -> midround antag events
    bucket_all = defaultdict(Counter)     # (p, s) -> bucket -> all midround events
    rs_seq = defaultdict(list)            # (p, s, round) -> [(seq, rule)] roundstart picks
    browse_events = defaultdict(list)     # (p, s, round) -> compact event rows, sampled rounds only
    interned = {'rule': {}, 'source': {}}

    def intern(kind, value):
        table = interned[kind]
        if value not in table:
            table[value] = len(table)
        return table[value]

    def short(v):
        return None if v in ('', None) else round(float(v), 1)
    mid_seq = defaultdict(list)           # (p, s, round) -> [(minute, seq, rule, spawned)] midround antag events
    mid_arrivals = defaultdict(list)      # (p, s, round) -> [(minute, seq)] each midround antag's join time

    for e in read_rows('events.csv', owner):
        key = (e['preset'], e['scenario'])
        rnd = int(e['round'])
        rule = e['rule']
        src = e['source']
        started = num(e['startedMin'])
        spawned = int(e['antagSpawned'] or 0)
        is_antag = e['isAntag'] == '1'
        # Ghost-role arrivals can lag behind their event start.
        arrived = [float(x) for x in (e.get('arrivals') or '').split(';') if x]
        if not arrived and spawned and started is not None:
            arrived = [started] * spawned
        if rnd < ROUND_SAMPLE:
            browse_events[(e['preset'], e['scenario'], rnd)].append([
                int(e['seq']), intern('source', src), intern('rule', rule), short(e['addedMin']), short(started),
                short(e['chaosCost']), short(e['budgetBefore']), short(e['budgetAfter']), int(e['antagTarget'] or 0),
                spawned, int(e['antagBlocked'] or 0), int(e['antagUnfilled'] or 0), e['antags'] or None,
                int(e['connected']), int(e['alive']), int(e['ghosts']), int(e['secAlive']),
                [round(a, 1) for a in arrived]])

        # Group mode-specific copies under base ids for cross-mode comparisons; preserve exact ids in the browser.
        raw_source.setdefault(rule, set()).add(src)
        raw_type[rule] = e['eventType']
        rule = canon(rule)
        rule_source.setdefault(rule, set()).add(src)
        rule_type[rule] = e['eventType']
        rule_rounds[key][rule].add(rnd)
        rule_count[key][rule] += 1
        rule_antags[key][rule] += spawned
        if e['chaosCost']:
            rule_cost[key][rule].append(float(e['chaosCost']))
        first = rule_first[key][rule]
        t = started if started is not None else num(e['addedMin'])
        if t is not None and (rnd not in first or t < first[rnd]):
            first[rnd] = t

        if src == 'roundstart':
            rs_seq[(e['preset'], e['scenario'], rnd)].append((int(e['seq']), rule))
        elif is_antag and t is not None:
            mid_seq[(e['preset'], e['scenario'], rnd)].append((t, int(e['seq']), rule, spawned))

        if src == 'midround' and t is not None:
            bucket_all[key][int(t // 10)] += 1
        if src != 'roundstart' and is_antag and t is not None:
            bucket_events[key][int(t // 10)] += 1
        if src != 'roundstart':
            for a in arrived:
                bucket_antags[key][int(a // 10)] += 1
                mid_arrivals[(e['preset'], e['scenario'], rnd)].append((a, int(e['seq'])))

        if arrived:
            arrivals[(e['preset'], e['scenario'], rnd)].extend(arrived)
            fam = family(rule)
            family_antags[key][fam] += spawned
            for part in e['antags'].split(';'):
                if ':' in part:
                    label, n = part.rsplit(':', 1)
                    label_antags[key][label] += int(n)
            if fam == FAMILIES[0]:
                major_rounds[key].add(rnd)
                fm = first_major[key]
                if rnd not in fm or min(arrived) < fm[rnd]:
                    fm[rnd] = min(arrived)

    timeline = defaultdict(lambda: defaultdict(lambda: defaultdict(list)))
    for t in read_rows('timeline.csv', owner):
        m = round(float(t['minute']))
        bucket = timeline[(t['preset'], t['scenario'])][m]
        for f in ('alive', 'ghosts', 'aliveAntags', 'secAlive', 'budget', 'tpTotal', 'tpUsed', 'tpAvailable', 'connected'):
            v = num(t[f])
            if v is not None:
                bucket[f].append(v)

    groups = {}
    for (preset, scen), rs in rounds.items():
        key = (preset, scen)
        n = len(rs)
        minutes = scenarios[scen]['roundMinutes']

        def mean(field):
            return statistics.fmean(float(r[field] or 0) for r in rs)

        def spread(field):
            vals = [float(r[field] or 0) for r in rs]
            return {'mean': r2(statistics.fmean(vals)), 'p10': r2(pct(vals, .1)), 'p50': r2(pct(vals, .5)),
                    'p90': r2(pct(vals, .9))}

        quiet = []
        for r in rs:
            times = sorted(arrivals.get((preset, scen, int(r['round'])), []))
            marks = [0.0] + times + [minutes]
            quiet.append(max(b - a for a, b in zip(marks, marks[1:])))

        first_mid = [float(r['firstMidroundAntagMin']) for r in rs if r['firstMidroundAntagMin']]

        close = 0
        for r in rs:
            joined = sorted(mid_arrivals.get((preset, scen, int(r['round'])), []))
            if any(b[0] - a[0] < 10 and a[1] != b[1] for a, b in zip(joined, joined[1:])):
                close += 1
        fm = list(first_major[key].values())

        tl = timeline[key]
        tmins = sorted(tl)

        def series(field, band=False):
            out = []
            for m in tmins:
                vals = tl[m].get(field)
                if not vals:
                    out.append(None)
                    continue
                if band:
                    out.append([r2(statistics.fmean(vals)), r2(pct(vals, .25)), r2(pct(vals, .75))])
                else:
                    out.append(r2(statistics.fmean(vals)))
            return out

        buckets = int(minutes // 10)
        hist = Counter(int(r['totalAntags']) for r in rs)
        primary = Counter(canon(r['primaryRule']) if r['primaryRule'] else '(none)' for r in rs)

        rules = {}
        for rule, rset in rule_rounds[key].items():
            firsts = list(rule_first[key][rule].values())
            costs = rule_cost[key][rule]
            rules[rule] = {
                'rounds': r2(100 * len(rset) / n),
                'count': r2(rule_count[key][rule] / n),
                'antags': r2(rule_antags[key][rule] / n),
                'first': r2(pct(firsts, .5)),
                'cost': r2(statistics.fmean(costs)) if costs else None,
            }

        outcome = []
        for r, q in zip(rs, quiet):
            rnd = int(r['round'])
            outcome.append({
                'totalAntags': float(r['totalAntags']), 'midroundAntags': float(r['midroundAntags']),
                'roundstartAntags': float(r['roundstartAntags']), 'peakAliveAntags': float(r['peakAliveAntags']),
                'major': 100.0 if rnd in major_rounds[key] else 0.0, 'quietMax': q,
                'firstMidroundAntag': num(r['firstMidroundAntagMin']), 'deaths': float(r['deaths']),
                'secAtStart': float(r['secAtStart']), 'tpStartPoints': float(r['tpStartPoints']),
            })

        levers = {}
        for param in randomized:
            lo, hi = ranges[param]
            binned = [[] for _ in range(LEVER_BINS)]
            for r, o in zip(rs, outcome):
                v = num(r.get('p_' + param))
                if v is None:
                    continue
                b = min(LEVER_BINS - 1, max(0, int((v - lo) / (hi - lo) * LEVER_BINS))) if hi > lo else 0
                binned[b].append(o)
            levers[param] = {}
            for m, _, _ in LEVER_METRICS:
                row = []
                for bin_ in binned:
                    vals = [o[m] for o in bin_ if o[m] is not None]
                    row.append(r2(statistics.fmean(vals)) if vals else None)
                levers[param][m] = row

        rs_order = []
        for k in range(4):
            picks = Counter()
            reached = 0
            for r in rs:
                seq = sorted(rs_seq.get((preset, scen, int(r['round'])), []))
                if len(seq) > k:
                    reached += 1
                    picks[seq[k][1]] += 1
            rs_order.append({'reached': r2(100 * reached / n),
                             'rules': [[rule, r2(100 * c / n)] for rule, c in picks.most_common(10)]})

        mid_order = []
        for k in range(8):
            picks, when, spawned_by, reached_t = Counter(), defaultdict(list), Counter(), []
            for r in rs:
                seq = sorted(mid_seq.get((preset, scen, int(r['round'])), []))
                if len(seq) > k:
                    t, _, rule, spawned = seq[k]
                    picks[rule] += 1
                    when[rule].append(t)
                    spawned_by[rule] += spawned
                    reached_t.append(t)
            mid_order.append({
                'reached': r2(100 * len(reached_t) / n),
                'minute': [r2(pct(reached_t, .1)), r2(pct(reached_t, .5)), r2(pct(reached_t, .9))],
                'rules': [[rule, r2(100 * c / n), r2(pct(when[rule], .5)), r2(spawned_by[rule] / c)]
                          for rule, c in picks.most_common(10)],
            })

        per_rule = defaultdict(lambda: {'rounds': set(), 'runs': 0, 'spawned': 0, 'empty': 0, 'times': []})
        for r in rs:
            for t, _, rule, spawned in mid_seq.get((preset, scen, int(r['round'])), []):
                d = per_rule[rule]
                d['rounds'].add(int(r['round']))
                d['runs'] += 1
                d['spawned'] += spawned
                d['empty'] += spawned == 0
                d['times'].append(t)
        mid_rules = {rule: {'rounds': r2(100 * len(d['rounds']) / n), 'runs': r2(d['runs'] / n),
                            'antagsPerRun': r2(d['spawned'] / d['runs']), 'empty': r2(100 * d['empty'] / d['runs']),
                            'median': r2(pct(d['times'], .5))}
                     for rule, d in per_rule.items()}

        groups.setdefault(scen, {})[preset] = {
            'n': n,
            'levers': levers,
            'rsOrder': rs_order,
            'midOrder': mid_order,
            'midRules': mid_rules,
            'kpi': {
                'totalAntags': spread('totalAntags'),
                'roundstartAntags': spread('roundstartAntags'),
                'midroundAntags': spread('midroundAntags'),
                'midroundAntagEvents': spread('midroundAntagEvents'),
                'midroundEvents': spread('midroundEvents'),
                'sideEvents': spread('sideEvents'),
                'peakAliveAntags': spread('peakAliveAntags'),
                'deaths': spread('deaths'),
                'startBudget': spread('startBudget'),
                'secAtStart': spread('secAtStart'),
                'tpStartPoints': spread('tpStartPoints'),
                'quietMax': {'p50': r2(pct(quiet, .5)), 'p10': r2(pct(quiet, .1)), 'p90': r2(pct(quiet, .9))},
                'firstMidroundAntag': {'p50': r2(pct(first_mid, .5)), 'p10': r2(pct(first_mid, .1)),
                                       'p90': r2(pct(first_mid, .9))},
                'closeArrivals': r2(100 * close / n),
                'noRoundstartAntag': r2(100 * sum(1 for r in rs if int(r['roundstartAntags']) == 0) / n),
                'majorRounds': r2(100 * len(major_rounds[key]) / n),
                'firstMajor': r2(pct(fm, .5)),
                'tpGreenshiftRoll': r2(100 * sum(1 for r in rs if r['tpGreenshiftRoll'] == '1') / n),
                'antagBlocked': r2(mean('antagBlocked')),
                'antagUnfilled': r2(mean('antagUnfilled')),
                'tpFilteredStarts': r2(mean('tpFilteredStarts')),
            },
            'timeline': {
                'minutes': tmins,
                'aliveAntags': series('aliveAntags', True),
                'budget': series('budget', True),
                'alive': series('alive'),
                'ghosts': series('ghosts'),
                'secAlive': series('secAlive', True),
                'connected': series('connected'),
                'tpTotal': series('tpTotal'),
                'tpUsed': series('tpUsed'),
                'tpAvailable': series('tpAvailable', True),
            },
            'arrivals': {
                'antags': [r2(bucket_antags[key][b] / n) for b in range(buckets)],
                'antagEvents': [r2(bucket_events[key][b] / n) for b in range(buckets)],
                'events': [r2(bucket_all[key][b] / n) for b in range(buckets)],
            },
            'hist': {str(k): r2(100 * v / n) for k, v in sorted(hist.items())},
            'families': {f: r2(family_antags[key][f] / n) for f in FAMILIES},
            'labels': {k: r2(v / n) for k, v in label_antags[key].most_common(30)},
            'primary': [[k, r2(100 * v / n)] for k, v in primary.most_common(14)],
            'rules': rules,
        }

    # Match sampled round numbers across presets to compare the same population rolls.
    param_cols = [c for c in next(iter(rounds.values()))[0].keys() if c.startswith('p_')]
    browser = {
        'sample': ROUND_SAMPLE,
        'eventFields': BROWSER_EVENT_FIELDS,
        'roundFields': BROWSER_ROUND_FIELDS,
        'params': [c[2:] for c in param_cols],
        'rules': sorted(interned['rule'], key=interned['rule'].get),
        'sources': sorted(interned['source'], key=interned['source'].get),
        'rounds': {},
    }
    for (preset, scen), rs in rounds.items():
        out = []
        for r in rs:
            rnd = int(r['round'])
            if rnd >= ROUND_SAMPLE:
                continue
            events = sorted(browse_events.get((preset, scen, rnd), []),
                            key=lambda ev: (ev[4] if ev[4] is not None else ev[3], ev[0]))
            out.append({
                'r': [short(r[f]) if f != 'round' else rnd for f in BROWSER_ROUND_FIELDS],
                'p': [num(r[c]) for c in param_cols],
                'e': events,
            })
        browser['rounds'].setdefault(scen, {})[preset] = out

    summary = {
        'generated': meta['generated'],
        'seconds': meta['seconds'],
        'rounds': config['rounds'],
        'presets': preset_slots(config['presets']),
        'scenarios': config['scenarios'],
        'config': {k: v for k, v in config.items() if k not in ('scenarios', 'presets', 'jobs')},
        'jobs': config.get('jobs'),
        'presetInfo': meta['presets'],
        'warnings': meta['warnings'],
        'families': FAMILIES,
        'paramLabels': {k: v[0] for k, v in PARAM_LABELS.items()},
        'levers': {
            'params': [{'id': p, 'label': PARAM_LABELS.get(p, (p, ''))[0], 'unit': PARAM_LABELS.get(p, (p, ''))[1],
                        'range': ranges[p],
                        'bins': [round(ranges[p][0] + (ranges[p][1] - ranges[p][0]) * (i + .5) / LEVER_BINS, 6)
                                 for i in range(LEVER_BINS)]} for p in randomized],
            'metrics': [{'id': m, 'label': label, 'unit': u} for m, label, u in LEVER_METRICS],
        },
        'ruleInfo': {**{r: {'sources': sorted(s), 'type': raw_type.get(r, ''), 'family': family(r)}
                        for r, s in raw_source.items()},
                     **{r: {'sources': sorted(s), 'type': rule_type.get(r, ''), 'family': family(r)}
                        for r, s in rule_source.items()}},
        'groups': groups,
        'browser': browser,
    }

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'summary.json').write_text(json.dumps(summary, separators=(',', ':')), encoding='utf-8')
    template = (HERE / 'dashboard.template.html').read_text(encoding='utf-8')
    page = template.replace('/*__DATA__*/null', json.dumps(summary, separators=(',', ':')))
    (OUT / 'dashboard.fragment.html').write_text(page, encoding='utf-8')
    shell = ('<!doctype html>\n<html lang="uk">\n<head>\n<meta charset="utf-8">\n'
             '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
             '<style>:root{color-scheme:dark}body{margin:0}img{max-width:100%}[hidden]{display:none!important}</style>\n'
             '</head>\n<body>\n')
    (OUT / 'dashboard.html').write_text(shell + page + '\n</body>\n</html>\n', encoding='utf-8')
    print(f"{sum(g['n'] for s in groups.values() for g in s.values())} rounds -> {OUT / 'dashboard.html'}"
          f" ({len(page) // 1024} KB)")


if __name__ == '__main__':
    main()
