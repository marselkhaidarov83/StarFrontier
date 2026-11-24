"""Evaluate the original economy against the agreed sortie and progression rules."""

import argparse
from collections import defaultdict
from math import ceil
from itertools import permutations
from statistics import mean, median

from combat_balance_v0_1 import (
    FACTIONS, SHEETS, build_ships, group_members, linked_row, longer_battles, midpoint, number,
    read_sheets, records, reference_trios, repair_route_scenarios, sorties_to_clear, spawn_rows,
)

EXTRA_SHEETS = (
    "DeliveryMissionTemplateConfig", "ReconMissionTemplateConfig",
    "RouteMissionTemplateConfig", "MissionGameplayConfig", "NpcLootTableConfig",
    "Оплаты станций", "MarketProfileConfig", "ResearchBranchConfig", "InvasionScheduleConfig", "NewGameConfig",
)


def settings(rows):
    return {row["B"]: number(row, "C") for row in rows
            if row.get("B") in {"fuelRewardDeflator", "rewardCredits", "LiberationKillRewardDeflator",
                                "DefenseKillRewardDeflator"}}


def generated_loot(rows):
    expected = defaultdict(lambda: defaultdict(float))
    for row in rows:
        if not row.get("A", "").startswith("enemy_") or "I" not in row or not row.get("B", "").isdigit():
            continue
        kind = row["D"].split(" · ")[0]
        expected[row["A"]][kind] += midpoint(row, "G", "H") * number(row, "I") / 100
    return expected


def station_count_diagnostics(sheets):
    prices = [row for row in sheets["Оплаты станций"] if row.get("C") == "credits_astralites"
              and row.get("A", "").startswith("station_payment_science_L")]
    research_levels = [row for row in sheets["ResearchBranchConfig"] if row.get("A", "").startswith("research_branch_")
                       and row.get("D", "").isdigit() and row.get("I", "").isdigit()]
    research_with_price = sum(number(row, "G") > 0 for row in research_levels)
    return (
        sum(number(row, "D") for row in prices),
        sum(number(row, "F") for row in prices),
        len(research_levels), research_with_price,
    )


def market_snapshot(rows):
    goods = defaultdict(lambda: defaultdict(dict))
    hull_prices = defaultdict(list)
    for row in rows:
        if not row.get("A", "").startswith("mMarketProfile_") or not row.get("D", "").isdigit():
            continue
        level = int(row["C"])
        item = row.get("E", "")
        if item.startswith("item_"):
            goods[level][row["A"]][item] = tuple(int(number(row, field)) for field in "FHIJ")
        if item == f"hull_ally_ranger_L{level:02}_01":
            hull_prices[level].append(int(number(row, "G")))
    return goods, hull_prices


def quote(stock, cap, basic, maximum):
    missing = max(0, cap - stock)
    return basic + (2 * (maximum - basic) * missing + cap) // (2 * cap)


def best_single_cargo_profit(profiles, items, level, cargo):
    best = 0
    for origin, destination in permutations(profiles.values(), 2):
        for key in origin.keys() & destination.keys():
            item = items[key]
            if (key.startswith(("item_astralite_", "item_repair")) or key == "item_fuel_cells_01"
                    or int(item["I"]) > level or int(item["J"]) <= 0 or item.get("K") != "Да"):
                continue
            source_stock, source_cap, source_base, source_max = origin[key]
            target_stock, target_cap, target_base, target_max = destination[key]
            margin = 0
            for unit in range(1, min(source_stock, cargo // int(item["J"])) + 1):
                margin += quote(target_stock + unit, target_cap, target_base, target_max)
                margin -= quote(source_stock - unit + 1, source_cap, source_base, source_max)
                best = max(best, margin)
    return best


def liberation_envelope(sheets, ships, routes, tick, systems_by_sector):
    scenarios = {(level, faction): (ships[f"ally_ranger_L{level:02}_01"], reference_trios(ships, level, faction))
                 for level in range(1, 11) for faction in FACTIONS}
    shots = longer_battles(ships, scenarios, tick, display=False)
    lower_total = upper_total = 0
    clearance = {}
    print("\n| Сектор | Изначально вражеских систем | Вылеты на систему | Время кампании по сектору, мин |")
    print("|---:|---:|---:|---:|")
    for level, count in sorted(systems_by_sector.items()):
        trip_seconds = max(row[0] for row in routes[level])
        cycle = ceil(shots[level] + trip_seconds + 45)
        kill_ticks = tuple(ceil(shots[level] * kill / 3) for kill in (1, 2, 3))
        attempts = []
        for faction in FACTIONS:
            interval, min_capacity, max_capacity = spawn_rows(sheets["EnemySpawnRuleConfig"], faction, level)
            if level == 10:
                interval = max(interval, 60)
            min_capacity = max(1, min_capacity)
            for capacity in range(min_capacity, max_capacity + 1):
                attempts.extend(sorties_to_clear(capacity, interval, phase, cycle, kill_ticks)
                                for phase in range(interval))
        if None in attempts:
            raise ValueError(f"Sector {level}: a trial scenario fails to complete")
        first, last = min(attempts), max(attempts)
        clearance[level] = first * cycle, last * cycle
        lower_total += count * first * cycle
        upper_total += count * last * cycle
        print(f"| {level} | {count} | {first}–{last} | {count * first * cycle / 60:.1f}–{count * last * cycle / 60:.1f} |")
    system_count = sum(systems_by_sector.values())
    print(f"Боевые циклы на {system_count} систем при одном освобождении каждой: "
          f"{lower_total / 3600:.2f}–{upper_total / 3600:.2f} ч.")
    return clearance


def invasion_sensitivity(rows, clearance):
    periods = {int(row["C"]): number(row, "E") for row in rows
               if row.get("C", "").isdigit() and "E" in row and row.get("A") == "invasion_schedule_01"}
    print("\n| Сектор | Попыток вторжения в час | Освобождение системы, с | Порог доли попыток с захватом |")
    print("|---:|---:|---:|---:|")
    for sector, (fast, slow) in sorted(clearance.items()):
        period = periods[sector]
        threshold = min(1, period / slow) * 100
        print(f"| {sector} | {3600 / period:g} | {fast}–{slow} | {threshold:.1f}% |")


def sector_access(sheets, operating_costs, fuel_price):
    systems = {row["A"]: row for row in sheets["Системы"] if "&range=" in row.get("D", "")
               and row.get("A", "").startswith("s")}
    sector_of = {key: int(number(linked_row(sheets["Сектора"], row["D"]), "E")) for key, row in systems.items()}
    open_routes = []
    for row in sheets["Маршруты"]:
        if row.get("G") != "Нет" or not row.get("A", "").startswith("route_"):
            continue
        origin, destination = (linked_row(sheets["Системы"], row[column])["A"] for column in "EF")
        open_routes.append((origin, destination, int(number(row, "H"))))
    start_settings = {row.get("B"): row.get("D") for row in sheets["NewGameConfig"]}
    start = start_settings["StartSystem"]
    station_payment = defaultdict(dict)
    for row in sheets["Оплаты станций"]:
        if row.get("A", "").startswith("station_payment_science_L") and row.get("C") in {"credits_astralites", "diamonds"}:
            station_payment[int(row["A"][-2:])][row["C"]] = row
    print("\n| Новый сектор | Доступно систем при открытии | Порог контроля предыдущего | Переходов между секторами | Карта, кр. + астр. | Карта, алм. |")
    print("|---:|---:|---:|---:|---:|---:|")
    credit_total = astralite_total = diamond_total = 0
    map_astralites = {}
    for level in range(1, 11):
        graph = defaultdict(set)
        crossing_count = 0
        for a, b, _ in open_routes:
            if max(sector_of[a], sector_of[b]) > level:
                continue
            graph[a].add(b)
            graph[b].add(a)
            crossing_count += sorted((sector_of[a], sector_of[b])) == [level - 1, level]
        seen = {start}
        pending = [start]
        while pending:
            neighbors = graph[pending.pop()] - seen
            seen.update(neighbors)
            pending.extend(neighbors)
        sector_systems = {key for key in systems if sector_of[key] == level}
        count = len(seen & sector_systems)
        assert count == len(sector_systems)
        if level < 3:
            print(f"| {level} | {count}/{len(sector_systems)} | — | {crossing_count} | — | — |")
            continue
        previous = level - 1
        required = ceil(sum(sector == previous for sector in sector_of.values()) * 0.8)
        credits = ceil(operating_costs[previous] / 100) * 100
        payment = station_payment[previous]
        normal = payment["credits_astralites"]
        scale = credits / number(normal, "D")
        astralites = ceil(number(normal, "F") * scale)
        map_astralites[level] = astralites
        diamonds = ceil(number(payment["diamonds"], "G") * scale)
        credit_total += credits
        astralite_total += astralites
        diamond_total += diamonds
        print(f"| {level} | {count}/{len(sector_systems)} | {required} | {crossing_count} | {credits} + {astralites} | {diamonds} |")
    print(f"Восемь карт по пробной шкале: {credit_total} кредитов и {astralite_total} астралитов или {diamond_total} алмазов.")
    start_exit_costs = {b if a == start else a: cost for a, b, cost in open_routes if start in (a, b)}
    first_local = min(cost for key, cost in start_exit_costs.items() if sector_of[key] == 1)
    second_sector = min(cost for key, cost in start_exit_costs.items() if sector_of[key] == 2)
    print(f"Новая игра: {start_settings['StartCredit']} кредитов и {start_settings['StartFuel']} топлива. "
          f"Из {systems[start]['B']} первый прыжок внутри сектора стоит от {first_local * fuel_price:g} кредитов, "
          f"прямой прыжок во второй сектор — {second_sector * fuel_price:g} кредитов.")
    print(f"Для первого рейса во второй сектор и обратно по прямому маршруту: {2 * second_sector} ед. топлива.")
    return map_astralites


def astralite_balance(sheets, items, by_sector, map_astralites):
    mass = max(int(item['J']) for key, item in items.items() if key.startswith('item_astralite_'))
    strength = {level: min(number(row, 'AD') for row in sheets['ShipEquipmentConfig']
                           if row.get('A', '').startswith('ship_grabber_') and row.get('L') == str(level))
                for level in range(1, 11)}
    drop_packages = defaultdict(list)
    for row in sheets['NpcLootTableConfig']:
        if row.get('A', '').startswith('enemy_') and row.get('D', '').startswith('40 ·') and 'I' in row:
            drop_packages[row['A']].append((int(row['G']), int(row['H']), number(row, 'I') / 100))

    def expected_collectable(loot_id, max_units):
        return sum(chance * sum(range(low, min(high, max_units) + 1)) / (high - low + 1)
                   for low, high, chance in drop_packages[loot_id])

    science = {int(row['A'][-2:]): int(number(row, 'F')) for row in sheets['Оплаты станций']
               if row.get('A', '').startswith('station_payment_science_L') and row.get('C') == 'credits_astralites'}
    print('\n| Сектор | Освобождений к следующему переходу | Среднее на систему при минимальном гарнизоне | Доступно накопительно, шт. | Станция и карты накопительно, шт. | Доля сбора для этих платежей |')
    print('|---:|---:|---:|---:|---:|---:|')
    cumulative = 0.0
    deferred = 0.0
    for level, systems in sorted(by_sector.items()):
        needed = ceil(0.8 * systems) if level + 1 in map_astralites else systems
        per_faction = []
        # Allow the player to keep a grabber from the previous level.
        max_units = int(strength[level - 1] // mass)
        for faction in FACTIONS:
            rule_id = f'enemy_spawn_rule_{faction}_01'
            entries = [row for row in sheets['EnemySpawnRuleConfig']
                       if row.get('A') == rule_id and row.get('B') == str(level)
                       and row.get('D', '').startswith(f'enemy_{faction}_L')]
            per_faction.append(sum(number(row, 'E') * expected_collectable(row['D'] + '_loot', max_units)
                                   for row in entries))
        per_system = mean(per_faction)
        cumulative += per_system * needed
        deferred += per_system * (systems - needed)
        known_expenses = sum(amount for rank, amount in science.items() if rank <= level)
        known_expenses += sum(amount for rank, amount in map_astralites.items() if rank <= level + 1)
        print(f'| {level} | {needed}/{systems} | {per_system:.1f} | {cumulative:.1f} '
              f'| {known_expenses} | {known_expenses / cumulative:.1%} |')
    research = 694  # From the funded 368-stage research trial.
    full_drops = cumulative + deferred
    print(f'Все изначально захваченные системы: {full_drops:.1f} потенциальных астралитов при минимальных гарнизонах.')
    print(f'Обычные платежи и исследовательская проба: {known_expenses + research} астралитов, '
          f'{(known_expenses + research) / full_drops:.1%} от ожидаемой доступной добычи.')
    first_gate_risk(sheets, drop_packages, int(strength[1] // mass), science, map_astralites, by_sector[2])


def add_package(distribution, low, high, chance, capacity, target):
    outcomes = defaultdict(float)
    for quantity in range(low, min(high, capacity) + 1):
        outcomes[quantity] = chance / (high - low + 1)
    outcomes[0] += 1 - sum(outcomes.values())
    result = defaultdict(float)
    for collected, weight in distribution.items():
        for quantity, probability in outcomes.items():
            result[min(collected + quantity, target)] += weight * probability
    return result


def first_gate_risk(sheets, drop_packages, capacity, science, map_astralites, systems):
    card = map_astralites[3]
    thresholds = (card, card + science[1], card + science[1] + science[2])
    count = ceil(systems * 0.8)
    packages_by_faction = []
    for faction in FACTIONS:
        rule = f'enemy_spawn_rule_{faction}_01'
        packages = []
        for row in sheets['EnemySpawnRuleConfig']:
            if (row.get('A') == rule and row.get('B') == '2'
                    and row.get('D', '').startswith(f'enemy_{faction}_L')):
                packages.extend(drop_packages[row['D'] + '_loot'] * int(row['E']))
        packages_by_faction.append(packages)
    print(f'\n| Доля подбираемых доступных пакетов | Карта ({thresholds[0]} шт.) | Карта и станция 1 ({thresholds[1]} шт.) | Карта и станция 1–2 ({thresholds[2]} шт.) | Все платежи после {systems} систем |')
    print('|---:|---:|---:|---:|---:|')
    for pickup in (0.2, 0.25, 0.3, 0.4, 0.5):
        outcomes = []
        for packages in packages_by_faction:
            distribution = {0: 1.0}
            for low, high, chance in packages * count:
                distribution = add_package(distribution, low, high, chance * pickup, capacity, thresholds[-1])
            assert abs(sum(distribution.values()) - 1) < 1e-10
            chance_at_gate = [sum(probability for quantity, probability in distribution.items() if quantity >= target)
                              for target in thresholds]
            for low, high, chance in packages * (systems - count):
                distribution = add_package(distribution, low, high, chance * pickup, capacity, thresholds[-1])
            outcomes.append([*chance_at_gate, distribution.get(thresholds[-1], 0)])
        print(f'| {pickup:.0%} | ' + ' | '.join(f'{mean(result):.1%}' for result in zip(*outcomes)) + ' |')


def main(*, astralites=False):
    sheets = read_sheets((*SHEETS, *EXTRA_SHEETS))
    ships = build_ships(sheets)
    hulls = records(sheets["HullConfig"])
    hull_groups = group_members(sheets["HullGroupConfig"], "Корпуса группы", "C")
    allies = records(sheets["AllyConfig"])
    tick = next(number(row, "D") for row in sheets["GameTimeConfig"] if row.get("B") == "secondsPerGameTick")
    routes = repair_route_scenarios(sheets, ships, tick)
    items = {row["A"]: row for row in sheets["ItemConfig"] if row.get("M", "").endswith(".asset")}
    fuel_price = number(items["item_fuel_cells_01"], "G")
    hull_repair_price = number(items["item_repair_01"], "G")
    delivery = settings(sheets["DeliveryMissionTemplateConfig"])["fuelRewardDeflator"]
    reconnaissance = settings(sheets["ReconMissionTemplateConfig"])["rewardCredits"]
    route_reward = settings(sheets["RouteMissionTemplateConfig"])["rewardCredits"]
    combat = settings(sheets["MissionGameplayConfig"])
    loot = generated_loot(sheets["NpcLootTableConfig"])
    market_goods, hull_prices = market_snapshot(sheets["MarketProfileConfig"])
    print(f"Тарифы: топливо {fuel_price:g} кр./ед., корпус {hull_repair_price:g} кр./ОП.")
    print(f"Награды: доставка {delivery:g} кр./топливо, разведка {reconnaissance:g}, маршрут {route_reward:g}, "
          f"оборона/освобождение {combat['DefenseKillRewardDeflator']:g}/{combat['LiberationKillRewardDeflator']:g} кр./цель.")
    print(f"Научная станция 1–10 и исследования: {station_count_diagnostics(sheets)} "
          "(кредиты, астралиты, строк этапов, заполнено оплатой).")
    print("\n| Сектор | Топливо за два прыжка, кр. | Разведка / маршрут после топлива, кр. | Восстановление корпуса, кр. | Минимум награды за цель, кр. |")
    print("|---:|---:|---:|---:|---:|")
    operating_costs = {}
    for level in range(1, 11):
        user = f"ally_ranger_L{level:02}_01"
        ship = ships[user]
        hull = median(midpoint(hulls[key], "N", "O") for key in hull_groups[allies[user]["G"]])
        # In the calibration most of the shield/hull pool is lost. Shields take damage first.
        damaged_hull = max(0, ceil(hull - ship.defense * 0.176))
        repair = damaged_hull * hull_repair_price
        fuel_min = min(row[1] for row in routes[level]) * fuel_price
        fuel_max = max(row[1] for row in routes[level]) * fuel_price
        operating_costs[level] = median(row[1] for row in routes[level]) * fuel_price + repair
        print(f"| {level} | {fuel_min:g}–{fuel_max:g} | {reconnaissance - fuel_max:g}…{reconnaissance - fuel_min:g} "
              f"| {repair:g} | {ceil((fuel_min + repair) / 3)}–{ceil((fuel_max + repair) / 3)} |")

    print("\n| Уровень врага | Сгенерированные кредиты | Выпавшие алмазы | Выпавшие астралиты |")
    print("|---:|---:|---:|---:|")
    for level in range(1, 11):
        sources = [loot[f"enemy_{faction}_L{level:02}_01_loot"] for faction in FACTIONS]
        values = [mean(values.get(kind, 0.0) for values in sources) for kind in ("20", "30", "40")]
        print(f"| {level} | " + " | ".join(f"{value:.2f}" for value in values) + " |")

    print("\n| Уровень | Трюм, ед. | Цена нового корпуса рейнджера, кр. | Лучшая маржа партии до топлива, кр. |")
    print("|---:|---:|---:|---:|")
    for level in range(1, 11):
        user = f"ally_ranger_L{level:02}_01"
        cargo = int(median(midpoint(hulls[key], "P", "Q") for key in hull_groups[allies[user]["G"]]))
        trade_margin = best_single_cargo_profit(market_goods[level], items, level, cargo)
        prices = hull_prices[level]
        print(f"| {level} | {cargo} | {min(prices)}–{max(prices)} | {trade_margin} |")

    by_sector = defaultdict(int)
    for row in sheets["Системы"]:
        if not row.get("A", "").startswith("s") or "&range=" not in row.get("D", ""):
            continue
        sector = linked_row(sheets["Сектора"], row["D"])
        if sector.get("G") == "Да":
            by_sector[int(number(sector, "E"))] += 1
    print(f"Системы в секторах с вражеским стартовым признаком: {sum(by_sector.values())} "
          f"({dict(sorted(by_sector.items()))}).")
    clearance = liberation_envelope(sheets, ships, routes, tick, by_sector)
    invasion_sensitivity(sheets["InvasionScheduleConfig"], clearance)
    map_astralites = sector_access(sheets, operating_costs, fuel_price)
    if astralites:
        astralite_balance(sheets, items, by_sector, map_astralites)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--astralites', action='store_true', help='Include the astralite balance by sector.')
    main(astralites=parser.parse_args().astralites)
