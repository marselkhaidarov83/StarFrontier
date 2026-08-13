#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Sprint3PlaceholderArtGenerator
{
    private const string OutputFolder =
        "Assets/Art/Sprint3/Placeholders";

    private enum PlaceholderKind
    {
        PlayerShip,
        Sun,
        Planet,
        Station,
        TargetMarker,
        InteractionMarker,
        ShipShadow,
        EngineGlow,
        BackgroundFar,
        BackgroundMid,
        BackgroundNear,
        JoystickBase,
        JoystickHandle,
        InteractButton,
        RecenterButton,
        HudPanel,
        HudBarFill
    }

    private readonly struct PlaceholderSpec
    {
        public readonly string FileName;
        public readonly int Width;
        public readonly int Height;
        public readonly PlaceholderKind Kind;
        public readonly bool IsUi;
        public readonly bool UseMipMaps;
        public readonly bool Repeat;

        public PlaceholderSpec(
            string fileName,
            int width,
            int height,
            PlaceholderKind kind,
            bool isUi,
            bool useMipMaps = false,
            bool repeat = false)
        {
            FileName = fileName;
            Width = width;
            Height = height;
            Kind = kind;
            IsUi = isUi;
            UseMipMaps = useMipMaps;
            Repeat = repeat;
        }
    }

    private static readonly PlaceholderSpec[] Specs =
    {
        new("PH_PlayerShip.png", 512, 512,
            PlaceholderKind.PlayerShip, false),

        new("PH_Sun.png", 512, 512,
            PlaceholderKind.Sun, false),

        new("PH_Planet.png", 512, 512,
            PlaceholderKind.Planet, false),

        new("PH_Station.png", 512, 512,
            PlaceholderKind.Station, false),

        new("PH_TargetMarker.png", 256, 256,
            PlaceholderKind.TargetMarker, false),

        new("PH_InteractionMarker.png", 256, 256,
            PlaceholderKind.InteractionMarker, false),

        new("PH_ShipShadow.png", 256, 256,
            PlaceholderKind.ShipShadow, false),

        new("PH_EngineGlow.png", 256, 256,
            PlaceholderKind.EngineGlow, false),

        new("PH_BackgroundFar.png", 1024, 1024,
            PlaceholderKind.BackgroundFar, false, true, true),

        new("PH_BackgroundMid.png", 1024, 1024,
            PlaceholderKind.BackgroundMid, false, true, true),

        new("PH_BackgroundNear.png", 1024, 1024,
            PlaceholderKind.BackgroundNear, false, true, true),

        new("PH_JoystickBase.png", 256, 256,
            PlaceholderKind.JoystickBase, true),

        new("PH_JoystickHandle.png", 256, 256,
            PlaceholderKind.JoystickHandle, true),

        new("PH_InteractButton.png", 256, 256,
            PlaceholderKind.InteractButton, true),

        new("PH_RecenterButton.png", 256, 256,
            PlaceholderKind.RecenterButton, true),

        new("PH_HudPanel.png", 512, 256,
            PlaceholderKind.HudPanel, true),

        new("PH_HudBarFill.png", 512, 64,
            PlaceholderKind.HudBarFill, true)
    };

    [MenuItem(
        "STAR FRONTIER/Art/Sprint 3/Generate Placeholder Art")]
    private static void GeneratePlaceholderArt()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Generate Sprint 3 Placeholder Art",
            "Временные PH-файлы будут созданы или перезаписаны. " +
            "Существующая графика вне папки Placeholders не изменится.",
            "Создать",
            "Отмена");

        if (!confirmed)
            return;

        EnsureFolder(OutputFolder);

        try
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                PlaceholderSpec spec = Specs[i];

                EditorUtility.DisplayProgressBar(
                    "Sprint 3 Placeholder Art",
                    spec.FileName,
                    (float)i / Specs.Length);

                CreateTexture(spec);
            }

            AssetDatabase.Refresh();

            foreach (PlaceholderSpec spec in Specs)
            {
                string assetPath =
                    $"{OutputFolder}/{spec.FileName}";

                ConfigureImporter(assetPath, spec);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Sprint3PlaceholderArtGenerator] " +
                $"Создано временных изображений: {Specs.Length}");

            EditorUtility.RevealInFinder(OutputFolder);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Ошибка",
                "Не удалось создать временные изображения. " +
                "Откройте Console и скопируйте первую ошибку.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateTexture(PlaceholderSpec spec)
    {
        Color32[] pixels =
            new Color32[spec.Width * spec.Height];

        Clear(pixels);

        DrawPlaceholder(
            pixels,
            spec.Width,
            spec.Height,
            spec.Kind);

        Texture2D texture = new Texture2D(
            spec.Width,
            spec.Height,
            TextureFormat.RGBA32,
            false);

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        byte[] png = texture.EncodeToPNG();

        UnityEngine.Object.DestroyImmediate(texture);

        string assetPath =
            $"{OutputFolder}/{spec.FileName}";

        string absolutePath =
            ToAbsolutePath(assetPath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException(
                "Не удалось определить папку изображения."));

        File.WriteAllBytes(absolutePath, png);

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureImporter(
    string assetPath,
    PlaceholderSpec spec)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError(
                $"Не найден TextureImporter: {assetPath}");

            return;
        }

        // Основные параметры импорта.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.spritePixelsPerUnit = 100f;

        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = spec.UseMipMaps;
        importer.filterMode = FilterMode.Bilinear;

        importer.wrapMode = spec.Repeat
            ? TextureWrapMode.Repeat
            : TextureWrapMode.Clamp;

        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.maxTextureSize =
            Mathf.NextPowerOfTwo(
                Mathf.Max(spec.Width, spec.Height));

        importer.isReadable = false;

        // В Unity 6000.3 spriteAlignment и spriteMeshType
        // задаются через TextureImporterSettings.
        TextureImporterSettings settings =
            new TextureImporterSettings();

        importer.ReadTextureSettings(settings);

        settings.spriteAlignment =
            (int)SpriteAlignment.Center;

        settings.spriteMeshType =
            SpriteMeshType.FullRect;

        settings.spritePivot =
            new Vector2(0.5f, 0.5f);

        settings.spritePixelsPerUnit = 100f;

        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static void DrawPlaceholder(
        Color32[] pixels,
        int width,
        int height,
        PlaceholderKind kind)
    {
        int centerX = width / 2;
        int centerY = height / 2;

        switch (kind)
        {
            case PlaceholderKind.PlayerShip:
                FillTriangle(
                    pixels, width, height,
                    new Vector2Int(centerX, height - 55),
                    new Vector2Int(65, 65),
                    new Vector2Int(width - 65, 65),
                    new Color32(40, 190, 230, 255));

                FillTriangle(
                    pixels, width, height,
                    new Vector2Int(centerX, height - 105),
                    new Vector2Int(145, 105),
                    new Vector2Int(width - 145, 105),
                    new Color32(15, 65, 100, 255));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY + 55, 42,
                    new Color32(180, 245, 255, 255));

                FillRect(
                    pixels, width, height,
                    centerX - 42, 30,
                    84, 55,
                    new Color32(255, 150, 40, 255));
                break;

            case PlaceholderKind.Sun:
                DrawRadialCircle(
                    pixels, width, height,
                    centerX, centerY,
                    width / 2 - 20,
                    new Color32(255, 170, 30, 230));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    width / 3,
                    new Color32(255, 225, 90, 255));
                break;

            case PlaceholderKind.Planet:
                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    width / 2 - 28,
                    new Color32(50, 115, 180, 255));

                FillEllipse(
                    pixels, width, height,
                    centerX - 70, centerY + 30,
                    90, 38,
                    new Color32(65, 180, 120, 255));

                FillEllipse(
                    pixels, width, height,
                    centerX + 95, centerY - 75,
                    65, 30,
                    new Color32(70, 165, 110, 255));

                DrawRing(
                    pixels, width, height,
                    centerX, centerY,
                    width / 2 - 28,
                    10,
                    new Color32(160, 220, 255, 210));
                break;

            case PlaceholderKind.Station:
                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    175,
                    new Color32(120, 135, 155, 255));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    105,
                    new Color32(0, 0, 0, 0));

                FillRect(
                    pixels, width, height,
                    centerX - 225, centerY - 28,
                    450, 56,
                    new Color32(180, 195, 210, 255));

                FillRect(
                    pixels, width, height,
                    centerX - 28, centerY - 225,
                    56, 450,
                    new Color32(180, 195, 210, 255));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    45,
                    new Color32(70, 220, 255, 255));
                break;

            case PlaceholderKind.TargetMarker:
                DrawCornerFrame(
                    pixels, width, height,
                    new Color32(80, 220, 255, 255));
                break;

            case PlaceholderKind.InteractionMarker:
                DrawDiamond(
                    pixels, width, height,
                    centerX, centerY,
                    82, 8,
                    new Color32(255, 205, 55, 255));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    13,
                    new Color32(255, 240, 150, 255));
                break;

            case PlaceholderKind.ShipShadow:
                FillEllipse(
                    pixels, width, height,
                    centerX, centerY,
                    95, 42,
                    new Color32(0, 0, 0, 105));
                break;

            case PlaceholderKind.EngineGlow:
                DrawRadialCircle(
                    pixels, width, height,
                    centerX, centerY,
                    110,
                    new Color32(30, 180, 255, 220));
                break;

            case PlaceholderKind.BackgroundFar:
                FillAll(
                    pixels,
                    new Color32(3, 8, 24, 255));

                DrawStars(
                    pixels, width, height,
                    480, 12345,
                    new Color32(185, 215, 255, 255),
                    1, 2);
                break;

            case PlaceholderKind.BackgroundMid:
                DrawRadialCircle(
                    pixels, width, height,
                    width / 3, height / 2,
                    340,
                    new Color32(45, 80, 180, 85));

                DrawRadialCircle(
                    pixels, width, height,
                    width * 3 / 4, height / 3,
                    280,
                    new Color32(130, 55, 175, 70));
                break;

            case PlaceholderKind.BackgroundNear:
                DrawStars(
                    pixels, width, height,
                    110, 67890,
                    new Color32(215, 235, 255, 180),
                    2, 5);
                break;

            case PlaceholderKind.JoystickBase:
                DrawRing(
                    pixels, width, height,
                    centerX, centerY,
                    106, 16,
                    new Color32(80, 190, 230, 150));

                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    82,
                    new Color32(25, 55, 85, 80));
                break;

            case PlaceholderKind.JoystickHandle:
                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    78,
                    new Color32(80, 205, 240, 210));

                DrawRing(
                    pixels, width, height,
                    centerX, centerY,
                    78, 8,
                    new Color32(190, 245, 255, 255));
                break;

            case PlaceholderKind.InteractButton:
                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    105,
                    new Color32(20, 90, 125, 210));

                DrawDiamond(
                    pixels, width, height,
                    centerX, centerY,
                    48, 9,
                    new Color32(255, 220, 70, 255));
                break;

            case PlaceholderKind.RecenterButton:
                FillCircle(
                    pixels, width, height,
                    centerX, centerY,
                    105,
                    new Color32(20, 90, 125, 210));

                DrawRing(
                    pixels, width, height,
                    centerX, centerY,
                    52, 9,
                    new Color32(210, 245, 255, 255));

                DrawLine(
                    pixels, width, height,
                    centerX, centerY + 78,
                    centerX, centerY + 40,
                    7,
                    new Color32(210, 245, 255, 255));
                break;

            case PlaceholderKind.HudPanel:
                FillRect(
                    pixels, width, height,
                    4, 4,
                    width - 8, height - 8,
                    new Color32(8, 28, 48, 205));

                DrawRectBorder(
                    pixels, width, height,
                    4, 4,
                    width - 8, height - 8,
                    6,
                    new Color32(60, 180, 220, 230));
                break;

            case PlaceholderKind.HudBarFill:
                FillRect(
                    pixels, width, height,
                    4, 4,
                    width - 8, height - 8,
                    new Color32(40, 205, 125, 255));

                DrawRectBorder(
                    pixels, width, height,
                    4, 4,
                    width - 8, height - 8,
                    4,
                    new Color32(190, 255, 225, 255));
                break;
        }
    }

    private static void Clear(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
    }

    private static void FillAll(
        Color32[] pixels,
        Color32 color)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
    }

    private static void SetPixel(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        Color32 color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        pixels[y * width + x] = color;
    }

    private static void BlendPixel(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        Color32 source)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int index = y * width + x;
        Color32 destination = pixels[index];

        float sourceAlpha = source.a / 255f;
        float destinationAlpha = destination.a / 255f;

        float outputAlpha =
            sourceAlpha +
            destinationAlpha * (1f - sourceAlpha);

        if (outputAlpha <= 0f)
        {
            pixels[index] = new Color32(0, 0, 0, 0);
            return;
        }

        byte r = (byte)Mathf.Clamp(
            (source.r * sourceAlpha +
             destination.r * destinationAlpha *
             (1f - sourceAlpha)) / outputAlpha,
            0f, 255f);

        byte g = (byte)Mathf.Clamp(
            (source.g * sourceAlpha +
             destination.g * destinationAlpha *
             (1f - sourceAlpha)) / outputAlpha,
            0f, 255f);

        byte b = (byte)Mathf.Clamp(
            (source.b * sourceAlpha +
             destination.b * destinationAlpha *
             (1f - sourceAlpha)) / outputAlpha,
            0f, 255f);

        pixels[index] = new Color32(
            r, g, b,
            (byte)(outputAlpha * 255f));
    }

    private static void FillRect(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        int rectWidth,
        int rectHeight,
        Color32 color)
    {
        for (int currentY = y;
             currentY < y + rectHeight;
             currentY++)
        {
            for (int currentX = x;
                 currentX < x + rectWidth;
                 currentX++)
            {
                SetPixel(
                    pixels, width, height,
                    currentX, currentY, color);
            }
        }
    }

    private static void DrawRectBorder(
        Color32[] pixels,
        int width,
        int height,
        int x,
        int y,
        int rectWidth,
        int rectHeight,
        int thickness,
        Color32 color)
    {
        FillRect(
            pixels, width, height,
            x, y, rectWidth, thickness, color);

        FillRect(
            pixels, width, height,
            x, y + rectHeight - thickness,
            rectWidth, thickness, color);

        FillRect(
            pixels, width, height,
            x, y, thickness, rectHeight, color);

        FillRect(
            pixels, width, height,
            x + rectWidth - thickness, y,
            thickness, rectHeight, color);
    }

    private static void FillCircle(
        Color32[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        Color32 color)
    {
        int radiusSquared = radius * radius;

        for (int y = centerY - radius;
             y <= centerY + radius;
             y++)
        {
            for (int x = centerX - radius;
                 x <= centerX + radius;
                 x++)
            {
                int deltaX = x - centerX;
                int deltaY = y - centerY;

                if (deltaX * deltaX +
                    deltaY * deltaY <= radiusSquared)
                {
                    SetPixel(
                        pixels, width, height,
                        x, y, color);
                }
            }
        }
    }

    private static void FillEllipse(
        Color32[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radiusX,
        int radiusY,
        Color32 color)
    {
        for (int y = centerY - radiusY;
             y <= centerY + radiusY;
             y++)
        {
            for (int x = centerX - radiusX;
                 x <= centerX + radiusX;
                 x++)
            {
                float normalizedX =
                    (x - centerX) / (float)radiusX;

                float normalizedY =
                    (y - centerY) / (float)radiusY;

                if (normalizedX * normalizedX +
                    normalizedY * normalizedY <= 1f)
                {
                    SetPixel(
                        pixels, width, height,
                        x, y, color);
                }
            }
        }
    }

    private static void DrawRing(
        Color32[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        int thickness,
        Color32 color)
    {
        int outerSquared = radius * radius;
        int inner = Mathf.Max(0, radius - thickness);
        int innerSquared = inner * inner;

        for (int y = centerY - radius;
             y <= centerY + radius;
             y++)
        {
            for (int x = centerX - radius;
                 x <= centerX + radius;
                 x++)
            {
                int deltaX = x - centerX;
                int deltaY = y - centerY;
                int distanceSquared =
                    deltaX * deltaX + deltaY * deltaY;

                if (distanceSquared <= outerSquared &&
                    distanceSquared >= innerSquared)
                {
                    SetPixel(
                        pixels, width, height,
                        x, y, color);
                }
            }
        }
    }

    private static void DrawRadialCircle(
        Color32[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        Color32 color)
    {
        for (int y = centerY - radius;
             y <= centerY + radius;
             y++)
        {
            for (int x = centerX - radius;
                 x <= centerX + radius;
                 x++)
            {
                float deltaX = x - centerX;
                float deltaY = y - centerY;
                float distance =
                    Mathf.Sqrt(deltaX * deltaX +
                               deltaY * deltaY);

                if (distance > radius)
                    continue;

                float strength =
                    1f - distance / radius;

                Color32 current = color;
                current.a = (byte)(
                    color.a * strength * strength);

                BlendPixel(
                    pixels, width, height,
                    x, y, current);
            }
        }
    }

    private static void FillTriangle(
        Color32[] pixels,
        int width,
        int height,
        Vector2Int a,
        Vector2Int b,
        Vector2Int c,
        Color32 color)
    {
        int minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        int maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        int minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        int maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int point = new Vector2Int(x, y);

                bool hasNegative =
                    Sign(point, a, b) < 0 ||
                    Sign(point, b, c) < 0 ||
                    Sign(point, c, a) < 0;

                bool hasPositive =
                    Sign(point, a, b) > 0 ||
                    Sign(point, b, c) > 0 ||
                    Sign(point, c, a) > 0;

                if (!(hasNegative && hasPositive))
                {
                    SetPixel(
                        pixels, width, height,
                        x, y, color);
                }
            }
        }
    }

    private static int Sign(
        Vector2Int point1,
        Vector2Int point2,
        Vector2Int point3)
    {
        return (point1.x - point3.x) *
               (point2.y - point3.y) -
               (point2.x - point3.x) *
               (point1.y - point3.y);
    }

    private static void DrawLine(
        Color32[] pixels,
        int width,
        int height,
        int x0,
        int y0,
        int x1,
        int y1,
        int thickness,
        Color32 color)
    {
        int deltaX = Mathf.Abs(x1 - x0);
        int stepX = x0 < x1 ? 1 : -1;
        int deltaY = -Mathf.Abs(y1 - y0);
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX + deltaY;

        while (true)
        {
            FillCircle(
                pixels, width, height,
                x0, y0,
                Mathf.Max(1, thickness / 2),
                color);

            if (x0 == x1 && y0 == y1)
                break;

            int doubledError = 2 * error;

            if (doubledError >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }

            if (doubledError <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private static void DrawCornerFrame(
        Color32[] pixels,
        int width,
        int height,
        Color32 color)
    {
        int margin = 34;
        int length = 52;
        int thickness = 8;

        DrawLine(
            pixels, width, height,
            margin, margin,
            margin + length, margin,
            thickness, color);

        DrawLine(
            pixels, width, height,
            margin, margin,
            margin, margin + length,
            thickness, color);

        DrawLine(
            pixels, width, height,
            width - margin, margin,
            width - margin - length, margin,
            thickness, color);

        DrawLine(
            pixels, width, height,
            width - margin, margin,
            width - margin, margin + length,
            thickness, color);

        DrawLine(
            pixels, width, height,
            margin, height - margin,
            margin + length, height - margin,
            thickness, color);

        DrawLine(
            pixels, width, height,
            margin, height - margin,
            margin, height - margin - length,
            thickness, color);

        DrawLine(
            pixels, width, height,
            width - margin, height - margin,
            width - margin - length, height - margin,
            thickness, color);

        DrawLine(
            pixels, width, height,
            width - margin, height - margin,
            width - margin, height - margin - length,
            thickness, color);
    }

    private static void DrawDiamond(
        Color32[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        int thickness,
        Color32 color)
    {
        DrawLine(
            pixels, width, height,
            centerX, centerY + radius,
            centerX + radius, centerY,
            thickness, color);

        DrawLine(
            pixels, width, height,
            centerX + radius, centerY,
            centerX, centerY - radius,
            thickness, color);

        DrawLine(
            pixels, width, height,
            centerX, centerY - radius,
            centerX - radius, centerY,
            thickness, color);

        DrawLine(
            pixels, width, height,
            centerX - radius, centerY,
            centerX, centerY + radius,
            thickness, color);
    }

    private static void DrawStars(
        Color32[] pixels,
        int width,
        int height,
        int count,
        int seed,
        Color32 color,
        int minimumRadius,
        int maximumRadius)
    {
        System.Random random =
            new System.Random(seed);

        for (int i = 0; i < count; i++)
        {
            int x = random.Next(0, width);
            int y = random.Next(0, height);
            int radius = random.Next(
                minimumRadius,
                maximumRadius + 1);

            byte alpha = (byte)random.Next(
                Mathf.Max(40, color.a / 2),
                color.a + 1);

            Color32 starColor = color;
            starColor.a = alpha;

            FillCircle(
                pixels, width, height,
                x, y, radius, starColor);
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath =
                $"{currentPath}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)
                ?.FullName
            ?? throw new InvalidOperationException(
                "Не удалось определить корневую папку проекта.");

        return Path.Combine(
            projectRoot,
            assetPath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }
}

#endif