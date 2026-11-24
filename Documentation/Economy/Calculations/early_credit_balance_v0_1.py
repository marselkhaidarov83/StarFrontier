"""Read launch data and estimate battle cash flows before proposed rebalancing."""

import argparse
from collections import defaultdict
from math import ceil, prod
from pathlib import Path
import re
from statistics import mean, median

from combat_balance_v0_1 import (
    FACTIONS, SHEETS, build_ships, group_members, midpoint, number, read_sheets, records,
    repair_route_scenarios,
)
from progression_economy_v0_1 import EXTRA_SHEETS, settings


def catalog_prices(configs):
    root = Path(__file__).resolve().parents[3]
    scene = (root / "Assets/_Project/Scenes/BootstrapScene.unity").read_text()
    section = scene.split("  equipmentCatalog:\n", 1)[1].split("  hullCatalog:", 1)[0]
    by_guid = dict(re.findall(r"(?:Weapon|Component): \{[^\n]*guid: ([a-f0-9]+)[^\n]*\}[^\n]*\n(?:[^\n]*\n)*?    NewPrice: (\d+)\n", section))
    prices = {}
    for key, row in configs.items():
        meta = root / (row["E"] + ".meta")
        guid = re.search(r"^guid: ([a-f0-9]+)$", meta.read_text(), re.MULTILINE).group(1)
        if guid in by_guid:
            prices[key] = int(by_guid[guid])
    return prices


def trophy_values(sheets, levels=(1, 2)):
    equipment = records(sheets["ShipEquipmentConfig"])
    weapons = records(sheets["WeaponConfig"])
    prices = catalog_prices(equipment | weapons)
    hulls = records(sheets["HullConfig"])
    hull_groups = group_members(sheets["HullGroupConfig"], "Корпуса группы", "C")
    weapon_groups = group_members(sheets["WeaponGroupConfig"], "Оружие группы", "C")
    npc_weapons = group_members(sheets["EnemyConfig"], "Группы оружия", "C")
    equipment_groups = group_members(
        sheets["EquipmentGroupConfig"], "Внутренние группы по типам / Варианты оборудования", "D",
    )
    loot_rows = sheets["NpcLootTableConfig"]
    loot_tables = records(loot_rows)
    loot_equipment = defaultdict(list)
    for row in loot_rows:
        if row.get("A", "").startswith("enemy_") and row.get("D") in ("Да", "Нет") and "F" in row:
            loot_equipment[row["A"]].append(row)
    enemies = records(sheets["EnemyConfig"])
    result = {}
    empty_holds = {}
    for faction in FACTIONS:
        for level in levels:
            key = f"enemy_{faction}_L{level:02}_01"
            npc = enemies[key]
            loot_id = npc["L"]
            initial = int(number(loot_tables[loot_id], "G"))
            values = []
            empty = []
            for hull_key in hull_groups[npc["G"]]:
                hull = hulls[hull_key]
                slots = defaultdict(list)
                for item in equipment_groups[npc["H"]]:
                    slot = int(equipment[item]["N"].split(" · ")[0])
                    if hull[chr(ord("R") + slot)] == "Да":
                        slots[slot].append(item)
                for weapon_group in npc_weapons[key]:
                    arsenal = weapon_groups[weapon_group][:int(number(hull, "M"))]
                    total = 0
                    probabilities = []
                    for r in loot_equipment[loot_id]:
                        slot = int(number(r, "E"))
                        component = r["D"] == "Да"
                        if component:
                            candidates = slots[slot]
                        else:
                            candidates = arsenal[slot:slot + 1]
                        if not candidates:
                            continue
                        probabilities.append(1 - number(r, "F") / 200)
                        estimates = []
                        for item in candidates:
                            config = equipment[item] if component else weapons[item]
                            base = prices.get(item, int(number(config, "H")) if component else 0)
                            assert base > 0, f"No quote for {item}"
                            maximum = int(number(config, "O" if component else "X"))
                            durability = maximum if initial < 0 else min(initial, maximum)
                            estimates.append(max(1, (2 * base * durability + maximum) // (2 * maximum)))
                        total += number(r, "F") / 100 * mean(estimates)
                    values.append(total)
                    empty.append(prod(probabilities))
            result[faction, level] = mean(values)
            empty_holds[faction, level] = mean(empty)
    return result, empty_holds


def early_loadout_cost(sheets):
    prices = {}
    for row in sheets['MarketProfileConfig']:
        if row.get('C') != '2' or not row.get('D', '').isdigit():
            continue
        candidate = row.get('F') or row.get('E', '')
        if row.get('G') == 'Да' and 'I' in row and candidate.startswith(('weapon_', 'ship_')):
            prices[candidate] = min(prices.get(candidate, float('inf')), int(number(row, 'I')))
    profile = 'ally_ranger_L02_01'
    hull_id = 'hull_' + profile
    hull_prices = [int(number(row, 'G')) for row in sheets['MarketProfileConfig']
                   if row.get('C') == '2' and row.get('E') == hull_id]
    hull = records(sheets['HullConfig'])[hull_id]
    equipment = records(sheets['ShipEquipmentConfig'])
    slots = defaultdict(list)
    for item, price in prices.items():
        if item in equipment and equipment[item]['L'] == '2':
            slot = int(equipment[item]['N'].split(' · ')[0])
            slots[slot].append(price)
    parts = sum(min(slots[slot]) for slot in range(7) if hull[chr(ord('R') + slot)] == 'Да')
    weapon_groups = group_members(sheets['WeaponGroupConfig'], 'Оружие группы', 'C')
    groups = group_members(sheets['AllyConfig'], 'Группы оружия', 'C')[profile]
    slots_count = int(number(hull, 'M'))
    loadouts = [weapon_groups[group][:slots_count] for group in groups]
    weapons = min(sum(prices[item] for item in loadout) for loadout in loadouts
                  if len(loadout) == slots_count and all(item in prices for item in loadout))
    return min(hull_prices) + parts + weapons


def liberation_costs(sheets, fuel_price, hull_price):
    ships = build_ships(sheets)
    tick = next(number(row, 'D') for row in sheets['GameTimeConfig'] if row.get('B') == 'secondsPerGameTick')
    routes = repair_route_scenarios(sheets, ships, tick)
    hulls = records(sheets['HullConfig'])
    costs = {}
    for sector in range(2, 11):
        player_level = 1 if sector == 2 else sector
        hull = midpoint(hulls[f'hull_ally_ranger_L{player_level:02}_01'], 'N', 'O')
        enemy_levels = range(max(1, sector - 2), sector + 1)
        fuel_requirements = [entry[1] for entry in routes[sector]]
        repair = ceil(hull * .5) * hull_price
        costs[sector] = (enemy_levels, repair + median(fuel_requirements) * fuel_price,
                         repair + max(fuel_requirements) * fuel_price)
    return costs


def mission_reward_curve(sheets, fuel_price, hull_price):
    costs = liberation_costs(sheets, fuel_price, hull_price)
    value, _ = trophy_values(sheets, range(1, 11))
    print('| Сектор | Целей за вылет | Топливо и корпус, кр. | Награда за цель для покрытия, кр. | Трофеи при сборе 50%, кр. | Чистый результат отхода, кр. | Доля сбора для безубыточного отхода |')
    print('|---:|---:|---:|---:|---:|---:|---:|')
    for sector, (enemy_levels, cost, _) in costs.items():
        targets = len(enemy_levels)
        drops = mean(sum(value[faction, level] for level in enemy_levels) for faction in FACTIONS)
        proceeds = drops * .5
        print(f'| {sector} | {targets} | {cost:.0f} | {ceil(cost / targets)} '
              f'| {proceeds:.0f} | {proceeds - cost:+.0f} | {cost / drops:.1%} |')


def fit_trophy_curve(costs):
    target = {sector: maximum * 1.2 for sector, (_, _, maximum) in costs.items()}
    slope = max(1, ceil(max((target[sector] - 1.5 * target[2]) / (1.5 * (sector - 2.5))
                            for sector in range(3, 11))))
    first = ceil(target[2] - slope / 2)
    return {rank: first + slope * (rank - 1) for rank in range(1, 11)}


def calibrated_trophies(sheets, fuel_price, hull_price):
    costs = liberation_costs(sheets, fuel_price, hull_price)
    worth = fit_trophy_curve(costs)
    old_values, _ = trophy_values(sheets, range(1, 11))
    print('| Уровень врага | Ожидаемая стоимость всего выпадения, кр. | Множитель к исходной стоимости |')
    print('|---:|---:|---:|')
    for rank, value in worth.items():
        original = mean(old_values[faction, rank] for faction in FACTIONS)
        print(f'| {rank} | {value} | {value / original:.2%} |')
    print('\n| Сектор | Расходы на дорогом маршруте, кр. | Выручка при сборе 50%, кр. | Прибыль отхода, кр. | Сбор для окупаемости |')
    print('|---:|---:|---:|---:|---:|')
    for sector, (enemy_levels, _, expense) in costs.items():
        dropped_value = sum(worth[rank] for rank in enemy_levels)
        proceeds = dropped_value * .5
        assert proceeds >= expense * 1.2
        print(f'| {sector} | {expense:g} | {proceeds:g} | {proceeds - expense:g} | {expense / dropped_value:.1%} |')
    enemy_levels, _, expense = costs[2]
    total_reward = len(enemy_levels) * ceil(expense / len(enemy_levels))
    net_success = sum(worth[rank] for rank in enemy_levels) * .5 + total_reward - expense
    wallet = 2000 + 4 * net_success
    remaining = wallet - 2600 - 1000 - early_loadout_cost(sheets)
    print(f'Четыре успешных вылета во втором секторе: {wallet:g} кр. с учётом обучения, '
          f'остаток после карты, станции 1 и комплекта 2 — {remaining:g} кр.')


def main(*, rewards=False, calibrate=False):
    sheets = read_sheets((*SHEETS, *EXTRA_SHEETS))
    reward = settings(sheets["MissionGameplayConfig"])["LiberationKillRewardDeflator"]
    items = {row["A"]: row for row in sheets["ItemConfig"] if row.get("M", "").endswith(".asset")}
    fuel_price = number(items["item_fuel_cells_01"], "G")
    hull_price = number(items["item_repair_01"], "G")
    if rewards:
        mission_reward_curve(sheets, fuel_price, hull_price)
        return
    if calibrate:
        calibrated_trophies(sheets, fuel_price, hull_price)
        return
    values, empty_holds = trophy_values(sheets)
    hulls = records(sheets["HullConfig"])
    hull = midpoint(hulls["hull_ally_ranger_L01_01"], "N", "O")
    direct = next(row for row in sheets["Маршруты"] if row.get("A") == "route_dust_harbor_solaria_02")
    fuel_cost = 2 * number(direct, "H") * fuel_price

    print("| Фракция | Трофеи за врага 1, кр. | Трофеи за врага 2, кр. | Выручка с двух при сборе 50%, кр. |")
    print("|---|---:|---:|---:|")
    for faction in FACTIONS:
        first, second = values[faction, 1], values[faction, 2]
        print(f"| {faction} | {first:.1f} | {second:.1f} | {(first + second) / 2:.1f} |")
    trophies = mean((values[f, 1] + values[f, 2]) / 2 for f in FACTIONS)
    income = 2 * reward + trophies
    print(f"Два уничтожения, успешная миссия и ожидаемая продажа: {income:.1f} кр. за вылет.")
    no_trophies = mean(empty_holds[f, 1] * empty_holds[f, 2] for f in FACTIONS)
    print(f"При независимом сборе 50% выпавшего оборудования доля вылетов без проданных трофеев: {no_trophies:.1%}.")
    print(f"Топливо на прямой маршрут туда и обратно: {fuel_cost:g} кр.")
    kit = early_loadout_cost(sheets)
    cash_goal = 2600 + 1000
    print(f"Полный комплект уровня 2: {kit} кр. Карта и научная станция: {cash_goal} кр.")
    print("| Урон корпусу | Топливо и корпус за вылет, кр. | Чистый результат 4 вылетов, кр. | До карты + станции 1, кр. | До карты, станции и комплекта, кр. |")
    print("|---:|---:|---:|---:|---:|")
    for damage in (0, 0.25, 0.5, 0.75):
        operating = fuel_cost + ceil(hull * damage) * hull_price
        net = 4 * (income - operating)
        print(f"| {damage:.0%} | {operating:g} | {net:.0f} | {cash_goal - net:.0f} | {cash_goal + kit - net:.0f} |")
    operating = fuel_cost + ceil(hull * 0.5) * hull_price
    print(f"Цена цели при 50% повреждения корпуса: {ceil(operating / 2)} кр. для покрытия вылета, "
          f"{ceil((operating + cash_goal / 4 - trophies) / 2)} кр. для карты и станции с ожидаемыми трофеями, "
          f"{ceil((operating + cash_goal / 4) / 2)} кр. для той же цели без продажи трофеев.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--rewards', action='store_true', help='Estimate battle mission compensation by sector.')
    modes.add_argument('--calibrate', action='store_true', help='Fit a trial trophy value curve to profitable retreats.')
    arguments = parser.parse_args()
    main(rewards=arguments.rewards, calibrate=arguments.calibrate)
