"""Estimate income from defending against a complete invasion group."""

import re
from collections import defaultdict
from itertools import product
from math import ceil, comb
from pathlib import Path
from statistics import mean, median

from combat_balance_v0_1 import FACTIONS, midpoint, records

PROJECT = Path(__file__).resolve().parents[3]
POPULATION = PROJECT / 'Assets/_Project/Content/Configs/Population'
COLLECTION_SHARE = .20
PERSONAL_SHARE = .30
SERVICE_SECONDS = 45
LATE_DEFENSE_SECONDS = 9 * 60


def invasion_groups(sheets):
    by_guid = {}
    for enemy_id, row in records(sheets['EnemyConfig']).items():
        meta = (PROJECT / (row['E'] + '.meta')).read_text()
        guid = re.search(r'^guid: ([a-f0-9]+)$', meta, re.M).group(1)
        by_guid[guid] = enemy_id

    groups_by_level = defaultdict(list)
    for faction in FACTIONS:
        file = POPULATION / 'EnemyGroupSpawnRules' / f'enemy_group_spawn_{faction}_01.asset'
        asset = file.read_text()
        for level, block in re.findall(r'^  - galaxyLevel: (\d+)\n(.*?)(?=^  - galaxyLevel: |\Z)', asset, re.M | re.S):
            options = re.split(r'^    - weight: \d+\n', block, flags=re.M)[1:]
            assert len(options) == len(FACTIONS)
            faction_id = FACTIONS.index(faction) * 10
            group, = [option for option in options if re.search(rf'^      faction: {faction_id}$', option, re.M)]
            entries = re.findall(r'^      - enemyConfig: .*guid: ([a-f0-9]+),.*\n'
                                 r'        minCount: (\d+)\n        maxCount: (\d+)', group, re.M)
            assert entries and len(entries) == group.count('enemyConfig:')
            counts = [range(int(low), int(high) + 1) for _, low, high in entries]
            enemy_types = [by_guid[guid] for guid, _, _ in entries]
            assert all(enemy_id.startswith(f'enemy_{faction}_') for enemy_id in enemy_types)
            for scenario in product(*counts):
                groups_by_level[int(level)].append(tuple(zip(enemy_types, scenario)))
    assert set(groups_by_level) == set(range(1, 11))
    return groups_by_level


def personal_coverage(count, required, share):
    return sum(comb(count, personal) * share**personal * (1 - share)**(count - personal)
               for personal in range(required, count + 1))


def report_defense(sheets, ships, tick, repair_price, loot, recon_rates, liberation_rates):
    groups = invasion_groups(sheets)
    hulls = records(sheets['HullConfig'])
    first_fire_ticks = max(ceil(ships[f'enemy_{faction}_L01_01'].defense /
                                ships['ally_ranger_L01_01'].damage_per_tick) for faction in FACTIONS)
    print('\n| Ур. галактики | Группа | Лично (30%) | Ремонт, кр. | Кр. за цель | Полный цикл, с | Покрытие при 30% | Оборона при вкладе 20% / 30% / 40%, кр./мин | Разведка / освобождение, кр./мин |')
    print('|---:|---:|---:|---:|---:|---:|---:|---|---|')
    timing = {}
    payouts = {}
    for level, compositions in sorted(groups.items()):
        counts = [sum(count for _, count in group) for group in compositions]
        required = max(1, ceil(median(counts) * PERSONAL_SHARE))
        hull = hulls[f'hull_ally_ranger_L{level:02}_01']
        expense = ceil(midpoint(hull, 'N', 'O') * .5) * repair_price
        rate = ceil(expense / required)
        payouts[level] = rate
        strongest = max(ships[f'enemy_{faction}_L{level:02}_01'].defense for faction in FACTIONS)
        damage_per_tick = strongest / (first_fire_ticks + level - 1.5)
        workloads = [sum(ceil(ships[enemy_id].defense / damage_per_tick) * count for enemy_id, count in group) * tick
                     for group in compositions]
        receipts = [sum(loot[int(enemy_id.split('_')[-2][1:])] * count for enemy_id, count in group) * COLLECTION_SHARE
                    for group in compositions]

        # Equal access to targets scales both personal kills and the player's share of total firepower.
        rates = []
        for share in (.20, PERSONAL_SHARE, .40):
            seconds = [workload * share + SERVICE_SECONDS for workload in workloads]
            profits = [count * share * rate + trophies - expense for count, trophies in zip(counts, receipts)]
            rates.append(mean(profit * 60 / duration for profit, duration in zip(profits, seconds)))
            if share == PERSONAL_SHARE:
                timing[level] = (seconds, profits, receipts, counts)
        coverage = mean(personal_coverage(count, required, PERSONAL_SHARE) for count in counts)
        seconds = mean(timing[level][0])
        comparisons = f'{recon_rates[level]:.0f} / {liberation_rates[level]:.0f}' if level in recon_rates else '—'
        print(f'| {level} | {min(counts)}–{max(counts)} | {required} | {expense:.0f} | {rate} | '
              f'{seconds:.1f} | {coverage:.1%} | {" / ".join(f"{value:.0f}" for value in rates)} | {comparisons} |')

    print('\n| Ур. | Дополнительные действия в обороне для порядка доходов, с | Допустимая стоимость выпавшего оснащения относительно исходной |')
    print('|---:|---|---|')
    common_allowances = set(range(121))
    for level, recon in sorted(recon_rates.items()):
        liberation = liberation_rates[level]
        seconds, profits, receipts, counts = timing[level]
        assert min(profits) >= 0
        # With all profits nonnegative, this duration is already above the useful bound.
        maximum_extra = ceil(mean(profits) * 60 / recon)
        all_rewards = [mean(profit * 60 / (duration + extra) for profit, duration in zip(profits, seconds))
                       for extra in range(maximum_extra + 1)]
        feasible = [extra for extra, income in enumerate(all_rewards) if recon < income < liberation]
        summary = f'{min(feasible)}–{max(feasible)}' if feasible else '—'
        trophy_rate = mean(trophies * 60 / duration for trophies, duration in zip(receipts, seconds))
        mission_rate = all_rewards[0] - trophy_rate
        lower = max(0, (recon - mission_rate) / trophy_rate)
        upper = min(1, (liberation - mission_rate) / trophy_rate)
        loot_range = f'{lower:.1%}–{upper:.1%}' if lower < upper else '—'
        print(f'| {level} | {summary} | {loot_range} |')
        common_allowances = {
            delay for delay in common_allowances
            if recon < mean(profit * 60 / (duration + max(0, count - 3) * delay)
                            for profit, duration, count in zip(profits, seconds, counts)) < liberation
        }

    if common_allowances:
        delay = min(common_allowances)
        seconds, profits, _, counts = timing[10]
        timings = [duration + max(0, count - 3) * delay for duration, count in zip(seconds, counts)]
        income = mean(profit * 60 / duration for profit, duration in zip(profits, timings))
        print(f'Проба на всех сравнимых уровнях: {delay}–{max(common_allowances)} с на врага сверх первых трёх. '
              f'При нижнем пороге на уровне 10: {mean(timings):.0f} с и {income:.0f} кр./мин.')

    late_seconds, _, _, late_counts = timing[10]
    extra_per_enemy = (LATE_DEFENSE_SECONDS - mean(late_seconds)) / mean(max(0, count - 3) for count in late_counts)
    print(f'\nЦель обороны уровня 10: {LATE_DEFENSE_SECONDS} с полного цикла. '
          f'Расчётная надбавка времени: {extra_per_enemy:.2f} с на врага сверх первых трёх.')
    print('| Ур. | Среднее полное время, с | Разведка | Оборона | Освобождение | Порядок доходов соблюдён |')
    print('|---:|---:|---:|---:|---:|---|')
    previous_income = 0
    previous_duration = 0
    researched_rates = {}
    cash_flows = {}
    for level, (seconds, profits, receipts, counts) in sorted(timing.items()):
        adjusted = [duration + max(0, count - 3) * extra_per_enemy for duration, count in zip(seconds, counts)]
        # Shorten the estimate only enough to avoid a decrease in income at the next level.
        for saved_seconds in range(ceil(min(adjusted))):
            durations = [duration - saved_seconds for duration in adjusted]
            income = mean(profit * 60 / duration for profit, duration in zip(profits, durations))
            if income >= previous_income * 1.01:
                break
        duration = mean(durations)
        assert previous_duration < duration and income > previous_income
        assert level != 10 or saved_seconds == 0
        if level in recon_rates:
            recon, liberation = recon_rates[level], liberation_rates[level]
            assert recon < income < liberation
            comparison = f'{recon:.0f} | {income:.0f} | {liberation:.0f} | да'
        else:
            comparison = f'— | {income:.0f} | — | —'
        print(f'| {level} | {duration:.0f} | {comparison} |')
        researched_rates[level] = mean((profit + .30 * PERSONAL_SHARE * count * payouts[level]) * 60 / elapsed
                                       for profit, count, elapsed in zip(profits, counts, durations))
        trophy_rate = mean(trophies * 60 / elapsed for trophies, elapsed in zip(receipts, durations))
        payout_rate = mean(PERSONAL_SHARE * count * payouts[level] * 60 / elapsed
                           for count, elapsed in zip(counts, durations))
        cash_flows[level] = (income - trophy_rate, trophy_rate, payout_rate)
        previous_duration = duration
        previous_income = income

    print('\n| Ур. | Огонь, с | Поиск и смена цели, с | Манёвры, с | Сбор, с | Посадка и обслуживание, с |')
    print('|---:|---:|---:|---:|---:|---:|')
    for level, target in ((1, 51), (5, 268), (8, 421), (10, LATE_DEFENSE_SECONDS)):
        fire = mean(timing[level][0]) - SERVICE_SECONDS
        remainder = target - fire - SERVICE_SECONDS
        print(f'| {level} | {fire:.1f} | {remainder * .15:.1f} | {remainder * .60:.1f} | '
              f'{remainder * .25:.1f} | {SERVICE_SECONDS} |')
    return researched_rates, cash_flows
