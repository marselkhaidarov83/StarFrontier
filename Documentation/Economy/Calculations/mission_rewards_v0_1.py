"""Calculate trial mission rewards and test the agreed income order."""

from collections import defaultdict
from dataclasses import replace
from heapq import heappop, heappush
from itertools import product
from math import ceil, floor
from pathlib import Path
import re
from statistics import median

from activity_income_v0_1 import available_corridors, one_cargo, trade_corridors
from combat_balance_v0_1 import (
    FACTIONS, SHEETS, build_ships, clearance_kills, linked_row, longer_battles,
    group_members, midpoint, number, read_sheets, records, reference_trios, repair_route_scenarios, spawn_rows,
)
from early_credit_balance_v0_1 import fit_trophy_curve, liberation_costs
from defense_balance_v0_1 import invasion_groups, report_defense
from progression_economy_v0_1 import EXTRA_SHEETS


def route_catalog(sheets):
    for route in sheets['Маршруты']:
        if not route.get('A', '').startswith('route_'):
            continue
        endpoints = [linked_row(sheets['Системы'], route[column]) for column in 'EF']
        level = max(int(number(linked_row(sheets['Сектора'], system['D']), 'E')) for system in endpoints)
        yield route['A'], endpoints[0]['A'], endpoints[1]['A'], level, int(number(route, 'H')), route['G'] == 'Нет'


def fuel_path(routes, level, start, target):
    graph = defaultdict(list)
    for _, a, b, rank, fuel, opened in routes:
        if opened and rank <= level and {start, target} != {a, b}:
            graph[a].append((b, fuel))
            graph[b].append((a, fuel))
    pending = [(0, 0, start, ())]
    visited = set()
    while pending:
        fuel, hops, current, legs = heappop(pending)
        if current in visited:
            continue
        if current == target:
            return legs
        visited.add(current)
        for destination, edge_cost in graph[current]:
            if destination not in visited:
                heappush(pending, (fuel + edge_cost, hops + 1, destination, (*legs, edge_cost)))
    return None


def route_itineraries(routes):
    for _, a, b, level, direct_fuel, opened in routes:
        alternate = fuel_path(routes, level, a, b)
        if alternate is None:
            continue
        direct = (direct_fuel,)
        return_legs = min((direct, alternate), key=lambda legs: (sum(legs), len(legs))) if opened else alternate
        survey_legs = alternate + return_legs[::-1]
        if opened:
            survey_legs += direct + return_legs[::-1]
        yield level, opened, survey_legs


def route_missions(routes, jump_seconds):
    missions = defaultdict(list)
    for level, opened, legs in route_itineraries(routes):
        missions[level, opened].append((sum(legs), len(legs) * jump_seconds[level] + 45))
    return missions


def refuel_stops(legs, capacity):
    """Minimum stops along a fixed itinerary with fuel available at each intermediate system."""
    remaining = capacity
    stops = 0
    for fuel in legs:
        if fuel > capacity:
            raise ValueError(f'Jump takes {fuel} fuel, tank capacity is {capacity}')
        if fuel > remaining:
            stops += 1
            remaining = capacity
        remaining -= fuel
    return stops


def liberation_cycles(sheets, level, ranks, shot_seconds, travel_seconds, sortie_expense, loot,
                      *, credits_per_kill=None, mission_bonus=0, spawn_interval=None, service_seconds=45):
    sortie_seconds = ceil(shot_seconds + travel_seconds + service_seconds)
    kill_times = tuple(ceil(shot_seconds * kill / len(ranks)) for kill in range(1, len(ranks) + 1))
    trials = []
    for faction in FACTIONS:
        interval, minimum, maximum = spawn_rows(sheets['EnemySpawnRuleConfig'], faction, level)
        if level == 10:
            interval = max(interval, 60)
        if spawn_interval is not None:
            interval = spawn_interval
        for capacity in range(max(1, minimum), maximum + 1):
            for phase in range(interval):
                kills = clearance_kills(capacity, interval, phase, sortie_seconds, kill_times)
                if kills is None:
                    raise ValueError(f'Level {level}: system cannot be liberated in this test.')
                receipts = .5 * sum(sum(loot[rank] for rank in ranks[:count]) for count in kills)
                payout = sortie_expense if credits_per_kill is None else credits_per_kill * kills[-1]
                net = receipts + payout * (1 + mission_bonus) - len(kills) * sortie_expense
                trials.append((net, sortie_seconds * len(kills), len(kills), kills[-1]))
    return trials


def reward(expense, minutes, net_rate, success=1):
    return ceil((expense + minutes * net_rate) / success)


def market_spread(corridors, market, fuel_price, target):
    periods, goods, limits = market

    def rates(spread):
        return sorted(max(one_cargo(origin, destination, periods, goods, limits, cycle, 2 * fuel * fuel_price,
                                    spread_basis_points=spread)
                          for origin, destination in ((a, b), (b, a)))
                      for _, _, a, b, cycle, fuel in corridors)

    exact = min(((abs(results[len(results) // 2] - target), spread, results)
                 for spread in range(0, 10001, 100) for results in (rates(spread),)), key=lambda row: row[:2])
    _, spread, results = exact
    return spread, results


def report_peaceful_market(markets, routes, fuel_price, revised, revised_first_level, recon_cost_rates):
    print('\n| Ур. | Мирная цель, кр./мин | Размах цены | Торговля мин. / медиана / макс. | Коридоры без прибыли | Доставка, кр. | Открытие, кр. | Улучшение, кр. | Маршруты выше разведки с +30% | Маршруты без допустимой выплаты при отклонении до 10% |')
    print('|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|')
    bonus_trials = []
    required_adjustments = defaultdict(list)
    for level, (corridors, market) in sorted(markets.items()):
        target = revised[level][0] if level in revised else revised_first_level
        spread, rates = market_spread(corridors, market, fuel_price, target)
        _, _, _, _, cycle, fuel = corridors[len(corridors) // 2]
        delivery = reward(fuel * fuel_price, cycle / 120, target)
        medians = []
        ranked_above = total = 0
        infeasible = 0
        for opened in (False, True):
            payouts = []
            for route_fuel, seconds in routes[level, opened]:
                expense = route_fuel * fuel_price
                payout = reward(expense, seconds / 60, target)
                payouts.append(payout)
                if level in revised:
                    total += 1
                    recon_with_bonus = revised[level][3]
                    ranked_above += (payout * 1.30 - expense) * 60 / seconds >= recon_with_bonus
                    minimum = reward(expense, seconds / 60, target * .90)
                    infeasible += (minimum * 1.30 - expense) * 60 / seconds >= recon_with_bonus
                    recon_net = revised[level][1]
                    recon_gross = recon_net + recon_cost_rates[level]
                    bonus_trials.append((level, recon_net, recon_gross, expense * 60 / seconds,
                                         payout * 60 / seconds, minimum * 60 / seconds))
                    recon_enhanced = recon_net + .10 * recon_gross
                    # Leave a 5% lead for reconnaissance after the common research bonus.
                    route_cap = recon_enhanced / 1.05
                    fuel_cap_per_minute = (route_cap - 1.10 * target) / .10
                    required_adjustments[level].append((route_fuel, seconds, fuel_cap_per_minute))
            medians.append(str(round(median(payouts))) if payouts else '—')
        fraction = f'{ranked_above}/{total}' if total else '—'
        conflict = f'{infeasible}/{total}' if total else '—'
        print(f'| {level} | {target:.0f} | {spread / 100:.0f}% | '
              f'{rates[0]:.0f} / {rates[len(rates) // 2]:.0f} / {rates[-1]:.0f} | '
              f'{rates.count(0)}/{len(rates)} | {delivery} | {" | ".join(medians)} | {fraction} | {conflict} |')

    print('\n| Максимальный бонус | Маршрутов не ниже разведки при паритете | То же при 90% мирной ставки | Мин. запас при паритете, кр./мин |')
    print('|---:|---:|---:|---:|')
    for bonus in (.01, .02, .03, .05, .06, .07, .08, .10, .15, .20, .30):
        gaps = [[recon + bonus * recon_gross - (gross[index] * (1 + bonus) - expense)
                 for _, recon, recon_gross, expense, *gross in bonus_trials] for index in (0, 1)]
        conflicts = [sum(gap <= 0 for gap in option) for option in gaps]
        print(f'| {bonus:.0%} | {conflicts[0]}/{len(bonus_trials)} | {conflicts[1]}/{len(bonus_trials)} | {min(gaps[0]):+.1f} |')
    for index in (0, 1):
        limit, level = min(((recon + expense - gross[index]) / (gross[index] - recon_gross), level)
                           for level, recon, recon_gross, expense, *gross in bonus_trials
                           if gross[index] > recon_gross)
        print(f'Порог равенства (вариант {index + 1}): {limit:.3%}, уровень {level}.')

    print('\n| Ур. | Топливо снизить на медиану / максимум | Либо добавить времени медиану / максимум, мин | Выплаты после пробного пересчёта топлива, кр. |')
    print('|---:|---|---|---|')
    for level, choices in sorted(required_adjustments.items()):
        target = revised[level][0]
        reductions = []
        extra_minutes = []
        payouts = []
        for original_fuel, seconds, fuel_cap in choices:
            assert fuel_cap > 0
            minutes = seconds / 60
            # Reserve one credit for rounding the full base payment upward.
            affordable_fuel = floor((fuel_cap * minutes - 11) / fuel_price)
            adjusted_fuel = min(original_fuel, max(0, affordable_fuel))
            expense = adjusted_fuel * fuel_price
            payout = reward(expense, minutes, target)
            recon = revised[level][1]
            recon_with_bonus = recon + .10 * (recon + recon_cost_rates[level])
            assert (payout * 1.10 - expense) / minutes * 1.05 <= recon_with_bonus
            reductions.append(1 - adjusted_fuel / original_fuel)
            extra_minutes.append(max(0, (original_fuel * fuel_price + 11) / fuel_cap - minutes))
            payouts.append(payout)
        print(f'| {level} | {median(reductions):.0%} / {max(reductions):.0%} | '
              f'{median(extra_minutes):.2f} / {max(extra_minutes):.2f} | {min(payouts)}–{max(payouts)} |')


def fuel_schedule(base):
    # Round each development level independently so low-cost routes can have flat steps.
    return [(base * (30 - step) + 15) // 30 for step in range(10)]


def trial_route_fuel(base, development):
    return (base * (136 - development) + 67) // 135


def trial_tanks(bases, level):
    if level == 10:
        maximum = 22 * trial_route_fuel(bases[level], 10) // 10
        return maximum - 2, maximum
    minimum = 2 * bases[level + 1]
    return minimum, minimum + 1


def frontier_recon_costs(routes, sector_by_system, frontier, bases, development, *, captured_behind=None):
    neighbors = defaultdict(list)
    for _, a, b, level, _, opened in routes:
        if opened and level <= frontier:
            fuel = trial_route_fuel(bases[level], development)
            neighbors[a].append((b, fuel))
            neighbors[b].append((a, fuel))
    allied = {system for system in neighbors if sector_by_system[system] < frontier and system != captured_behind}
    shortest = {}
    pending = [(0, system) for system in allied]
    # All initial priorities are zero, so order by system ID before using the heap.
    pending.sort()
    while pending:
        fuel, system = heappop(pending)
        if system in shortest:
            continue
        shortest[system] = fuel
        for destination, cost in neighbors[system]:
            if destination not in allied and destination not in shortest:
                heappush(pending, (fuel + cost, destination))
    return {system: 2 * fuel for system, fuel in shortest.items() if sector_by_system[system] == frontier}


def report_recon_range(sheets, routes, bases):
    sectors = {row['A']: int(number(linked_row(sheets['Сектора'], row['D']), 'E'))
               for row in sheets['Системы'] if row.get('A', '').startswith('s') and '&range=' in row.get('D', '')}
    print('\n| Сектор | Вражеских целей на графе | Топливо туда-обратно, развитие 1 / 10 | Достижимы с макс. баком прошлого / своего уровня, развитие 1 | То же при развитии 10 |')
    print('|---:|---:|---|---|---|')
    for level in range(2, 11):
        original = frontier_recon_costs(routes, sectors, level, bases, 1)
        developed = frontier_recon_costs(routes, sectors, level, bases, 10)
        assert original.keys() == developed.keys()
        assert len(original) == sum(sector == level for sector in sectors.values())
        tanks = [trial_tanks(bases, rank)[1] for rank in (level - 1, level)]
        feasible = [' / '.join(str(sum(cost <= capacity for cost in costs.values())) for capacity in tanks)
                    for costs in (original, developed)]
        print(f'| {level} | {len(original)} | {min(original.values())}–{max(original.values())} / '
              f'{min(developed.values())}–{max(developed.values())} | {feasible[0]} | {feasible[1]} |')
        if level == 10:
            print(f'Сектор 10, расходы на каждую цель: {sorted(original.values())} / {sorted(developed.values())}.')
        if level > 2:
            previous = (system for system, rank in sectors.items() if rank == level - 1)
            scenarios = [frontier_recon_costs(routes, sectors, level, bases, 1, captured_behind=system)
                         for system in previous]
            counts = [sum(cost <= tanks[0] for cost in scenario.values()) for scenario in scenarios]
            print(f'Если одна система предыдущего сектора ещё захвачена, бак прошлого уровня: {min(counts)}–{max(counts)} целей.')


def report_refueled_missions(routes, bases, tanks, price, revised, first_target, battles, jump_seconds):
    print('\nДозаправка по маршруту: 35 с на остановку, чувствительность 20–50 с.')
    print('| Сеть | Тип | Заданий | Дозаправок мин. / мед. / макс. | Время, медиана мин. | Выплата, медиана кр. | Сектор 10, медиана кр. |')
    print('|---|---|---:|---|---:|---:|---:|')
    for snapshot, candidate_routes in (
        ('Исходные маршруты, развитие 1', routes),
        ('Исходные маршруты, развитие 9', [
            (key, a, b, level, trial_route_fuel(fuel, 9) if opened else fuel, opened)
            for key, a, b, level, fuel, opened in routes
        ]),
    ):
        results = defaultdict(list)
        minimum_advantage = {delay: float('inf') for delay in (20, 35, 50)}
        discounted_gaps = []
        for level, opened, legs in route_itineraries(candidate_routes):
            stops = refuel_stops(legs, tanks[level])
            expense = sum(legs) * price
            base_seconds = len(legs) * jump_seconds[level] + 45
            target = revised[level][0] if level in revised else first_target
            seconds = base_seconds + 35 * stops
            results[opened].append((level, stops, seconds, reward(expense, seconds / 60, target)))
            if level not in revised:
                continue
            recon_minutes = (battles[level][4] + 75) / 60
            recon_cost = 2 * bases[level] * price + ceil(battles[level][2] * .2)
            recon_payout = reward(recon_cost, recon_minutes, revised[level][1], .9)
            recon_income = (.9 * (recon_payout + (recon_payout + 9) // 10) - recon_cost) / recon_minutes
            payout_with_stop = results[opened][-1][-1]
            discounted_route = (payout_with_stop + (payout_with_stop + 9) // 10 - ceil(expense / 2)) * 60 / seconds
            discounted_recon = recon_income + (bases[level] * price) / recon_minutes
            discounted_gaps.append(discounted_recon / discounted_route - 1)
            for delay in minimum_advantage:
                minutes = (base_seconds + delay * stops) / 60
                payout = reward(expense, minutes, target)
                income = (payout + (payout + 9) // 10 - expense) / minutes
                minimum_advantage[delay] = min(minimum_advantage[delay], recon_income / income - 1)
        for opened, rows in sorted(results.items()):
            stops = [count for _, count, _, _ in rows]
            late = [payout for level, _, _, payout in rows if level == 10]
            print(f'| {snapshot} | {"Улучшение" if opened else "Открытие"} | {len(rows)} | '
                  f'{min(stops)} / {median(stops):g} / {max(stops)} | '
                  f'{median(seconds for _, _, seconds, _ in rows) / 60:.2f} | '
                  f'{median(payout for _, _, _, payout in rows):.0f} | {median(late):.0f} |')
        assert min(minimum_advantage.values()) > .05
        print(f'{snapshot}: минимальное преимущество разведки с +10%: '
              + ', '.join(f'{delay} с → {margin:.1%}' for delay, margin in minimum_advantage.items()))
        print(f'{snapshot}: при скидке 50% на заправку и сохранении выплат маршрут не хуже разведки '
              f'в {sum(gap <= 0 for gap in discounted_gaps)} из {len(discounted_gaps)} случаев. '
              f'Минимальное преимущество разведки: {min(discounted_gaps):+.1%}.')


def report_fuel_and_tanks(sheets, ships, tick, items, costs, loot, battles, revised, first_target, jump_seconds):
    original_routes = list(route_catalog(sheets))
    print('\n| База сектора 10 | Цена топлива | Маршрутов не ниже разведки | Мин. преимущество разведки |')
    print('|---:|---:|---:|---:|')
    for last in (50, 75, 100):
        bases = {level: 12 + ((last - 12) * (level - 1) + 4) // 9 for level in range(1, 11)}
        routes = [(key, a, b, level, bases[level], opened) for key, a, b, level, _, opened in original_routes]
        surveys = route_missions(routes, jump_seconds)
        strict_price = None
        for price in range(1, 26):
            gaps = []
            for level, (target, recon_target, *_) in revised.items():
                _, _, repair, _, travel, *_ = battles[level]
                minutes = (travel + 75) / 60
                expense = 2 * bases[level] * price + ceil(repair * .2)
                payout = reward(expense, minutes, recon_target, .9)
                recon = (.9 * (payout + (payout + 9) // 10) - expense) / minutes
                for opened in (False, True):
                    for fuel, seconds in surveys[level, opened]:
                        expense = fuel * price
                        payout = reward(expense, seconds / 60, target)
                        income = (payout + (payout + 9) // 10 - expense) * 60 / seconds
                        gaps.append(recon / income - 1)
            if min(gaps) >= .05:
                strict_price = price
            if price in (2, 3, 4, 5, 25):
                print(f'| {last} | {price} | {sum(gap <= 0 for gap in gaps)}/{len(gaps)} | {min(gaps):+.1%} |')
        print(f'База {last}: максимальная целая цена с запасом 5% — {strict_price}.')
        if last == 75:
            selected = bases, strict_price, surveys, routes

    bases, price, surveys, routes = selected
    assert price is not None
    print(f'\nПробный вариант: база 75, цена {price}.')
    print('| Сектор | Базовый прыжок | Развитие 10 | Бак мин.–макс. | Базовых прыжков своим / в следующий сектор | Полная заправка мин.–макс., кр. | Торговля, медиана | Прибыль отхода | Заданий с обязательной заправкой даже с большим баком |')
    print('|---:|---:|---:|---|---|---|---:|---:|---:|')
    previous_tanks = 0, 0
    tanks = {}
    for level, corridors, market in trade_corridors(sheets, ships, tick, items, price, fuel_by_level=bases):
        base = bases[level]
        next_cost = bases.get(level + 1, base)
        development = [trial_route_fuel(base, rank) for rank in range(1, 11)]
        developed = development[-1]
        assert development[0] == base and all(a >= b for a, b in zip(development, development[1:]))
        tank_low, tank_high = trial_tanks(bases, level)
        if level == 10:
            assert tank_high * 10 == 22 * developed
        assert 2 <= tank_low // base <= tank_high // base <= 3
        assert tank_low // next_cost >= 2
        assert all(new > old for new, old in zip((tank_low, tank_high), previous_tanks))
        previous_tanks = tank_low, tank_high
        tanks[level] = tank_high
        target = revised[level][0] if level in revised else first_target
        _, rates = market_spread(corridors, market, price, target)
        net = '—'
        if level in costs:
            ranks, *_ = costs[level]
            net = f'{.5 * sum(loot[rank] for rank in ranks) - 2 * base * price - battles[level][2]:.0f}'
        mission_fuels = [fuel for opened in (False, True) for fuel, _ in surveys[level, opened]]
        refuel_required = sum(fuel > tank_high for fuel in mission_fuels)
        print(f'| {level} | {base} | {developed} | {tank_low}–{tank_high} | '
              f'{tank_low // base}–{tank_high // base} / {tank_low // next_cost}–{tank_high // next_cost} | '
              f'{tank_low * price}–{tank_high * price} | {rates[len(rates) // 2]:.0f} | {net} | '
              f'{refuel_required}/{len(mission_fuels)} |')
    report_refueled_missions(routes, bases, tanks, price, revised, first_target, battles, jump_seconds)
    report_recon_range(sheets, routes, bases)
    return bases, price


def report_route_fuel(sheets, ships, tick, items, hulls_price, costs, loot, battles, battle_seconds,
                      revised, first_target, jump_seconds):
    fuel_price = number(items['item_fuel_cells_01'], 'G')
    original_routes = list(route_catalog(sheets))
    levels = sorted(revised)
    candidates = []
    print('\n| Прибавка топлива за сектор | База 1 / 10 | Заданий вне запаса 5% | Минимальное преимущество разведки |')
    print('|---:|---|---:|---:|')
    for step in range(1, 6):
        bases = {level: 3 + (level - 1) * step for level in range(1, 11)}
        routes = [(key, a, b, level, bases[level], opened) for key, a, b, level, _, opened in original_routes]
        surveys = route_missions(routes, jump_seconds)
        gaps = []
        extra_seconds_by_level = defaultdict(list)
        for level in levels:
            target, recon_target, *_ = revised[level]
            _, _, repair, _, travel, *_ = battles[level]
            recon_minutes = (travel + 75) / 60
            recon_expense = 2 * bases[level] * fuel_price + ceil(repair * .2)
            recon_payout = reward(recon_expense, recon_minutes, recon_target, .9)
            recon = (.99 * recon_payout - recon_expense) / recon_minutes
            for opened in (False, True):
                for fuel, seconds in surveys[level, opened]:
                    expense = fuel * fuel_price
                    payout = reward(expense, seconds / 60, target)
                    income = (payout + (payout + 9) // 10 - expense) * 60 / seconds
                    gaps.append(recon / income - 1)
                    headroom = recon / 1.05 - 1.10 * target
                    assert headroom > 0
                    extra = 60 * (.10 * expense + 2.1) / headroom - seconds
                    extra_seconds_by_level[level].append(max(0, ceil(extra)))
        missed = sum(gap < .05 for gap in gaps)
        print(f'| {step} | {bases[1]} / {bases[10]} | {missed}/{len(gaps)} | {min(gaps):+.1%} |')
        if min(gaps) > 0:
            candidates.append((bases, extra_seconds_by_level))
    if not candidates:
        return
    bases, extra_seconds_by_level = candidates[-1]
    print(f'Покрытие RouteConfig: {len(original_routes)} маршрутов, '
          f'{sum(opened for *_, opened in original_routes)} открыты в начале.')
    hulls = records(sheets['HullConfig'])
    equipment = records(sheets['ShipEquipmentConfig'])
    print('\n| Ур. | Топливо по развитию 1–10 | Мин. бак своего уровня | Прыжков на полном баке | Маржа торговли | Торговля, медиана | Прибыль отхода, кр. | Освобождение +10%, кр./мин | До 5% запаса нужно ещё, с максимум |')
    print('|---:|---|---:|---:|---:|---:|---:|---:|---:|')
    for level, corridors, market in trade_corridors(sheets, ships, tick, items, fuel_price, fuel_by_level=bases):
        target = revised[level][0] if level in revised else first_target
        spread, rates = market_spread(corridors, market, fuel_price, target)
        tank = int(number(equipment[f'ship_tank_industrial_L{level:02}_01'], 'V'))
        fuel = bases[level]
        schedule = fuel_schedule(fuel)
        assert all(a >= b >= 1 for a, b in zip(schedule, schedule[1:]))
        net = '—'
        liberation = '—'
        if level in costs:
            ranks, *_ = costs[level]
            player_level = 1 if level == 2 else level
            hull = midpoint(hulls[f'hull_ally_ranger_L{player_level:02}_01'], 'N', 'O')
            expense = 2 * fuel * fuel_price + ceil(hull * .5) * hulls_price
            net = f'{.5 * sum(loot[rank] for rank in ranks) - expense:.0f}'
            capacities = []
            for faction in FACTIONS:
                _, low, high = spawn_rows(sheets['EnemySpawnRuleConfig'], faction, level)
                capacities.extend(range(low, high + 1))
            per_kill = ceil(expense / max(1, ceil(.30 * median(capacities))))
            trials = liberation_cycles(sheets, level, list(ranks), battle_seconds[level],
                                       battles[level][4], expense, loot, credits_per_kill=per_kill, mission_bonus=.10)
            liberation = f'{median(cash * 60 / elapsed for cash, elapsed, _, _ in trials):.0f}'
        print(f'| {level} | {", ".join(map(str, schedule))} | {tank} | {tank // fuel} | '
              f'{spread / 100:.0f}% | {rates[len(rates) // 2]:.0f} | {net} | {liberation} | '
              f'{max(extra_seconds_by_level.get(level, [0]))} |')


def victory_premium(trials, target):
    lower = 0
    upper = max(0, ceil(max(target * seconds / 60 - net for net, seconds, _, _ in trials)))
    while lower < upper:
        candidate = (lower + upper) // 2
        if median((net + candidate) * 60 / seconds for net, seconds, _, _ in trials) >= target:
            upper = candidate
        else:
            lower = candidate + 1
    return lower


def trophy_multiplier(trials, sortie_expense, defense_rate):
    for percentage in range(100, 301):
        scale = percentage / 100
        results = []
        for net, seconds, sorties, _ in trials:
            unreimbursed = (sorties - 1) * sortie_expense
            adjusted_net = scale * (net + unreimbursed) - unreimbursed
            results.append(adjusted_net * 60 / seconds)
        if median(results) > scale * defense_rate:
            return scale
    return None


def liberation_contribution(sheets, costs, battle_seconds, repair_routes, loot):
    print('\n| Сектор | Стартовый гарнизон | 30% гарнизона | Расчётное число личных целей | Кр. за цель | Доля сценариев с окупаемым финалом | Освобождение, кр./мин |')
    print('|---:|---:|---:|---:|---:|---:|---:|')
    rates = {}
    researched_rates = {}
    for level, (rank_range, _, expense) in costs.items():
        capacities = []
        for faction in FACTIONS:
            _, low, high = spawn_rows(sheets['EnemySpawnRuleConfig'], faction, level)
            capacities.extend(range(low, high + 1))
        minimum, maximum = min(capacities), max(capacities)
        personal_kills = max(1, ceil(.30 * median(capacities)))
        per_kill = ceil(expense / personal_kills)
        travel_seconds, *_ = max(repair_routes[level], key=lambda row: row[1])
        trials = liberation_cycles(sheets, level, list(rank_range), battle_seconds[level],
                                   travel_seconds, expense, loot, credits_per_kill=per_kill)
        covered = sum(final_kills * per_kill >= expense for _, _, _, final_kills in trials) / len(trials)
        rate = median(net * 60 / seconds for net, seconds, _, _ in trials)
        rates[level] = rate
        researched = liberation_cycles(sheets, level, list(rank_range), battle_seconds[level],
                                        travel_seconds, expense, loot, credits_per_kill=per_kill,
                                        mission_bonus=.30)
        researched_rates[level] = median(net * 60 / seconds for net, seconds, _, _ in researched)
        print(f'| {level} | {minimum}–{maximum} | {ceil(.30 * minimum)}–{ceil(.30 * maximum)} | '
              f'{personal_kills} | {per_kill} | {covered:.1%} | {rate:.0f} |')
    return rates, researched_rates


def report_updated_combat(sheets, ships, tick, costs, loot, battle_seconds, battles, defense_flows, revised, bases, price):
    print('\n| Сектор | Топливо + ремонт, кр. | Личных целей | Награда за цель, кр. | Прибыль отхода, кр. | Окупаемый сбор | Разведка / оборона / освобождение, кр./мин | С +10% |')
    print('|---:|---:|---:|---:|---:|---:|---|---|')
    trials_by_level = {}
    recon_by_level = {}
    for level, (ranks, _, _) in costs.items():
        _, _, repair, _, travel, *_ = battles[level]
        expense = 2 * bases[level] * price + repair
        capacities = []
        for faction in FACTIONS:
            _, low, high = spawn_rows(sheets['EnemySpawnRuleConfig'], faction, level)
            capacities.extend(range(low, high + 1))
        personal = ceil(.30 * median(capacities))
        per_kill = ceil(expense / personal)
        loot_total = sum(loot[rank] for rank in ranks)
        trials = liberation_cycles(sheets, level, list(ranks), battle_seconds[level], travel, expense, loot,
                                   credits_per_kill=per_kill)
        trials_by_level[level] = (trials, expense, per_kill)
        minutes = (travel + 75) / 60
        recon_expense = 2 * bases[level] * price + ceil(repair * .2)
        recon_payout = reward(recon_expense, minutes, revised[level][1], .9)
        mission_net, trophies, payout = defense_flows[level][:3]
        rates = []
        recon_by_level[level] = []
        for bonus in (0, .10):
            recon = (.9 * recon_payout * (1 + bonus) - recon_expense) / minutes
            recon_by_level[level].append(recon)
            defense = mission_net + trophies + bonus * payout
            liberation = median((cash + bonus * kills * per_kill) * 60 / elapsed
                                for cash, elapsed, _, kills in trials)
            rates.append(f'{recon:.0f} / {defense:.0f} / {liberation:.0f}')
        print(f'| {level} | {expense:.0f} | {personal} | {per_kill} | {.5 * loot_total - expense:.0f} | '
              f'{expense / loot_total:.1%} | {rates[0]} | {rates[1]} |')

    print('\n| Множитель выкупа | Уровни с нарушением порядка в базовом режиме / с бонусом | Уровни с убыточным отходом | Спады освобождения между соседними уровнями без бонуса |')
    print('|---:|---|---|---|')
    for scale in (.7, .8, .9, 1):
        conflicts = [[], []]
        losses = []
        drops = []
        previous = 0
        for level, (trials, expense, per_kill) in trials_by_level.items():
            ranks = costs[level][0]
            mission_net, trophies, payout = defense_flows[level][:3]
            if .5 * scale * sum(loot[rank] for rank in ranks) <= expense:
                losses.append(level)
            for index, bonus in enumerate((0, .10)):
                defense = mission_net + scale * trophies + bonus * payout
                liberation = median((scale * (cash + sorties * expense - kills * per_kill)
                                     - sorties * expense + (1 + bonus) * kills * per_kill) * 60 / elapsed
                                    for cash, elapsed, sorties, kills in trials)
                if not recon_by_level[level][index] < defense < liberation:
                    conflicts[index].append(level)
                if index == 0:
                    if liberation < previous:
                        drops.append(level)
                    previous = liberation
        format_levels = lambda levels: ', '.join(map(str, levels)) or '—'
        print(f'| {scale:.0%} | {format_levels(conflicts[0])} / {format_levels(conflicts[1])} | '
              f'{format_levels(losses)} | {format_levels(drops)} |')

    # Check if the income dip can be removed by tuning regular reinforcement alone.
    def rates(trials, per_kill):
        return tuple(median((cash + bonus * kills * per_kill) * 60 / elapsed
                            for cash, elapsed, _, kills in trials) for bonus in (0, .10))

    previous_trials, _, previous_payout = trials_by_level[3]
    following_trials, _, following_payout = trials_by_level[5]
    lower = rates(previous_trials, previous_payout)
    upper = rates(following_trials, following_payout)
    _, expense, per_kill = trials_by_level[4]
    print('\n| Сектор 4: интервал пополнения, с | Освобождение без бонуса / с +10%, кр./мин | Рост от сектора 3 до 5 |')
    print('|---:|---|---|')
    for interval in (30, 45, 60, 90, 120, 135, 150, 180, 210):
        try:
            trials = liberation_cycles(sheets, 4, list(costs[4][0]), battle_seconds[4], battles[4][4],
                                       expense, loot, credits_per_kill=per_kill, spawn_interval=interval)
        except ValueError:
            print(f'| {interval} | — | Нет: не все состояния освобождаются |')
            continue
        incomes = rates(trials, per_kill)
        feasible = all(low < income < high for low, income, high in zip(lower, incomes, upper))
        print(f'| {interval} | {incomes[0]:.0f} / {incomes[1]:.0f} | {"да" if feasible else "нет"} |')
    for saved in range(1, 21):
        trials = liberation_cycles(sheets, 4, list(costs[4][0]), battle_seconds[4], battles[4][4],
                                   expense, loot, credits_per_kill=per_kill, service_seconds=45 - saved)
        incomes = rates(trials, per_kill)
        if all(low < income < high for low, income, high in zip(lower, incomes, upper)):
            print(f'Сектор 4: сократить подготовку и обслуживание на {saved} с за вылет; '
                  f'доходность {incomes[0]:.0f} / {incomes[1]:.0f} кр./мин.')
            break

    report_sector_four_routes(sheets, ships, tick, costs, loot, battle_seconds, expense, per_kill, lower, upper)


def report_sector_four_routes(sheets, ships, tick, costs, loot, battle_seconds, expense, per_kill, lower, upper):
    key = 'ally_ranger_L04_01'
    baseline = repair_route_scenarios(sheets, ships, tick)[4]
    benchmark = max(baseline, key=lambda row: row[1])
    benchmark_route = benchmark[2:]
    print('\n| Скорость двигателя | Рейс на ремонт, общий диапазон, с | Контрольное направление, с |')
    print('|---:|---|---:|')
    for speed in (230, 240, 270, 300, 345, 460):
        adjusted = dict(ships, **{key: replace(ships[key], speed=speed)})
        routes = repair_route_scenarios(sheets, adjusted, tick)[4]
        durations = [row[0] for row in routes]
        reference_seconds = next(row[0] for row in routes if row[2:] == benchmark_route)
        print(f'| {speed} | {min(durations):g}–{max(durations):g} | {reference_seconds:g} |')
    print(f'Контрольное направление: {benchmark_route[0]} → {benchmark_route[1]}.')

    print('\n| Рейс на ремонт, с | Направлений при типовом двигателе | Доход с обслуживанием 45 с | Доход с обслуживанием 39 с |')
    print('|---:|---:|---|---|')
    timings = [row[0] for row in baseline]
    pooled = {45: [], 39: []}
    for travel in sorted(set(timings)):
        incomes = []
        for service in (45, 39):
            trials = liberation_cycles(sheets, 4, list(costs[4][0]), battle_seconds[4], travel,
                                       expense, loot, credits_per_kill=per_kill, service_seconds=service)
            pooled[service].extend(trials * timings.count(travel))
            rates = tuple(median((cash + bonus * kills * per_kill) * 60 / elapsed
                                 for cash, elapsed, _, kills in trials) for bonus in (0, .10))
            feasible = all(low < rate < high for low, rate, high in zip(lower, rates, upper))
            incomes.append(f'{rates[0]:.0f} / {rates[1]:.0f} ({"да" if feasible else "нет"})')
        print(f'| {travel:g} | {timings.count(travel)} | {incomes[0]} | {incomes[1]} |')
    for service, trials in pooled.items():
        rates = tuple(median((cash + bonus * kills * per_kill) * 60 / elapsed
                             for cash, elapsed, _, kills in trials) for bonus in (0, .10))
        print(f'Все 9 направлений с равным весом, обслуживание {service} с: '
              f'{rates[0]:.0f} / {rates[1]:.0f} кр./мин.')


def report_equipment_repair(sheets, costs, loot, battles, bases, price, battle_seconds, defense_flows, revised):
    prices_path = Path(__file__).resolve().parents[3] / 'Assets/_Project/Content/Configs/Market/Items'
    slots = ('engine', 'fuel_tank', 'radar', 'scanner', 'repair_droid', 'grabber', 'shield_generator')
    unit_prices = {}
    for name in ('weapon', *slots):
        asset = (prices_path / f'item_repair_{name}_01.asset').read_text()
        unit_prices[name] = int(re.search(r'^  basePrice: (\d+)$', asset, re.M).group(1))

    hulls = records(sheets['HullConfig'])
    equipment = records(sheets['ShipEquipmentConfig'])
    allies = records(sheets['AllyConfig'])
    groups = group_members(sheets['EquipmentGroupConfig'], 'Внутренние группы по типам / Варианты оборудования', 'D')
    def loadout_repair(ship_level):
        profile = f'ally_ranger_L{ship_level:02}_01'
        hull = hulls[f'hull_{profile}']
        shield = max(int(number(equipment[key], 'AI')) for key in groups[allies[profile]['H']]
                     if equipment[key]['N'].startswith('6 ·'))
        assert hull['X'] == 'Да'
        wear = int(number(hull, 'M')) * unit_prices['weapon']
        wear += sum(unit_prices[name] for column, name in zip('RSTUVWX', slots) if hull[column] == 'Да')
        return shield, wear

    print('\n| Сектор | Щит: макс. очков | Тарифный ремонт 1 ед. каждого предмета | Полные расходы при цене щита 1 / 2 / 3 | Прибыль отхода при цене 1 / 2 / 3 | Награда за цель при цене 1 |')
    print('|---:|---:|---:|---|---|---:|')
    installments = {}
    for level, (ranks, _, _) in costs.items():
        shield, wear = loadout_repair(1 if level == 2 else level)
        base = int(2 * bases[level] * price + battles[level][2])
        full_costs = [base + wear + shield * shield_price for shield_price in (1, 2, 3)]
        receipts = .5 * sum(loot[rank] for rank in ranks)
        profits = [round(receipts - cost) for cost in full_costs]
        capacities = [count for faction in FACTIONS
                      for _, minimum, maximum in (spawn_rows(sheets['EnemySpawnRuleConfig'], faction, level),)
                      for count in range(minimum, maximum + 1)]
        payout = ceil(full_costs[0] / ceil(.30 * median(capacities)))
        installments[level] = (full_costs[0], payout)
        print(f'| {level} | {shield} | {wear} | {" / ".join(map(str, full_costs))} | '
              f'{" / ".join(map(str, profits))} | {payout} |')

    print('\n| Сектор | Оборона / освобождение без бонуса | Оборона / освобождение с +10% | Порядок с разведкой |')
    print('|---:|---|---|---|')
    groups_by_level = invasion_groups(sheets)
    hull_price = next(number(row, 'G') for row in sheets['ItemConfig'] if row.get('A') == 'item_repair_01')
    repaired_cycles = {}
    for level, (expense, per_kill) in installments.items():
        # Defense uses a same-level ship, unlike the first liberation sortie in sector 2.
        extra = sum(loadout_repair(level))
        repair_budget = ceil(midpoint(hulls[f'hull_ally_ranger_L{level:02}_01'], 'N', 'O') * .5) * hull_price
        invaders = [sum(count for _, count in group) for group in groups_by_level[level]]
        defense_payout = ceil((repair_budget + extra) / ceil(.30 * median(invaders)))
        mission_net, trophy_rate, payout_rate, cycles_per_minute, previous_payout = defense_flows[level]
        adjusted_payout_rate = payout_rate * defense_payout / previous_payout
        defense_net = mission_net + trophy_rate + adjusted_payout_rate - payout_rate - extra * cycles_per_minute
        travel = battles[level][4]
        recon_minutes = (travel + 75) / 60
        recon_expense = 2 * bases[level] * price + ceil(battles[level][2] * .2)
        recon_payout = reward(recon_expense, recon_minutes, revised[level][1], .9)
        trials = liberation_cycles(sheets, level, list(costs[level][0]), battle_seconds[level], travel, expense,
                                   loot, credits_per_kill=per_kill, service_seconds=39 if level == 4 else 45)
        numbers = []
        thresholds = []
        for bonus in (0, .10):
            defense = defense_net + bonus * adjusted_payout_rate
            liberation = median((cash + bonus * kills * per_kill) * 60 / elapsed
                                for cash, elapsed, _, kills in trials)
            recon = (.9 * recon_payout * (1 + bonus) - recon_expense) / recon_minutes
            numbers.append((f'{defense:.0f} / {liberation:.0f}', recon < defense < liberation))
            thresholds.append((recon, defense))
        repaired_cycles[level] = (expense, per_kill, trials, thresholds, trophy_rate)
        print(f'| {level} | {numbers[0][0]} | {numbers[1][0]} | {"да" if all(passed for _, passed in numbers) else "нет"} |')

    report_liberation_trophy_calibration(costs, loot, repaired_cycles, groups_by_level)
    report_liberation_service_calibration(sheets, costs, loot, battles, battle_seconds, repaired_cycles)


def liberation_rates(trials, payout):
    return tuple(median((cash + bonus * kills * payout) * 60 / elapsed
                        for cash, elapsed, _, kills in trials) for bonus in (0, .10))


def liberation_weighted_rates(trials, payout):
    elapsed = sum(seconds for _, seconds, _, _ in trials)
    return tuple(sum(cash + bonus * kills * payout for cash, _, _, kills in trials) * 60 / elapsed
                 for bonus in (0, .10))


def report_liberation_service_calibration(sheets, costs, loot, battles, battle_seconds, cycles):
    baseline = {3: 45, 4: 39, 5: 45}
    choices = {}
    weighted = {}
    for level, original in baseline.items():
        expense, payout, _, thresholds, _ = cycles[level]
        choices[level] = []
        weighted[level] = {}
        for service in range(30, 61):
            trials = liberation_cycles(sheets, level, list(costs[level][0]), battle_seconds[level],
                                       battles[level][4], expense, loot, credits_per_kill=payout,
                                       service_seconds=service)
            rates = liberation_rates(trials, payout)
            weighted[level][service] = liberation_weighted_rates(trials, payout)
            if all(rate > defense for rate, (_, defense) in zip(rates, thresholds)):
                choices[level].append((abs(service - original), service, rates))

    lower = liberation_rates(cycles[2][2], cycles[2][1])
    upper = liberation_rates(cycles[6][2], cycles[6][1])
    feasible = []
    for combination in product(*choices.values()):
        sequences = list(zip(lower, *(row[2] for row in combination), upper))
        if all(all(a < b for a, b in zip(sequence, sequence[1:])) for sequence in sequences):
            feasible.append(combination)
    print(f'\nСектор 3–5: {len(feasible)} сочетаний времени обслуживания сохраняют рост в обоих режимах.')
    if feasible:
        best = min(feasible, key=lambda rows: (sum(row[0] for row in rows), max(row[0] for row in rows), rows))
        print('| Сектор | Обслуживание было → стало, с | Освобождение без бонуса / с +10%, кр./мин |')
        print('|---:|---|---|')
        for (level, original), (_, service, rates) in zip(baseline.items(), best):
            print(f'| {level} | {original} → {service} | {rates[0]:.0f} / {rates[1]:.0f} |')
        lookup = {level: {row[1]: row[2] for row in options} for level, options in choices.items()}
        print('\n| Независимый разброс обслуживания, с | Устойчивых сочетаний из номинально годных |')
        print('|---:|---:|')
        for tolerance in (1, 2, 3):
            stable = 0
            for combination in feasible:
                rate_windows = [[lower], *[
                    [lookup[level][second] for second in range(row[1] - tolerance, row[1] + tolerance + 1)
                     if second in lookup[level]]
                    for level, row in zip(baseline, combination)
                ], [upper]]
                if any(len(window) != 2 * tolerance + 1 for window in rate_windows[1:-1]):
                    continue
                if all(max(pair[mode] for pair in left) < min(pair[mode] for pair in right)
                       for left, right in zip(rate_windows, rate_windows[1:]) for mode in (0, 1)):
                    stable += 1
            print(f'| ±{tolerance} | {stable}/{len(feasible)} |')

        print('\n| Сектор | Кредитов за минуту суммарного времени без бонуса / с +10% |')
        print('|---:|---|')
        selected_seconds = {level: option[1] for level, option in zip(baseline, best)}
        comparison = {}
        for level, (expense, payout, trials, _, _) in cycles.items():
            rates = weighted[level][selected_seconds[level]] if level in selected_seconds else liberation_weighted_rates(trials, payout)
            comparison[level] = rates
            print(f'| {level} | {rates[0]:.0f} / {rates[1]:.0f} |')
        for tolerance in (1, 2, 3):
            windows = [[comparison[2]], *[
                [weighted[level][time] for time in range(selected - tolerance, selected + tolerance + 1)]
                for level, selected in selected_seconds.items()
            ], [comparison[6]]]
            stable = all(max(pair[mode] for pair in left) < min(pair[mode] for pair in right)
                         for left, right in zip(windows, windows[1:]) for mode in (0, 1))
            print(f'Суммарный темп, сектора 2–6, ±{tolerance} с: {"устойчив" if stable else "неустойчив"}.')
        expense, payout, _, _, _ = cycles[8]
        for service in range(45, 29, -1):
            trials = liberation_cycles(sheets, 8, list(costs[8][0]), battle_seconds[8], battles[8][4],
                                       expense, loot, credits_per_kill=payout, service_seconds=service)
            rates = liberation_weighted_rates(trials, payout)
            if all(low < rate < high for low, rate, high in zip(comparison[7], rates, comparison[9])):
                print(f'Суммарный темп, сектор 8: {service} с обслуживания, {rates[0]:.0f} / {rates[1]:.0f} кр./мин.')
                break


def report_liberation_trophy_calibration(costs, loot, cycles, invasion_compositions):
    """Adjust mid-level trophy value, keeping the other calibration inputs fixed."""
    adjustable = (2, 3, 4, 5)
    # The median depends only on sortie length, count and final kills. Keep their frequencies.
    grouped_cycles = {}
    invasion_ranks = {}
    for level, (_, _, trials, _, _) in cycles.items():
        frequencies = defaultdict(int)
        for trial in trials:
            frequencies[trial] += 1
        grouped_cycles[level] = frequencies
        invasion_ranks[level] = {int(enemy.split('_')[-2][1:]) for group in invasion_compositions[level]
                                 for enemy, _ in group}

    def evaluate(candidate):
        previous = (0, 0)
        result = {}
        for level, (expense, rate, _, thresholds, defense_trophies) in cycles.items():
            ranks = list(costs[level][0])
            proceeds = .5 * sum(candidate[rank] for rank in ranks)
            if proceeds < 1.2 * expense:
                return None
            changes = [.5 * (candidate[rank] - loot[rank]) for rank in ranks]
            frequencies = grouped_cycles[level]
            bounds = [candidate[rank] / loot[rank] for rank in invasion_ranks[level]]
            incomes = []
            for index, bonus in enumerate((0, .10)):
                values = []
                for (cash, elapsed, sorties, kills), frequency in frequencies.items():
                    change = (sorties - 1) * sum(changes) + sum(changes[:kills])
                    income = (cash + change + bonus * kills * rate) * 60 / elapsed
                    values.extend([income] * frequency)
                income = median(values)
                recon, defense = thresholds[index]
                low = defense + (min(bounds) - 1) * defense_trophies
                high = defense + (max(bounds) - 1) * defense_trophies
                if income <= previous[index] or not recon < low <= high < income:
                    return None
                incomes.append(income)
            previous = incomes
            result[level] = (proceeds - expense, *incomes)
        return result

    winner = None
    for changes in product(range(-400, 401, 50), repeat=len(adjustable)):
        magnitude = sum(abs(change) for change in changes)
        if winner and magnitude >= winner[0]:
            continue
        candidate = dict(loot)
        for rank, change in zip(adjustable, changes):
            candidate[rank] += change
        if any(candidate[rank] >= candidate[rank + 1] for rank in range(1, 10)):
            continue
        result = evaluate(candidate)
        if result:
            winner = magnitude, candidate, result

    if winner is None:
        print('Локальный подбор стоимости трофеев: в проверенной сетке решения нет.')
        return
    _, candidate, result = winner
    print('\nСтупени выкупа после перекалибровки: '
          + ', '.join(f'{rank}: {loot[rank]} → {candidate[rank]}' for rank in adjustable))
    print('| Сектор | Отход, кр. | Освобождение без бонуса / с +10%, кр./мин |')
    print('|---:|---:|---|')
    for level, (retreat, base, enhanced) in result.items():
        print(f'| {level} | {retreat:.0f} | {base:.0f} / {enhanced:.0f} |')


def main():
    sheets = read_sheets((*SHEETS, *EXTRA_SHEETS))
    ships = build_ships(sheets)
    tick = next(number(row, 'D') for row in sheets['GameTimeConfig'] if row.get('B') == 'secondsPerGameTick')
    items = {row['A']: row for row in sheets['ItemConfig'] if row.get('M', '').endswith('.asset')}
    fuel_price = number(items['item_fuel_cells_01'], 'G')
    repair_price = number(items['item_repair_01'], 'G')
    costs = liberation_costs(sheets, fuel_price, repair_price)
    loot = fit_trophy_curve(costs)
    scenarios = {(level, faction): (ships[f'ally_ranger_L{level:02}_01'], reference_trios(ships, level, faction))
                 for level in range(1, 11) for faction in FACTIONS}
    battle_seconds = longer_battles(ships, scenarios, tick, display=False)
    repair_routes = repair_route_scenarios(sheets, ships, tick)
    markets = {level: (rows, market) for level, rows, market in trade_corridors(sheets, ships, tick, items, fuel_price)}
    corridors = {level: rows[len(rows) // 2] for level, (rows, _) in markets.items()}
    jump_seconds = {level: median(row[2] for row in rows) / 2
                    for level, rows in available_corridors(sheets, ships, tick).items()}
    routes = route_missions(list(route_catalog(sheets)), jump_seconds)
    battles = {}
    limits = {}
    for level, (rank_range, _, expense) in costs.items():
        ranks = list(rank_range)
        travel_seconds, fuel, *_ = max(repair_routes[level], key=lambda row: row[1])
        repair = expense - fuel * fuel_price
        # Defense starts and finishes locally, so this scenario needs no intersystem jump.
        defense_rate = .2 * sum(loot[rank] for rank in ranks) * 60 / (battle_seconds[level] + 45)
        clearances = liberation_cycles(sheets, level, ranks, battle_seconds[level], travel_seconds, expense, loot)
        liberation_rate = median(net * 60 / seconds for net, seconds, _, _ in clearances)
        # Solve for the baseline using two 20% income gaps, with liberation above defense.
        limits[level] = min(defense_rate, liberation_rate / 1.2) / 1.2**2
        battles[level] = (defense_rate, liberation_rate, repair, fuel, travel_seconds, expense, clearances)

    targets = {}
    for level in range(10, 0, -1):
        ceiling = min(corridors[level][0], limits.get(level, float('inf')))
        targets[level] = min(ceiling, targets.get(level + 1, float('inf')) / 1.05)

    print('| Сектор | Торговля исходная | Цель доставка / маршрут | Цель разведка | Оборона по трофеям | Освобождение по всем вылетам |')
    print('|---:|---:|---:|---:|---:|---:|')
    for level, target in sorted(targets.items()):
        combat = '— | — | —'
        if level in battles:
            defense, liberation, *_ = battles[level]
            # A shorter defense scenario could beat liberation. Keep that conflict visible.
            assert target < target * 1.2 < defense
            combat = f'{target * 1.2:.0f} | {defense:.0f} | {liberation:.0f}'
        print(f'| {level} | {corridors[level][0]:.0f} | {target:.0f} | {combat} |')

    print('\n| Сектор | Доставка | Открытие | Улучшение | Разведка (90%) | Оборона за цель | Освобождение за 1 / 2 / 3 цели |')
    print('|---:|---:|---:|---:|---:|---:|---:|')
    recon_gross_rates = {}
    recon_cost_rates = {}
    delivery_gross_rates = {}
    for level, target in sorted(targets.items()):
        _, _, _, _, round_trip, per_leg_fuel = corridors[level]
        delivery = reward(per_leg_fuel * fuel_price, round_trip / 120, target)
        delivery_gross_rates[level] = delivery / (round_trip / 120)
        route_rewards = []
        for opened in (False, True):
            rewards = [reward(fuel * fuel_price, seconds / 60, target) for fuel, seconds in routes[level, opened]]
            route_rewards.append(f'{median(rewards):.0f}' if rewards else '—')
        combat = '— | — | —'
        if level in battles:
            defense, liberation, repair, fuel, travel, expense, clearances = battles[level]
            recon_minutes = (travel + 45 + 30) / 60
            recon_cost = fuel * fuel_price + ceil(repair * .2)
            recon = reward(recon_cost, recon_minutes, target * 1.2, .9)
            recon_gross_rates[level] = .9 * recon / recon_minutes
            recon_cost_rates[level] = recon_cost / recon_minutes
            maximum_kills = len(costs[level][0])
            coefficients = ' / '.join(str(ceil(expense / kills)) for kills in range(1, maximum_kills + 1))
            combat = f'{recon} | {ceil(repair / maximum_kills)} | {coefficients}'
        print(f'| {level} | {delivery} | {" | ".join(route_rewards)} | {combat} |')

    print('\n| Сектор | Вылетов, медиана | Личных целей в финале | Добавка к времени обороны для паритета, с | Премия победы для превышения обороны на 20%, кр. | Множитель выкупа для превосходства |')
    print('|---:|---:|---|---:|---:|---:|')
    for level, (defense, liberation, *_, expense, trials) in battles.items():
        extra_defense_time = ceil(max(0, (defense / liberation - 1) * (battle_seconds[level] + 45)))
        premium = victory_premium(trials, defense * 1.2)
        scale = trophy_multiplier(trials, expense, defense)
        scale_text = f'{scale:.2f}' if scale is not None else '>3.00'
        print(f'| {level} | {median(count for _, _, count, _ in trials):g} | '
              f'{", ".join(map(str, sorted({kills for _, _, _, kills in trials})))} | {extra_defense_time} | {premium} | {scale_text} |')

    liberation_rates, researched_liberation = liberation_contribution(sheets, costs, battle_seconds, repair_routes, loot)
    recon_rates = {level: target * 1.2 for level, target in targets.items() if level in liberation_rates}
    defense_rates, defense_flows = report_defense(sheets, ships, tick, repair_price, loot, recon_rates, liberation_rates)
    print('\n| Ур. | Разведка +30% | Оборона +30% | Освобождение +30% | Запас разведка → оборона | Предел базового чистого дохода разведки | Цель доставки без бонуса |')
    print('|---:|---:|---:|---:|---:|---:|---:|')
    for level in sorted(recon_rates):
        enhanced_recon = 1.30 * recon_gross_rates[level] - recon_cost_rates[level]
        defense = defense_rates[level]
        liberation = researched_liberation[level]
        recon_ceiling = (defense - .30 * recon_cost_rates[level]) / 1.30
        enhanced_delivery = targets[level] + .30 * delivery_gross_rates[level]
        print(f'| {level} | {enhanced_recon:.0f} | {defense:.0f} | {liberation:.0f} | '
              f'{defense - enhanced_recon:+.0f} | {recon_ceiling:.0f} | {targets[level]:.0f} |')
        if enhanced_delivery >= defense:
            print(f'Доставка с +30% на уровне {level}: {enhanced_delivery:.0f} кр./мин — выше обороны.')

    print('\n| Ур. | Мирный ориентир до / после, кр./мин | Разведка до / после, кр./мин | Доставка / разведка / оборона с +30%, кр./мин |')
    print('|---:|---|---|---|')
    revised = {}
    next_target = float('inf')
    for level in sorted(recon_rates, reverse=True):
        defense = defense_rates[level]
        delivery_cost = delivery_gross_rates[level] - targets[level]
        recon_cost = recon_cost_rates[level]
        # Keep at least 5% headroom in both researched gaps, with reconnaissance 20% above the base peaceful rate.
        recon_limit = (defense / 1.05 - .30 * recon_cost) / 1.56
        delivery_limit = (defense / 1.05**2 - .30 * delivery_cost) / 1.30
        target = min(targets[level], next_target / 1.05, recon_limit, delivery_limit)
        next_target = target
        cap = defense / 1.05
        enhanced_delivery = 1.30 * target + .30 * delivery_cost
        enhanced_recon = max(1.56 * target + .30 * recon_cost, 1.05 * enhanced_delivery)
        assert 0 < enhanced_delivery < enhanced_recon <= cap + 1e-8
        revised[level] = (target, (enhanced_recon - .30 * recon_cost) / 1.30, enhanced_delivery, enhanced_recon, defense)
    revised_first_level = min(targets[1], revised[2][0] / 1.05)
    print(f'| 1 | {targets[1]:.0f} / {revised_first_level:.0f} | — | — |')
    for level, (target, recon, enhanced_delivery, enhanced_recon, defense) in sorted(revised.items()):
        assert target < recon < defense
        print(f'| {level} | {targets[level]:.0f} / {target:.0f} | {recon_rates[level]:.0f} / {recon:.0f} | '
              f'{enhanced_delivery:.0f} / {enhanced_recon:.0f} / {defense:.0f} |')

    report_peaceful_market(markets, routes, fuel_price, revised, revised_first_level, recon_cost_rates)
    report_route_fuel(sheets, ships, tick, items, repair_price, costs, loot, battles, battle_seconds, revised,
                      revised_first_level, jump_seconds)
    bases, price = report_fuel_and_tanks(sheets, ships, tick, items, costs, loot, battles, revised,
                                         revised_first_level, jump_seconds)
    report_updated_combat(sheets, ships, tick, costs, loot, battle_seconds, battles, defense_flows, revised, bases, price)
    report_equipment_repair(sheets, costs, loot, battles, bases, price, battle_seconds, defense_flows, revised)


if __name__ == '__main__':
    main()
