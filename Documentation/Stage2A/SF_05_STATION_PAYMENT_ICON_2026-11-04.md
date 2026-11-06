# SF_05 — общая иконка астралита для оплаты строительства

Дата: 04.11.2026. Основание: владелец запросил общую иконку астралита и утвердил второй макет формы оплаты с синим выбранным вариантом. Это не подтверждение иконки в игре или Android.

- Файл проекта: `Assets/Resources/icon_astralite_generic_01.png` + .meta, новый ассет без замены прежних видовых иконок.
- Изображение: 1254×1254, PNG с alpha; sips подтвердил hasAlpha=yes.
- Unity: TextureImporter Sprite Single (textureType=8, spriteMode=1), alphaIsTransparency=1. Параметры основаны на существующей иконке астралита, GUID и spriteID новые.
- Binding: StationConstructionPaymentPanel2A.CreateResource; Resources.Load<Sprite>("icon_astralite_generic_01").
- Built-in image_gen, transparent_background=true; CLI/API fallback не использовался.
- Исходный результат генерации: `/Users/marselhaidarov/.codex/generated_images/01a19cb2-39b2-7132-9da4-be8e6d42f28a/exec-02874fa1-1477-4b95-b880-18ce2b505a00.png`.
- Статус: ассет и binding внесены; Unity import/визуальная проверка агентом не запускались, отдельного UA по новому ассету нет.

## Полный промпт

Create ONE isolated game inventory icon for STAR FRONTIER, a generic astralite mineral. Compact cluster of 4 angular faceted crystals, one medium tall crystal and three smaller shards, luminous blue-violet mineral with cool cyan highlights and dark violet shadow facets. Polished hand-painted 2D sci-fi mobile game resource sprite, similar to clear faceted cyan diamond currency icons, crisp readable silhouette at 64 pixels. Three-quarter view, centered cluster filling about 78% of a square canvas with equal safe margins. Subtle internal glow only, no exterior large bloom. Transparent background with true alpha, no floor, no shadow plane, no frame, no text, no numbers, no UI, no species emblem, no border, no checkerboard pattern. This is a production resource sprite matching the blue-purple crystal shown in an approved construction payment UI mockup.
