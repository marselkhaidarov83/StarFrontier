"""Build a deduplicated report from an AppMetrica Logs API JSON/CSV export.

Export only event_name,event_json. This tool makes no network requests and needs no token.
"""
import argparse
import collections
import csv
import html
import json
import uuid
from pathlib import Path

ECONOMY_ACTIONS = {
    'sf_buy_item', 'sf_sell_item', 'sf_refuel', 'sf_repair',
    'sf_mission_reward', 'sf_government_reward',
}


def read_rows(path):
    with path.open(encoding='utf-8-sig', newline='') as stream:
        if path.suffix.lower() == '.json':
            data = json.load(stream)
            return data.get('data', []) if isinstance(data, dict) else data
        return list(csv.DictReader(stream))


def analyze(rows, environment, app_version=None, balance_version=None):
    seen = {}
    counts = collections.Counter()
    money = collections.Counter()
    duplicates = invalid = conflicts = 0
    for row in rows:
        name = row.get('event_name', '')
        if not isinstance(name, str) or not name.startswith('sf_'):
            continue
        try:
            raw = row['event_json']
            event = json.loads(raw) if isinstance(raw, str) else raw
            if event.get('schema_version') != 1 or event.get('action') != name:
                raise ValueError('Unknown schema or mismatched event name')
            event_id = str(uuid.UUID(event['event_id']))
            installation_id = str(uuid.UUID(event['installation_id']))
            if event['environment'] not in ('test', 'production'):
                raise ValueError('Missing environment')
            if event['environment'] != environment:
                continue
            if app_version is not None and event['app_version'] != app_version:
                continue
            if balance_version is not None and event['balance_version'] != balance_version:
                continue
            group = (event['app_version'], event['balance_version'], name, event['result'], event['reason'])
            if not all(isinstance(value, str) for value in group):
                raise ValueError('Invalid grouping fields')
            delta = event.get('credits_delta', 0)
            if name in ECONOMY_ACTIONS and (not isinstance(delta, int) or isinstance(delta, bool)):
                raise ValueError('Invalid credits delta')
            key = (event['environment'], installation_id, event_id)
            signature = json.dumps(event, sort_keys=True, separators=(',', ':'))
            if key in seen:
                duplicates += 1
                if seen[key] != signature:
                    conflicts += 1
                continue
            seen[key] = signature
            counts[group] += 1
            if name in ECONOMY_ACTIONS:
                money[group] += delta
        except (ValueError, TypeError, KeyError, AttributeError):
            invalid += 1
    return {
        'environment': environment,
        'unique_events': sum(counts.values()),
        'duplicates_removed': duplicates,
        'conflicting_duplicates': conflicts,
        'invalid_rows': invalid,
        'groups': [dict(app_version=g[0], balance_version=g[1], action=g[2], result=g[3], reason=g[4],
                        count=count, credits_delta=money[g]) for g, count in sorted(counts.items())],
    }


def write_report(report, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.with_suffix('.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    rows = ''.join('<tr>' + ''.join('<td>' + html.escape(str(g[field])) + '</td>' for field in
                   ('app_version', 'balance_version', 'action', 'result', 'reason', 'count', 'credits_delta')) + '</tr>'
                   for g in report['groups'])
    text = '''<!doctype html><html lang="ru"><meta charset="utf-8"><title>STAR FRONTIER — аналитика</title>
<style>body{font:16px system-ui;margin:40px;color:#e4eaf4;background:#101925}table{border-collapse:collapse;width:100%}td,th{padding:12px;border:1px solid #41516a;text-align:left}th{background:#203046}p{line-height:1.6}</style>
<h1>STAR FRONTIER — проверка событий</h1>'''
    text += '<p>Окружение: <b>' + html.escape(report['environment']) + '</b>. Уникальных событий: ' + str(report['unique_events'])
    text += '. Повторов исключено: ' + str(report['duplicates_removed']) + '. Конфликтов: ' + str(report['conflicting_duplicates'])
    text += '. Некорректных строк: ' + str(report['invalid_rows']) + '.</p>'
    text += '<p>Изменение кредитов суммируется только для экономических операций; контекст миссий и взаимодействий повторно не учитывается. Причина отказа относится к наблюдаемой операции и не объясняет уход игрока.</p>'
    text += '<table><thead><tr><th>Сборка</th><th>Баланс</th><th>Событие</th><th>Результат</th><th>Причина</th><th>Количество</th><th>Изменение кредитов</th></tr></thead><tbody>' + rows + '</tbody></table></html>'
    destination.with_suffix('.html').write_text(text, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('export', type=Path)
    parser.add_argument('--environment', required=True, choices=('test', 'production'))
    parser.add_argument('--app-version')
    parser.add_argument('--balance-version')
    parser.add_argument('--output', type=Path, default=Path('player-analytics-report'))
    args = parser.parse_args()
    report = analyze(read_rows(args.export), args.environment, args.app_version, args.balance_version)
    write_report(report, args.output)
    print(f"Unique: {report['unique_events']}; duplicates: {report['duplicates_removed']}; invalid: {report['invalid_rows']}; conflicts: {report['conflicting_duplicates']}")
    return 1 if report['invalid_rows'] or report['conflicting_duplicates'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
