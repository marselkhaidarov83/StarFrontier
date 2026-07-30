#!/usr/bin/env python3
"""
STAR FRONTIER — generator of Sprint 3 controlled-integration manifests.

Place this file at:
Tools/Integration/create_sprint3_integration_manifests.py

Run from the project root:
python3 Tools/Integration/create_sprint3_integration_manifests.py

The script does NOT merge, delete, checkout, or classify files automatically.
It only captures exact Git refs, creates an inventory, and prepares manifest templates.
"""

from __future__ import annotations

import csv
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


SOURCE_BRANCH = "stage-2a-sprint-3-2026.06.24"
MAIN_BRANCH = "main"


@dataclass(frozen=True)
class Package:
    package_id: str
    name: str
    scope: str
    dependencies: str
    blockers: str


PACKAGES = (
    Package(
        "M-01",
        "Управление",
        "Player control intent, единый InputActions asset, click/tap routing, input bridge.",
        "Архитектурное ядро Sprint 1; Config/State/Service; действующее решение без виртуального стика.",
        "Не включать movement, camera, HUD или Routes/Travel случайно.",
    ),
    Package(
        "M-02",
        "Движение",
        "Ship movement service, acceleration/deceleration/rotation, bounds and final movement parameters.",
        "M-01; ShipMovementConfig; ShipConfig/ShipFinalStats mapping.",
        "Источник финальных параметров и lifecycle SystemBounds должны быть однозначны.",
    ),
    Package(
        "M-03",
        "Сохранения",
        "SaveVersion/migration and persistence of ship position, direction and Sprint 3 persistent state.",
        "M-01; M-02; working SaveService and migration rules.",
        "Нужны migration/round-trip checks; runtime target/camera state не сохранять.",
    ),
    Package(
        "M-04",
        "Камера",
        "One production camera ownership path, follow/free-look/recenter and camera state binding.",
        "M-01; M-02; SystemCameraConfig.",
        "OPEN-M04: единственный production owner камеры должен быть явно выбран до интеграции.",
    ),
    Package(
        "M-05",
        "HUD и взаимодействия",
        "HUD binding, fuel/target/interaction presentation, touch arbitration and input-through protection.",
        "M-01…M-04; Fuel/Target/Interaction services and states.",
        "Production asset variants and required scene references must be selected and bound.",
    ),
    Package(
        "M-06",
        "Pseudo-3D",
        "Layered backgrounds, parallax, ship/planet/station presentation, shadows and engine FX.",
        "M-04; M-05; approved SF_05 art selection.",
        "Production art variants, trail material/texture and Android budget must be resolved.",
    ),
    Package(
        "M-07",
        "QA и диагностика",
        "Validators, debug HUD/commands, tests, reports and exact-SHA evidence plumbing.",
        "M-01…M-06.",
        "Package must not contain accidental gameplay changes.",
    ),
    Package(
        "M-08",
        "Routes/Travel — условный пакет",
        "RouteEndpoint schema, route entry/exit assignment, preview/travel/fuel/time/save transaction.",
        "Sprint 2 route graph; M-05 fuel/interaction; explicit owner decision.",
        "OPEN-M08: INCLUDED or EXCLUDED must be explicitly decided. Do not transfer automatically.",
    ),
)


def run_git(root: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", *args],
        cwd=root,
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} failed:\n{completed.stderr.strip()}"
        )
    return completed.stdout.strip()


def find_project_root() -> Path:
    here = Path(__file__).resolve()
    candidate = here.parents[2]
    try:
        root = Path(run_git(candidate, "rev-parse", "--show-toplevel"))
    except Exception as exc:
        raise SystemExit(
            "Не удалось найти Git-репозиторий. "
            "Положите файл в Tools/Integration внутри проекта.\n"
            f"Причина: {exc}"
        ) from exc
    return root


def parse_name_status(text: str) -> list[tuple[str, str]]:
    rows: list[tuple[str, str]] = []
    for raw in text.splitlines():
        if not raw.strip():
            continue
        parts = raw.split("\t")
        status = parts[0]
        path = parts[-1]
        rows.append((status, path))
    return rows


def write_manifest(
    path: Path,
    package: Package,
    source_sha: str,
    main_sha: str,
) -> None:
    decision = (
        "HOLD — требуется явное решение владельца"
        if package.package_id == "M-08"
        else "PREPARED_NOT_INTEGRATED"
    )

    path.write_text(
        f"""# {package.package_id} — {package.name}

## 1. Статус
- Package status: `{decision}`
- Канонический статус: `NOT_INTEGRATED`
- Source branch: `{SOURCE_BRANCH}`
- Source SHA: `{source_sha}`
- Base branch: `{MAIN_BRANCH}`
- Base SHA: `{main_sha}`
- Package commit SHA: `ЗАПОЛНИТЬ ПОСЛЕ СОЗДАНИЯ COMMIT`

## 2. Назначение
{package.scope}

## 3. Зависимости
{package.dependencies}

## 4. Блокеры и обязательные решения
{package.blockers}

## 5. Включаемые файлы
Заполнять только после ручной классификации `FILE_CLASSIFICATION.csv`.

| Статус Git | Путь | Причина включения | Связанная задача |
|---|---|---|---|
|  |  |  |  |

## 6. Явно исключённые файлы
| Путь | Причина исключения | В какой пакет перенесён |
|---|---|---|
|  |  |  |

## 7. Сцены, префабы, Config и графика
| Объект | Точный путь | Binding / выбранная версия | SF-ART / решение |
|---|---|---|---|
|  |  |  |  |

## 8. Минимальный gate пакета
- [ ] Unity завершила компиляцию без красных ошибок.
- [ ] `STAR FRONTIER → Проверки Sprint 3 → Полная проверка` выполнена.
- [ ] Результат проверки сохранён с branch + exact SHA.
- [ ] В `git diff --name-status` нет файлов соседних пакетов.
- [ ] Нет `.DS_Store`, `.utmp` и других локальных файлов.
- [ ] Известные блокеры пакета закрыты либо пакет не интегрируется.

## 9. Rollback
До merge:
```bash
git switch integration/2a-sprints-1-3
git branch -D package/{package.package_id.lower()}-REPLACE-NAME
```

После merge пакета в integration-ветку:
```bash
git switch integration/2a-sprints-1-3
git revert -m 1 <MERGE_COMMIT_SHA>
```

## 10. Evidence
- Validator report:
- Test artifact:
- Manual Unity result:
- UA:
- Android/device:
- Performance/soak:
- Notes:
""",
        encoding="utf-8",
    )


def main() -> None:
    root = find_project_root()
    output = root / "Documentation" / "Sprint3" / "IntegrationPackages"
    output.mkdir(parents=True, exist_ok=True)

    source_sha = run_git(root, "rev-parse", SOURCE_BRANCH)
    main_sha = run_git(root, "rev-parse", MAIN_BRANCH)
    current_branch = run_git(root, "branch", "--show-current")
    status = run_git(root, "status", "--short")

    diff = run_git(
        root,
        "diff",
        "--name-status",
        f"{MAIN_BRANCH}...{SOURCE_BRANCH}",
    )
    rows = parse_name_status(diff)

    (output / "ALL_CHANGED_FILES.txt").write_text(
        diff + ("\n" if diff else ""),
        encoding="utf-8",
    )

    with (output / "FILE_CLASSIFICATION.csv").open(
        "w",
        newline="",
        encoding="utf-8",
    ) as stream:
        writer = csv.writer(stream)
        writer.writerow(
            [
                "git_status",
                "path",
                "package",
                "include_yes_no",
                "reason",
                "dependency_or_blocker",
            ]
        )
        for git_status, path in rows:
            writer.writerow([git_status, path, "", "", "", ""])

    for package in PACKAGES:
        safe_name = (
            package.name.lower()
            .replace(" ", "_")
            .replace("/", "_")
            .replace("—", "")
        )
        write_manifest(
            output / f"{package.package_id}_{safe_name}.md",
            package,
            source_sha,
            main_sha,
        )

    (output / "INTEGRATION_ORDER.md").write_text(
        f"""# Sprint 3 — порядок controlled integration

## Зафиксированные ссылки
- Current branch at manifest generation: `{current_branch}`
- Working source: `{SOURCE_BRANCH}` @ `{source_sha}`
- Canonical base: `{MAIN_BRANCH}` @ `{main_sha}`
- Working tree at generation: `{'CLEAN' if not status else 'DIRTY — см. ниже'}`

```text
{status or 'clean'}
```

## Порядок подготовки
1. M-01 — Управление
2. M-02 — Движение
3. M-03 — Сохранения
4. M-04 — Камера
5. M-05 — HUD и взаимодействия
6. M-06 — Pseudo-3D
7. M-07 — QA и диагностика
8. M-08 — HOLD до явного решения владельца

## Важное ограничение
Этот порядок готовит integration-ветку. Он не разрешает автоматически OPEN-M04,
OPEN-M08 или выбор production-графики и не меняет статусы SF_03.
Merge в `main` является отдельной задачей 2A-S03-05-T20.
""",
        encoding="utf-8",
    )

    (output / "M-08_DECISION_REQUIRED.md").write_text(
        f"""# OPEN-M08 — решение владельца обязательно

## Player-facing вопрос
Должен ли Sprint 3 включать полноценный перелёт между системами
с RouteEndpoint, стоимостью топлива, игровым временем, Save и восстановлением,
или Sprint 3 заканчивается управлением кораблём внутри системы?

## Вариант A — INCLUDED
M-08 создаётся как отдельный change set. В него входят:
- RouteEndpoint schema;
- назначение входа/выхода каждого Route;
- preview и фактический TravelService;
- единая транзакция fuel/time/save;
- validator и route/travel regression.

## Вариант B — EXCLUDED
M-08 исключается из Sprint 3:
- его файлы не попадают в M-01…M-07;
- рабочая реализация сохраняется в отдельной ветке;
- решение фиксируется в SF_04;
- SF_03 указывает перенос за пределы Sprint 3.

## Зафиксировать
- Решение: `INCLUDED / EXCLUDED`
- Дата:
- Кем подтверждено:
- SF-DEC:
- Source SHA: `{source_sha}`
""",
        encoding="utf-8",
    )

    (output / "PACKAGE_BRANCH_COMMANDS.md").write_text(
        """# Команды создания одного package change set

Замените `<ID>` и `<NAME>` на пакет, например `m01` и `player-control`.

```bash
git fetch origin
git switch integration/2a-sprints-1-3
git pull --ff-only
git switch -c package/<ID>-<NAME>

# Добавляйте только файлы, уже занесённые в manifest:
git checkout stage-2a-sprint-3-2026.06.24 -- <PATH_1> <PATH_2>

git status --short
git diff --name-status
git add <PATH_1> <PATH_2>
git commit -m "<M-XX>: <package name>"
git rev-parse HEAD
```

После локального gate:

```bash
git switch integration/2a-sprints-1-3
git merge --no-ff package/<ID>-<NAME> -m "Integrate <M-XX>: <package name>"
```

Не используйте `git add .` для integration package.
""",
        encoding="utf-8",
    )

    print(f"Готово: {output}")
    print(f"Изменённых путей main...working: {len(rows)}")
    print("Автоматическая классификация файлов НЕ выполнялась.")


if __name__ == "__main__":
    main()
