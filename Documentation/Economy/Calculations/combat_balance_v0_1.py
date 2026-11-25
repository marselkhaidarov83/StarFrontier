"""Reproduce the STAR FRONTIER combat baseline from SF_Data_v92.xlsx."""

from collections import defaultdict
from dataclasses import dataclass, replace
from itertools import permutations
from math import ceil, cos, dist, hypot, radians, sin
from pathlib import Path
from statistics import mean, median
from xml.etree import ElementTree
from zipfile import ZipFile

WORKBOOK = Path(__file__).resolve().parents[1] / "Sources" / "SF_Data_v92.xlsx"
FACTIONS = ("ai", "ancients", "infected")
ROUTE_SHEETS = ("Маршруты", "Системы", "Сектора", "Планеты", "Тип планеты", "Орбиты планет", "Точки маршрутов")
SHEETS = (
    "AllyConfig", "EnemyConfig", "HullGroupConfig", "WeaponGroupConfig",
    "EquipmentGroupConfig", "HullConfig", "WeaponConfig", "ShipEquipmentConfig",
    "EnemySpawnRuleConfig", "GameTimeConfig", "ShipMovementConfig", "ItemConfig", *ROUTE_SHEETS,
)


def read_sheets(names=SHEETS):
    with ZipFile(WORKBOOK) as book:
        shared = [
            "".join(node.text or "" for node in item.findall(".//{*}t"))
            for item in ElementTree.fromstring(book.read("xl/sharedStrings.xml"))
        ]
        relationships = {
            item.get("Id"): item.get("Target").lstrip("/")
            for item in ElementTree.fromstring(book.read("xl/_rels/workbook.xml.rels"))
        }
        sheets = {}
        for sheet in ElementTree.fromstring(book.read("xl/workbook.xml")).findall(".//{*}sheet"):
            name = sheet.get("name")
            if name not in names:
                continue
            link = next(value for key, value in sheet.attrib.items() if key.endswith("}id"))
            target = relationships[link]
            root = ElementTree.fromstring(book.read(target if target.startswith("xl/") else "xl/" + target))
            sheets[name] = []
            for row in root.findall(".//{*}sheetData/{*}row"):
                values = {}
                for cell in row:
                    column = cell.get("r").rstrip("0123456789")
                    value = cell.findtext("{*}v") or ""
                    if cell.get("t") == "s":
                        value = shared[int(value)]
                    elif cell.get("t") == "inlineStr":
                        value = "".join(node.text or "" for node in cell.findall(".//{*}t"))
                    if value:
                        values[column] = value
                if name in ROUTE_SHEETS:
                    values["row"] = row.get("r")
                sheets[name].append(values)
    return sheets


def number(row, column):
    return float(row[column])


def midpoint(row, lower, upper):
    return mean((number(row, lower), number(row, upper)))


def records(rows):
    return {row["A"]: row for row in rows if row.get("E", "").startswith("Assets/") and row["E"].endswith(".asset")}


def group_members(rows, section, value_column):
    groups = defaultdict(list)
    in_section = False
    for row in rows:
        if len(row) == 1:
            in_section = row.get("A") == section
        if in_section and row.get("B", "").isdigit() and value_column in row:
            groups[row["A"]].append(row[value_column])
    return groups


@dataclass(frozen=True)
class Ship:
    defense: float
    damage_per_tick: float
    speed: float
    weapon_range: float = 0
    shortest_weapon_range: float = 0
    loadouts: tuple = ()


def build_ships(sheets):
    hulls = records(sheets["HullConfig"])
    weapons = records(sheets["WeaponConfig"])
    equipment = records(sheets["ShipEquipmentConfig"])
    hull_groups = group_members(sheets["HullGroupConfig"], "Корпуса группы", "C")
    weapon_groups = group_members(sheets["WeaponGroupConfig"], "Оружие группы", "C")
    equipment_groups = group_members(
        sheets["EquipmentGroupConfig"], "Внутренние группы по типам / Варианты оборудования", "D",
    )
    ships = {}
    for sheet in ("AllyConfig", "EnemyConfig"):
        ship_weapons = group_members(sheets[sheet], "Группы оружия", "C")
        for key, row in records(sheets[sheet]).items():
            if int(row["F"]) == 0 or (sheet == "AllyConfig" and not key.startswith("ally_ranger_")):
                continue
            components = [equipment[key] for key in equipment_groups[row["H"]]]
            shield = median(midpoint(item, "AH", "AI") for item in components if number(item, "AH") > 0)
            speed = median(midpoint(item, "R", "S") for item in components if number(item, "R") > 0)
            samples = []
            for hull_key in hull_groups[row["G"]]:
                hull = hulls[hull_key]
                defense = midpoint(hull, "N", "O") + (shield if hull.get("X") == "Да" else 0)
                for group in ship_weapons[key]:
                    loadout = weapon_groups[group][:int(number(hull, "M"))]
                    # Total weapon damage is split across its shots in the game.
                    salvo = sum(midpoint(weapons[weapon], "H", "I") for weapon in loadout)
                    ranges = [midpoint(weapons[weapon], 'J', 'K') for weapon in loadout]
                    guns = tuple((midpoint(weapons[weapon], 'H', 'I'), reach) for weapon, reach in zip(loadout, ranges))
                    samples.append((defense, salvo, max(ranges), min(ranges), guns))
            ships[key] = Ship(median(row[0] for row in samples), median(row[1] for row in samples), speed,
                              median(row[2] for row in samples), median(row[3] for row in samples),
                              tuple(row[4] for row in samples))
    return ships


def fight(player, enemies, *, separate=False):
    """Compare continuous group fire with non-overlapping exchanges of fire."""
    best = None
    for order in permutations(enemies):
        ticks = 0
        received = 0.0
        remaining_damage = sum(enemy.damage_per_tick for enemy in order)
        for enemy in order:
            target_ticks = ceil(enemy.defense / player.damage_per_tick)
            ticks += target_ticks
            # Simultaneous last volleys make this an explicit conservative assumption.
            incoming_damage = enemy.damage_per_tick if separate else remaining_damage
            received += target_ticks * incoming_damage
            remaining_damage -= enemy.damage_per_tick
        result = (received / player.defense, ticks)
        best = result if best is None else min(best, result)
    return best


def staggered_fight(player, enemies, stagger, damage_factor):
    """Enemies enter mutual firing range at equal offsets, beginning with the weakest."""
    arrivals = [(index * stagger, enemy) for index, enemy in enumerate(enemies)]
    best = None
    for order in permutations(arrivals):
        elapsed = 0
        received = 0.0
        for arrives, enemy in order:
            elapsed = max(elapsed, arrives) + ceil(enemy.defense / player.damage_per_tick)
            received += (elapsed - arrives) * enemy.damage_per_tick * damage_factor
        result = (received / player.defense, elapsed)
        best = result if best is None else min(best, result)
    return best


def minimum_surviving_stagger(player, enemies, damage_factor):
    upper = max(ceil(enemy.defense / player.damage_per_tick) for enemy in enemies)
    for stagger in range(upper + 1):
        pressure, elapsed = staggered_fight(player, enemies, stagger, damage_factor)
        if pressure < 1:
            return stagger, elapsed
    raise ValueError("The calibrated ship cannot survive even with separate fire.")


def spawn_rows(rows, faction, level):
    key = f"enemy_spawn_rule_{faction}_01"
    metadata = next(row for row in rows if row.get("A") == key and row.get("C") == str(level)
                    and row.get("D", "").isdigit() and "G" not in row)
    counts = [row for row in rows if row.get("A") == key and row.get("B") == str(level)
              and row.get("D", "").startswith("enemy_" + faction + "_L")]
    return int(number(metadata, "D")), sum(int(number(row, "E")) for row in counts), sum(
        int(number(row, "F")) for row in counts
    )


def reference_trios(ships, level, faction):
    if level == 1:
        ranks = [(1, 1, 1)]
    elif level == 2:
        ranks = [(1, 1, 2), (1, 2, 2)]
    else:
        ranks = [(level - 2, level - 1, level)]
    return [[ships[f"enemy_{faction}_L{rank:02}_01"] for rank in trio] for trio in ranks]


def longer_battles(ships, scenarios, tick_seconds, *, display=True, movement=None):
    """Trial curve: retain the opening anchor and add one firing tick per level."""
    first_ship = ships["ally_ranger_L01_01"]
    first_target = max(
        fight(first_ship, [ships[f"enemy_{faction}_L01_01"]])[1] for faction in FACTIONS
    )
    rows = []
    for level in range(1, 11):
        target_ticks = first_target + level - 1
        player = ships[f"ally_ranger_L{level:02}_01"]
        opponents = [ships[f"enemy_{faction}_L{level:02}_01"] for faction in FACTIONS]
        max_defense = max(enemy.defense for enemy in opponents)
        player = replace(player, damage_per_tick=max_defense / (target_ticks - 0.5))
        duels = [fight(player, [enemy])[1] * tick_seconds for enemy in opponents]
        all_fire_seconds = []
        faction_runs = []
        for faction in FACTIONS:
            _, trios = scenarios[level, faction]
            simultaneous = [fight(player, trio) for trio in trios]
            separated = [fight(player, trio, separate=True) for trio in trios]
            all_fire_seconds.extend(ticks * tick_seconds for _, ticks in simultaneous)
            low = 1 / min(pressure for pressure, _ in simultaneous)
            high = 1 / max(pressure for pressure, _ in separated)
            faction_runs.append((trios, low, high))
        rows.append((level, target_ticks, player, duels, all_fire_seconds, faction_runs))

    target_floor = max(low / high for *_, runs in rows for _, low, high in runs)
    if target_floor >= 1:
        print("У кривой более долгих боёв нет общего целевого остатка прочности.")
        return
    target = mean((target_floor, 1))
    maximum_firing_time = {level: max(all_fire_seconds) for level, _, _, _, all_fire_seconds, _ in rows}
    if not display:
        return maximum_firing_time
    print(f"\nБолее долгие бои: {100 * (1 - target):.1f}% прочности после самой тяжёлой раздельной тройки.")
    print("| Ур. | Цель дуэли, с | Расчёт дуэли, с | Тройка, время огня, с | Разрыв ИИ / Древние / Заражённые, с |")
    print("|---:|---:|---:|---:|---:|")
    for level, target_ticks, player, duels, all_fire_seconds, runs in rows:
        gaps = []
        for trios, low, high in runs:
            incoming_factor = target * high
            assert low < incoming_factor < high
            gaps.append(f"{max(minimum_surviving_stagger(player, trio, incoming_factor)[0] for trio in trios) * tick_seconds:g}")
        print(f"| {level} | {target_ticks * tick_seconds:g} | {min(duels):g}–{max(duels):g} "
              f"| {min(all_fire_seconds):g}–{max(all_fire_seconds):g} | " + " / ".join(gaps) + " |")
    print("\n| Ур. | Эффективный урон за тик | Номинальный урон при 90% / 75% реализации | Разрыв для остатка ≥10%, с | Доля прочности при таком разрыве | Полное время тройки, с |")
    print("|---:|---:|---|---:|---|---|")
    for level, _, player, _, _, runs in rows:
        outcomes = [(trio, target * high) for trios, _, high in runs for trio in trios]
        gaps = []
        for trio, damage_factor in outcomes:
            limit = max(ceil(enemy.defense / player.damage_per_tick) for enemy in trio)
            gap = next(offset for offset in range(limit + 1)
                       if staggered_fight(player, trio, offset, damage_factor)[0] <= .90)
            gaps.append(gap)
        hardest_gap = max(gaps)
        simulations = [staggered_fight(player, trio, hardest_gap, factor) for trio, factor in outcomes]
        health = [1 - pressure for pressure, _ in simulations]
        durations = [elapsed * tick_seconds for _, elapsed in simulations]
        assert min(health) >= .10 - 1e-10
        print(f"| {level} | {player.damage_per_tick:.1f} | {ceil(player.damage_per_tick / .90)} / "
              f"{ceil(player.damage_per_tick / .75)} | {hardest_gap * tick_seconds:g} | "
              f"{min(health):.1%}–{max(health):.1%} | {min(durations):g}–{max(durations):g} |")
    if movement is not None:
        report_contact_geometry(rows, tick_seconds, target, movement)
        report_distance_fire(rows, tick_seconds, target)
    return maximum_firing_time


def active_fire_share(loadout, distance):
    return sum(damage for damage, reach in loadout if reach >= distance) / sum(damage for damage, _ in loadout)


def ranged_fire(ship, distance):
    share = median(active_fire_share(loadout, distance) for loadout in ship.loadouts)
    return replace(ship, damage_per_tick=ship.damage_per_tick * share), share


def report_distance_fire(rows, tick_seconds, target):
    points = (80, 150, 250, 300)
    print('\n| Ур. | Доля урона на 80 / 150 / 250 / 300 | Огонь по тройке на 80 / 150 / 250 / 300, с |')
    print('|---:|---|---|')
    for level, _, player, _, _, groups in rows:
        shares = []
        times = []
        for distance in points:
            ranged_player, share = ranged_fire(player, distance)
            shares.append(f'{share:.0%}')
            if share == 0:
                times.append('—')
                continue
            fights = [sum(ceil(enemy.defense / ranged_player.damage_per_tick) for enemy in trio) * tick_seconds
                      for trios, _, _ in groups for trio in trios]
            times.append(f'{min(fights):g}–{max(fights):g}')
        print(f'| {level} | {" / ".join(shares)} | {" / ".join(times)} |')

    _, _, late_ship, _, _, late_groups = rows[-1]
    print('\n| Дистанция, ур. 10 | Доля урона комплектов, мин.–макс. | Нулевой урон | Остаток прочности при интервале 14 с |')
    print('|---:|---|---|---|')
    for distance in points:
        loadout_shares = [active_fire_share(loadout, distance) for loadout in late_ship.loadouts]
        ranged_player, share = ranged_fire(late_ship, distance)
        residual = '—'
        if share:
            stagger = ceil(14 / tick_seconds)
            health = [1 - staggered_fight(ranged_player, [ranged_fire(enemy, distance)[0] for enemy in trio],
                                        stagger, target * high)[0]
                      for trios, _, high in late_groups for trio in trios]
            residual = f'{min(health):.1%}–{max(health):.1%}'
        print(f'| {distance} | {min(loadout_shares):.0%}–{max(loadout_shares):.0%} | '
              f'{loadout_shares.count(0)}/{len(loadout_shares)} | {residual} |')


def report_contact_geometry(rows, tick_seconds, target, movement):
    """Bound the spacing from straight-line closure at stock and trial speeds."""
    spawn_path = WORKBOOK.parents[3] / 'Assets/_Project/Content/Configs/Population/SystemNpcSpawnPoints'
    radii = defaultdict(list)
    for asset in spawn_path.glob('system_npc_spawn_points_s??_*.asset'):
        level = int(asset.name.split('_')[4][1:])
        radius = next(float(line.split(':', 1)[1]) for line in asset.read_text().splitlines()
                      if line.startswith('  enemyRandomRadius:'))
        radii[level].append(radius)

    speed_multiplier = float(movement['speedMultiplier'])
    max_distance = 2 * float(movement['boundaryProtectionRadiusWorld'])
    print('\n| Ур. | Разброс группы макс., ед. | Разброс дальности врагов, ед. | Скорость врагов исходная / пробная | Нужный разрыв, с | Сближение не быстрее, ед./тик | Верхняя оценка между первым и третьим без манёвра, с: исходная / пробная |')
    print('|---:|---:|---:|---|---:|---:|---|')
    for level, _, player, _, _, runs in rows:
        radius = max(radii[level])
        span = hypot(2 * radius, radius * .25)
        outcomes = [(trio, target * high) for trios, _, high in runs for trio in trios]
        required = 0
        ranges = []
        stock = []
        for trio, factor in outcomes:
            limit = max(ceil(enemy.defense / player.damage_per_tick) for enemy in trio)
            required = max(required, next(gap for gap in range(limit + 1)
                                          if staggered_fight(player, trio, gap, factor)[0] <= .90))
            ranges.append(max(enemy.weapon_range for enemy in trio) - min(enemy.weapon_range for enemy in trio))
            stock.extend(enemy.speed for enemy in trio)
        trial = [110 - 5 * (rank - 1) for rank in range(max(1, level - 2), level + 1)]
        closure_distance_span = span + max(ranges)
        closing_ceiling = closure_distance_span / (2 * required)
        windows = []
        for speeds in (stock, trial):
            slow = min(speeds) * speed_multiplier
            fast = max(speeds) * speed_multiplier
            window = (closure_distance_span / slow + max_distance * (1 / slow - 1 / fast)) * tick_seconds
            windows.append(window)
        print(f'| {level} | {span:.0f} | {max(ranges):.0f} | {min(stock):g}–{max(stock):g} / '
              f'{min(trial)}–{max(trial)} | {required * tick_seconds:g} | {closing_ceiling:.1f} | '
              f'{windows[0]:.1f} / {windows[1]:.1f} |')

    print('\n| Ур. | Дальность полного залпа игрока / первого выстрела | Враг: первый выстрел | Враг: удержание дистанции |')
    print('|---:|---|---|---|')
    for level, _, player, _, _, runs in rows:
        enemies = [enemy for trios, _, _ in runs for trio in trios for enemy in trio]
        print(f'| {level} | {player.shortest_weapon_range:.0f} / {player.weapon_range:.0f} | '
              f'{min(e.weapon_range for e in enemies):.0f}–{max(e.weapon_range for e in enemies):.0f} | '
              f'{min(.9 * e.shortest_weapon_range for e in enemies):.0f}–{max(.9 * e.shortest_weapon_range for e in enemies):.0f} |')


def linked_row(sheet, cell):
    row_number = cell.rsplit("&range=", 1)[-1].lstrip("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
    return next(row for row in sheet if row["row"] == row_number)


def sorties_to_clear(capacity, interval, phase, cycle, kill_ticks):
    kills = clearance_kills(capacity, interval, phase, cycle, kill_ticks)
    return len(kills) if kills is not None else None


def clearance_kills(capacity, interval, phase, cycle, kill_ticks):
    alive = capacity
    seen = set()
    sorties = []
    while (alive, phase) not in seen:
        seen.add((alive, phase))
        kills = 0
        for second in range(1, cycle + 1):
            if (phase + second) % interval == 0:
                alive = min(capacity, alive + 1)
            if second in kill_ticks:
                alive -= 1
                kills += 1
                if alive == 0:
                    return (*sorties, kills)
        sorties.append(kills)
        phase = (phase + cycle) % interval
    return None


def route_geometry(sheets, route, engine_speed, *, reverse=False):
    systems = sheets["Системы"]
    hostile = linked_row(systems, route["F" if reverse else "E"])
    allied = linked_row(systems, route["E" if reverse else "F"])
    allied_portal = linked_row(sheets["Точки маршрутов"], route["R" if reverse else "S"])
    hostile_portal = linked_row(sheets["Точки маршрутов"], route["S" if reverse else "R"])
    allied_entry = tuple(number(allied_portal, column) for column in "HIJ")
    allied_exit = tuple(number(allied_portal, column) for column in "EFG")
    hostile_exit = tuple(number(hostile_portal, column) for column in "EFG")
    hostile_entry = tuple(number(hostile_portal, column) for column in "HIJ")
    movement = {row.get("B"): row.get("D") for row in sheets["ShipMovementConfig"]}
    tick_distance = engine_speed * float(movement["speedMultiplier"])
    # Use the system center as the illustrative engagement point and freeze the orbit at its initial phase.
    center = (0, 0, 0)
    hostile_ticks = ceil(dist(center, hostile_exit) / tick_distance) + ceil(dist(hostile_entry, center) / tick_distance)
    destinations = []
    for planet in sheets["Планеты"]:
        if not planet.get("D", "").endswith(f"&range=A{allied['row']}"):
            continue
        kind = linked_row(sheets["Тип планеты"], planet["F"])
        if kind.get("E") != "Да":
            continue
        orbit = linked_row(sheets["Орбиты планет"], planet["G"])
        angle = radians(number(orbit, "F"))
        radius = number(orbit, "E")
        point = (number(orbit, "H") + radius * cos(angle), number(orbit, "I") + radius * sin(angle), number(orbit, "J"))
        inbound = ceil(dist(allied_entry, point) / tick_distance)
        # During the takeoff tick at most the configured takeoff distance is covered.
        after_takeoff = max(0, dist(point, allied_exit) - float(movement["planetTakeoffDistance"]))
        outbound = 1 + ceil(after_takeoff / tick_distance)
        destinations.append((inbound + outbound, planet["B"]))
    if not destinations:
        return None
    allied_ticks, planet_name = min(destinations)
    return hostile, allied, planet_name, hostile_ticks, allied_ticks


def repair_route(sheets, ships, tick_seconds, combat_seconds):
    """Sector 10 benchmark with Black Star allied and Abyss Gate still captured."""
    route = next(row for row in sheets["Маршруты"] if row.get("A") == "route_abyss_gate_black_star_02")
    hostile, allied, planet_name, hostile_ticks, allied_ticks = route_geometry(
        sheets, route, ships["ally_ranger_L10_01"].speed,
    )
    travel_seconds = (hostile_ticks + allied_ticks) * tick_seconds
    print(f"\nРейс на ремонт: {hostile['B']} → {allied['B']} ({planet_name}) → {hostile['B']}.")
    print(f"Прямолинейная оценка при начальной фазе орбит: {travel_seconds:g} с, из них {hostile_ticks * tick_seconds:g} с во вражеской системе.")
    items = {row["A"]: row for row in sheets["ItemConfig"] if row.get("M", "").endswith(".asset")}
    fuel_price = number(items["item_fuel_cells_01"], "G")
    hull_repair_price = number(items["item_repair_01"], "G")
    round_trip_fuel = [2 * number(route, column) for column in "HQ"]
    print(f"Топливо за два прыжка, развитие 1 / 10: {round_trip_fuel[0]:g} / {round_trip_fuel[1]:g} ед. "
          f"при цене {fuel_price:g}: {round_trip_fuel[0] * fuel_price:g} / {round_trip_fuel[1] * fuel_price:g} кредитов.")
    print(f"Ремонт корпуса: {hull_repair_price:g} кредитов за единицу повреждения, оснащение оплачивается отдельно.")
    print("| Доп. действия, с | Полный цикл, с | Минимальный интервал для 3 побед, с | Максимум подкреплений при 45 с |")
    print("|---:|---:|---:|---:|")
    for overhead in (0, 15, 30, 45):
        cycle = combat_seconds + travel_seconds + overhead
        print(f"| {overhead} | {cycle:g} | {ceil(cycle / 2)} | {ceil(cycle / 45)} |")
    print("\n| Гарнизон | Цикл, с | Вылеты при 45 с | Вылеты при 60 с |")
    print("|---:|---:|---:|---:|")
    max_garrison = max(spawn_rows(sheets["EnemySpawnRuleConfig"], faction, 10)[2] for faction in FACTIONS)
    min_garrison = min(spawn_rows(sheets["EnemySpawnRuleConfig"], faction, 10)[1] for faction in FACTIONS)
    kill_ticks = tuple(ceil(combat_seconds * kill / 3) for kill in (1, 2, 3))
    for capacity in range(min_garrison, max_garrison + 1):
        for overhead in (15, 30, 45):
            cycle = ceil(combat_seconds + travel_seconds + overhead)
            intervals = []
            for interval in (45, 60):
                counts = [sorties_to_clear(capacity, interval, phase, cycle, kill_ticks) for phase in range(interval)]
                finished = [count for count in counts if count is not None]
                summary = f"{min(finished)}–{max(finished)}" if finished else "нет"
                if len(finished) != interval:
                    summary += f" (завершено {len(finished)}/{interval})"
                intervals.append(summary)
            print(f"| {capacity} | {cycle} | " + " | ".join(intervals) + " |")


def repair_route_scenarios(sheets, ships, tick_seconds):
    scenarios = defaultdict(list)
    systems = sheets["Системы"]
    sectors = sheets["Сектора"]
    for route in sheets["Маршруты"]:
        if route.get("G") != "Нет" or not route.get("A", "").startswith("route_"):
            continue
        endpoints = [linked_row(systems, route[column]) for column in "EF"]
        sector_levels = [int(number(linked_row(sectors, endpoint["D"]), "E")) for endpoint in endpoints]
        for index, level in enumerate(sector_levels):
            if sector_levels[1 - index] > level:
                continue
            speed = ships[f"ally_ranger_L{level:02}_01"].speed
            geometry = route_geometry(sheets, route, speed, reverse=index == 1)
            if geometry is None:
                continue
            hostile, allied, _, hostile_ticks, allied_ticks = geometry
            seconds = (hostile_ticks + allied_ticks) * tick_seconds
            scenarios[level].append((seconds, 2 * number(route, "H"), hostile["B"], allied["B"]))
    return scenarios


def survey_repair_routes(sheets, ships, tick_seconds, combat_by_level):
    scenarios = repair_route_scenarios(sheets, ships, tick_seconds)
    print("\n| Сектор | Направлений | Рейс на ремонт, с | Топливо за два прыжка, ед. | Интервал из Excel, с | Мин. интервал с запасом 45 с |")
    print("|---:|---:|---:|---:|---:|---:|")
    for level in range(1, 11):
        rows = scenarios[level]
        if not rows:
            print(f"| {level} | 0 | — | — | — | — |")
            continue
        seconds, fuel, _, _ = zip(*rows)
        initial_interval = min(spawn_rows(sheets["EnemySpawnRuleConfig"], faction, level)[0] for faction in FACTIONS)
        minimum = ceil((combat_by_level[level] + max(seconds) + 45) / 2)
        print(f"| {level} | {len(rows)} | {min(seconds):g}–{max(seconds):g} | {min(fuel):g}–{max(fuel):g} "
              f"| {initial_interval} | {minimum} |")


def main():
    sheets = read_sheets()
    ships = build_ships(sheets)
    tick = next(number(row, "D") for row in sheets["GameTimeConfig"] if row.get("B") == "secondsPerGameTick")
    print("| Ур. | 1 на 1: секунды до цели | 1 на 3: секунды до целей | 1 на 3: урон / запас | Гарнизон: мин–макс | Интервал, с | Запас на путь и ремонт при 3 победах, с |")
    print("|---:|---:|---:|---:|---:|---:|---:|")
    pressure_thresholds = {}
    separation_results = []
    tuning_windows = []
    combat_scenarios = {}
    for level in range(1, 11):
        player = ships[f"ally_ranger_L{level:02}_01"]
        duels, groups, separated, populations = [], [], [], []
        windows = []
        for faction in FACTIONS:
            def enemy(enemy_level):
                return ships[f"enemy_{faction}_L{enemy_level:02}_01"]

            duels.append(fight(player, [enemy(level)]))
            first_trio = len(groups)
            scenarios = reference_trios(ships, level, faction)
            combat_scenarios[level, faction] = (player, scenarios)
            for opponents in scenarios:
                groups.append(fight(player, opponents))
                separated.append(fight(player, opponents, separate=True))
            group_floor = min(ratio for ratio, _ in groups[first_trio:])
            separated_ceiling = max(ratio for ratio, _ in separated[first_trio:])
            windows.append((100 / group_floor, 100 / separated_ceiling))
            populations.append(spawn_rows(sheets["EnemySpawnRuleConfig"], faction, level))
        interval = min(seconds for seconds, _, _ in populations)
        longest = max(ticks for _, ticks in groups) * tick
        pressure_thresholds[level] = 100 / max(ratio for ratio, _ in groups)
        separation_results.append((level, [ratio for ratio, _ in separated]))
        tuning_windows.append((level, windows))
        print(f"| {level} | {min(t for _, t in duels) * tick:g}–{max(t for _, t in duels) * tick:g} "
              f"| {min(t for _, t in groups) * tick:g}–{longest:g} "
              f"| {min(r for r, _ in groups):.2f}–{max(r for r, _ in groups):.2f} "
              f"| {min(low for _, low, _ in populations)}–{max(high for _, _, high in populations)} "
              f"| {interval} | ≤{2 * interval - longest:g} |")
    for name in ("ally_ranger", *(f"enemy_{faction}" for faction in FACTIONS)):
        print(f"Скорость {name}: {ships[f'{name}_L01_01'].speed:g} → {ships[f'{name}_L10_01'].speed:g}")
    print("Доля исходного входящего урона на границе выживания для самой тяжёлой тройки:")
    for level, threshold in pressure_thresholds.items():
        print(f"Уровень {level}: {threshold:.1f}%")
    print("\n| Ур. | Урон при разделении / запас | Побед / сценариев при разделении |")
    print("|---:|---:|---:|")
    for level, ratios in separation_results:
        wins = sum(ratio < 1 for ratio in ratios)
        print(f"| {level} | {min(ratios):.2f}–{max(ratios):.2f} | {wins}/{len(ratios)} |")
    print("\n| Ур. | ИИ | Древние | Заражённые | Общий диапазон |")
    print("|---:|---:|---:|---:|---:|")
    for level, windows in tuning_windows:
        common = (max(low for low, _ in windows), min(high for _, high in windows))
        intervals = [f"[{low:.1f}%; {high:.1f}%)" if low < high else "нет" for low, high in (*windows, common)]
        print(f"| {level} | " + " | ".join(intervals) + " |")

    # Normalize the hardest separated scenario for each faction and level to one target.
    # Choose the midpoint between the required floor and the survival boundary.
    target_floor = max(low / high for _, windows in tuning_windows for low, high in windows)
    if target_floor >= 1:
        print("Общая цель по остаточной прочности недостижима с одним коэффициентом на пару уровень–фракция.")
        return
    target = mean((target_floor, 1))
    print(f"\nПробная калибровка: {100 * (1 - target):.1f}% прочности после самой тяжёлой раздельной тройки.")
    print("| Ур. | Доля исходного урона ИИ | Доля исходного урона Древних | Доля исходного урона Заражённых |")
    print("|---:|---:|---:|---:|")
    maneuver_results = []
    for level, windows in tuning_windows:
        factors = [target * high for _, high in windows]
        assert all(low < factor < high for factor, (low, high) in zip(factors, windows))
        print(f"| {level} | " + " | ".join(f"{factor:.1f}%" for factor in factors) + " |")
        maneuvers = []
        for faction, factor in zip(FACTIONS, factors):
            player, scenarios = combat_scenarios[level, faction]
            for opponents in scenarios:
                assert staggered_fight(player, opponents, 0, factor / 100)[0] >= 1
            runs = [minimum_surviving_stagger(player, opponents, factor / 100) for opponents in scenarios]
            maneuvers.append(max(stagger for stagger, _ in runs) * tick)
        maneuver_results.append((level, maneuvers))

    print("\n| Ур. | ИИ: разрыв, с | Древние: разрыв, с | Заражённые: разрыв, с |")
    print("|---:|---:|---:|---:|")
    for level, maneuvers in maneuver_results:
        print(f"| {level} | " + " | ".join(f"{gap:g}" for gap in maneuvers) + " |")
    movement = {row.get('B'): row.get('D') for row in sheets['ShipMovementConfig']}
    combat_by_level = longer_battles(ships, combat_scenarios, tick, movement=movement)
    if combat_by_level is not None:
        repair_route(sheets, ships, tick, combat_by_level[10])
        survey_repair_routes(sheets, ships, tick, combat_by_level)


if __name__ == "__main__":
    main()
