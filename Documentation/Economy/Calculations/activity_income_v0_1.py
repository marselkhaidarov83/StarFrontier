"""Compare route-based delivery, replenishable trade and trial combat income."""

from collections import defaultdict
from dataclasses import dataclass
from heapq import heappop, heappush
from math import ceil, lcm

from combat_balance_v0_1 import (
    FACTIONS, SHEETS, build_ships, linked_row, longer_battles, midpoint, number,
    read_sheets, records, reference_trios, repair_route_scenarios, route_geometry,
)
from early_credit_balance_v0_1 import fit_trophy_curve, liberation_costs
from progression_economy_v0_1 import EXTRA_SHEETS, quote, settings


@dataclass(frozen=True)
class Commodity:
    change: int
    capacity: int
    basic: int
    maximum: int


# Baseline sector expenditures from SF_CAMPAIGN_BUDGET_V0_1, including maps.
BASE_BUDGET = (1000, 9060, 20715, 32095, 47850, 64980, 86420, 109275, 136145, 164680)
MAP_COST = (0, 0, 2600, 2900, 3300, 3800, 4200, 4700, 5000, 5500)
RESEARCH_BUDGET = 100500
ACTIVE_HOURS = 150


def profile_data(rows):
    periods = {}
    goods = defaultdict(dict)
    for row in rows:
        if not row.get('A', '').startswith('mMarketProfile_') or not row.get('C', '').isdigit():
            continue
        key = (row['A'], int(row['C']))
        if row.get('E', '').startswith('item_'):
            goods[key][row['E']] = Commodity(*(int(number(row, column)) for column in 'GHIJ'))
        elif row.get('D', '').isdigit() and 'J' in row and row.get('E', '').isdigit():
            periods[key] = int(number(row, 'D'))
    return periods, goods


def one_cargo(origin, destination, periods, goods, limits, round_trip, fuel_bill, *, wallet=None,
              spread_basis_points=10000):
    """Best repeatable producer-to-consumer batch, with an empty return."""
    period_a, period_b = periods[origin], periods[destination]
    if period_a <= 0 or period_b <= 0:
        raise ValueError('Invalid market interval')
    window = lcm(period_a, period_b)
    available = goods[origin]
    wanted = goods[destination]
    best_rate = 0.0
    for sku in available.keys() & wanted.keys() & limits.keys():
        a, b = available[sku], wanted[sku]
        if a.change <= 0 or b.change >= 0 or a.capacity <= 0 or b.capacity <= 0:
            continue
        supply = a.change * (window // period_a)
        demand = -b.change * (window // period_b)
        a_max = a.basic + ((a.maximum - a.basic) * spread_basis_points + 5000) // 10000
        b_max = b.basic + ((b.maximum - b.basic) * spread_basis_points + 5000) // 10000
        margin = -fuel_bill
        purchase = 0
        for units in range(1, min(limits[sku], a.capacity, b.capacity) + 1):
            purchase_unit = quote(a.capacity + 1 - units, a.capacity, a.basic, a_max)
            purchase += purchase_unit
            if wallet is not None and purchase + fuel_bill > wallet:
                break
            margin += quote(units, b.capacity, b.basic, b_max)
            margin -= purchase_unit
            cycle = window * max(ceil(round_trip / window), ceil(units / supply), ceil(units / demand))
            rate = margin * 60 / cycle
            best_rate = max(best_rate, rate)
    return best_rate


def planetary_profiles(sheets):
    profiles = {}
    for planet in sheets['Планеты']:
        if not planet.get('D', '').startswith('#gid='):
            continue
        kind = linked_row(sheets['Тип планеты'], planet['F'])
        if kind.get('E') == 'Да' and kind.get('N'):
            system = linked_row(sheets['Системы'], planet['D'])
            profiles[system['A'], planet['B']] = kind['N']
    return profiles


def available_corridors(sheets, ships, tick):
    profiles = planetary_profiles(sheets)
    result = defaultdict(list)
    for route in sheets['Маршруты']:
        if route.get('G') != 'Нет' or not route.get('A', '').startswith('route_'):
            continue
        endpoints = [linked_row(sheets['Системы'], route[column]) for column in 'EF']
        ranks = [int(number(linked_row(sheets['Сектора'], system['D']), 'E')) for system in endpoints]
        level = max(ranks)
        engine = ships[f'ally_ranger_L{level:02}_01'].speed
        legs = [route_geometry(sheets, route, engine, reverse=reverse) for reverse in (False, True)]
        if any(leg is None for leg in legs):
            continue
        pair = [(profiles.get((leg[1]['A'], leg[2])), level) for leg in legs]
        if all(profile for profile, _ in pair):
            flight = sum(leg[4] for leg in legs) * tick
            result[level].append((route['A'], pair, flight, int(number(route, 'H'))))
    return result


def ordinary_items(items, level, cargo):
    return {key: cargo // int(item['J']) for key, item in items.items()
            if (key.startswith('item_') and not key.startswith(('item_astralite_', 'item_repair'))
                and key != 'item_fuel_cells_01' and int(item['I']) <= level
                and int(item['J']) > 0 and item.get('K') == 'Да')}


def trade_corridors(sheets, ships, tick, items, fuel_price, *, fuel_by_level=None):
    hulls = records(sheets['HullConfig'])
    periods, goods = profile_data(sheets['MarketProfileConfig'])
    for level, corridors in sorted(available_corridors(sheets, ships, tick).items()):
        cargo = int(midpoint(hulls[f'hull_ally_ranger_L{level:02}_01'], 'P', 'Q'))
        limits = ordinary_items(items, level, cargo)
        comparisons = []
        for name, (a, b), flight, fuel in corridors:
            if fuel_by_level is not None:
                fuel = fuel_by_level[level]
            cycle = flight + 45
            trade = max(one_cargo(first, second, periods, goods, limits, cycle, 2 * fuel * fuel_price)
                        for first, second in ((a, b), (b, a)))
            comparisons.append((trade, name, a, b, cycle, fuel))
        ranked = sorted(comparisons, key=lambda row: (row[0], row[-1] / row[-2], row[1]))
        yield level, ranked, (periods, goods, limits)


def delivery_network(sheets, maximum_level):
    systems = {row['A'] for row in sheets['Системы'] if '&range=' in row.get('D', '')
               and number(linked_row(sheets['Сектора'], row['D']), 'E') <= maximum_level}
    neighbors = defaultdict(list)
    for route in sheets['Маршруты']:
        if route.get('G') != 'Нет' or not route.get('A', '').startswith('route_'):
            continue
        a, b = (linked_row(sheets['Системы'], route[column])['A'] for column in 'EF')
        if {a, b} <= systems:
            fuel = int(number(route, 'H'))
            neighbors[a].append((b, fuel))
            neighbors[b].append((a, fuel))
    inhabited = defaultdict(int)
    for planet in sheets['Планеты']:
        if planet.get('D', '').startswith('#gid=') and linked_row(sheets['Тип планеты'], planet['F']).get('E') == 'Да':
            inhabited[linked_row(sheets['Системы'], planet['D'])['A']] += 1
    return neighbors, inhabited


def minimum_fuel(neighbors, source):
    costs = {source: 0}
    pending = [(0, source)]
    while pending:
        used, system = heappop(pending)
        if used != costs[system]:
            continue
        for destination, fuel in neighbors[system]:
            total = used + fuel
            if total < costs.get(destination, float('inf')):
                costs[destination] = total
                heappush(pending, (total, destination))
    return costs


def campaign_credit_sensitivity(credit_rates):
    assert len(credit_rates) == len(BASE_BUDGET) == len(MAP_COST)
    development = [budget - map_cost for budget, map_cost in zip(BASE_BUDGET, MAP_COST)]
    research_scale = 1 + RESEARCH_BUDGET / sum(development)
    # Maps are funded in the preceding sector. Research is apportioned by development spend.
    maps_by_earning_sector = (*MAP_COST[1:], 0)
    targets = [cost * research_scale * .7 + map_cost
               for cost, map_cost in zip(development, maps_by_earning_sector)]
    equal_targets = [sum(targets) / len(targets)] * len(targets)
    print(f'Цель без VIP: {sum(targets):.0f} кр. после топлива и ремонта за {ACTIVE_HOURS} ч.')
    print('| Способ | Равные расходы по уровням, ч | Расходы по этапам, ч | Доля 150 ч по этапам |')
    print('|---|---:|---:|---:|')
    for label, rates in (
        ('Торговля', [trade for trade, _ in credit_rates]),
        ('Цепочка доставок', [delivery for _, delivery in credit_rates]),
        ('Чередование по лучшей ставке на каждом уровне', [max(pair) for pair in credit_rates]),
    ):
        equal_hours = sum(target / rate for target, rate in zip(equal_targets, rates)) / 60
        stage_hours = sum(target / rate for target, rate in zip(targets, rates)) / 60
        print(f'| {label} | {equal_hours:.2f} | {stage_hours:.2f} | {stage_hours / ACTIVE_HOURS:.2%} |')


def main():
    sheets = read_sheets((*SHEETS, *EXTRA_SHEETS))
    ships = build_ships(sheets)
    tick = next(number(row, 'D') for row in sheets['GameTimeConfig'] if row.get('B') == 'secondsPerGameTick')
    items = {row['A']: row for row in sheets['ItemConfig'] if row.get('M', '').endswith('.asset')}
    fuel_price = number(items['item_fuel_cells_01'], 'G')
    hull_price = number(items['item_repair_01'], 'G')
    delivery_unit = settings(sheets['DeliveryMissionTemplateConfig'])['fuelRewardDeflator']
    costs = liberation_costs(sheets, fuel_price, hull_price)
    trophy_worth = fit_trophy_curve(costs)
    scenarios = {(level, faction): (ships[f'ally_ranger_L{level:02}_01'], reference_trios(ships, level, faction))
                 for level in range(1, 11) for faction in FACTIONS}
    combat_seconds = longer_battles(ships, scenarios, tick, display=False)
    repair_routes = repair_route_scenarios(sheets, ships, tick)

    print('| Сектор | Коридоров | Торговля, кр./мин | Доставка, кр./мин | Доставка для паритета, кр./ед. топлива | Отход, кр./мин | Победа, кр./мин |')
    print('|---:|---:|---:|---:|---:|---:|---:|')
    starting_capital = {}
    return_offers = []
    credit_rates = []
    for level, comparisons, (periods, goods, limits) in trade_corridors(sheets, ships, tick, items, fuel_price):
        trade, route_id, a, b, cycle, fuel = comparisons[len(comparisons) // 2]
        delivery = (delivery_unit - fuel_price) * 2 * fuel * 60 / cycle
        parity = fuel_price + trade * cycle / (120 * fuel)
        route = next(row for row in sheets['Маршруты'] if row.get('A') == route_id)
        endpoints = [linked_row(sheets['Системы'], route[column])['A'] for column in 'EF']
        neighbors, inhabited = delivery_network(sheets, level)
        costs_from_origin = minimum_fuel(neighbors, endpoints[0])
        connected = costs_from_origin.keys()
        assert costs_from_origin[endpoints[1]] == fuel, f'{route_id}: direct jump is not the cheapest'
        possible_targets = [sum(inhabited[system] for system in connected - {endpoint}) for endpoint in endpoints]
        return_offers.append((level, route_id, *possible_targets, (delivery_unit - 2 * fuel_price) * fuel))
        credit_rates.append((trade, delivery))
        if level <= 2:
            starting_capital[level] = max(one_cargo(first, second, periods, goods, limits, cycle,
                                                    2 * fuel * fuel_price, wallet=2000)
                                          for first, second in ((a, b), (b, a)))
        combat = '— | —'
        if level in costs:
            ranks, _, expense = costs[level]
            receipts = sum(trophy_worth[rank] for rank in ranks) / 2
            mission_reward = ceil(expense / len(ranks)) * len(ranks)
            duration = combat_seconds[level] + max(row[0] for row in repair_routes[level]) + 45
            combat = f'{(receipts - expense) * 60 / duration:.0f} | {(receipts + mission_reward - expense) * 60 / duration:.0f}'
        print(f'| {level} | {len(comparisons)} | {trade:.0f} | {delivery:.0f} | {parity:.1f} | {combat} |')
    print(f'Повторяемая торговля с кошельком 2000 кр. на тех же коридорах: '
          f'{starting_capital[1]:.0f} кр./мин в секторе 1 и {starting_capital[2]:.0f} кр./мин в секторе 2.')
    print('\n| Сектор | Коридор | Планет-целей из A / B | Шанс точного возврата к точке A или B при свежей доставке | Один заказ, пустой возврат, кр. |')
    print('|---:|---|---:|---:|---:|')
    for level, route_id, target_a, target_b, net in return_offers:
        print(f'| {level} | {route_id} | {target_a} / {target_b} | {1 / target_b:.2%} / {1 / target_a:.2%} | {net:.0f} |')

    campaign_credit_sensitivity(credit_rates)


if __name__ == '__main__':
    main()
