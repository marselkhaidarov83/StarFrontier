using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerShipViewEditModeTests
{
    [Test]
    public void HasRequiredReferences_WithMissingRenderers_ReturnsFalse()
    {
        GameObject root =
            new GameObject("PF_PlayerShip");

        PlayerShipView2A view =
            root.AddComponent<PlayerShipView2A>();

        Assert.IsFalse(view.HasRequiredReferences());

        Object.DestroyImmediate(root);
    }

    [Test]
    public void HasRequiredReferences_WithAllRenderers_ReturnsTrue()
    {
        GameObject root =
            CreatePlayerShipView(
                out PlayerShipView2A view,
                out _,
                out _,
                out _);

        Assert.IsTrue(view.HasRequiredReferences());

        Object.DestroyImmediate(root);
    }

    [Test]
    public void ApplyInspectorSprites_AssignsSprites()
    {
        GameObject root =
            CreatePlayerShipView(
                out PlayerShipView2A view,
                out SpriteRenderer bodyRenderer,
                out SpriteRenderer shadowRenderer,
                out SpriteRenderer engineGlowRenderer);

        Sprite bodySprite =
            CreateTestSprite("body");

        Sprite shadowSprite =
            CreateTestSprite("shadow");

        Sprite engineSprite =
            CreateTestSprite("engine");

        SetPrivateField(
            view,
            "bodySprite",
            bodySprite);

        SetPrivateField(
            view,
            "shadowSprite",
            shadowSprite);

        SetPrivateField(
            view,
            "engineGlowSprite",
            engineSprite);

        view.ApplyInspectorSprites();

        Assert.AreEqual(bodySprite, bodyRenderer.sprite);
        Assert.AreEqual(shadowSprite, shadowRenderer.sprite);
        Assert.AreEqual(engineSprite, engineGlowRenderer.sprite);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(bodySprite.texture);
        Object.DestroyImmediate(shadowSprite.texture);
        Object.DestroyImmediate(engineSprite.texture);
    }

    [Test]
    public void SetFinalShipSprite_OverridesBodySprite()
    {
        GameObject root =
            CreatePlayerShipView(
                out PlayerShipView2A view,
                out SpriteRenderer bodyRenderer,
                out _,
                out _);

        Sprite temporarySprite =
            CreateTestSprite("temporary");

        Sprite finalSprite =
            CreateTestSprite("final");

        view.SetBodySprite(temporarySprite);
        view.SetFinalShipSprite(finalSprite);

        Assert.AreEqual(finalSprite, bodyRenderer.sprite);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(temporarySprite.texture);
        Object.DestroyImmediate(finalSprite.texture);
    }

    [Test]
    public void ConfigureSorting_AssignsOrderInLayer()
    {
        GameObject root =
            CreatePlayerShipView(
                out PlayerShipView2A view,
                out SpriteRenderer bodyRenderer,
                out SpriteRenderer shadowRenderer,
                out SpriteRenderer engineGlowRenderer);

        SetPrivateField(
            view,
            "shadowOrderInLayer",
            -1);

        SetPrivateField(
            view,
            "engineGlowOrderInLayer",
            5);

        SetPrivateField(
            view,
            "bodyOrderInLayer",
            10);

        view.ConfigureSorting();

        Assert.AreEqual(-1, shadowRenderer.sortingOrder);
        Assert.AreEqual(5, engineGlowRenderer.sortingOrder);
        Assert.AreEqual(10, bodyRenderer.sortingOrder);

        Object.DestroyImmediate(root);
    }

    private static GameObject CreatePlayerShipView(
        out PlayerShipView2A view,
        out SpriteRenderer bodyRenderer,
        out SpriteRenderer shadowRenderer,
        out SpriteRenderer engineGlowRenderer)
    {
        GameObject root =
            new GameObject("PF_PlayerShip");

        GameObject visualRoot =
            new GameObject("ShipVisualRoot");

        visualRoot.transform.SetParent(
            root.transform,
            false);

        GameObject shadow =
            new GameObject("ShipShadow");

        shadow.transform.SetParent(
            visualRoot.transform,
            false);

        GameObject engine =
            new GameObject("EngineGlow");

        engine.transform.SetParent(
            visualRoot.transform,
            false);

        GameObject body =
            new GameObject("ShipBody");

        body.transform.SetParent(
            visualRoot.transform,
            false);

        shadowRenderer =
            shadow.AddComponent<SpriteRenderer>();

        engineGlowRenderer =
            engine.AddComponent<SpriteRenderer>();

        bodyRenderer =
            body.AddComponent<SpriteRenderer>();

        view =
            root.AddComponent<PlayerShipView2A>();

        SetPrivateField(
            view,
            "visualRoot",
            visualRoot.transform);

        SetPrivateField(
            view,
            "bodyRenderer",
            bodyRenderer);

        SetPrivateField(
            view,
            "shadowRenderer",
            shadowRenderer);

        SetPrivateField(
            view,
            "engineGlowRenderer",
            engineGlowRenderer);

        return root;
    }

    private static Sprite CreateTestSprite(string name)
    {
        Texture2D texture =
            new Texture2D(4, 4);

        texture.name =
            name + "_texture";

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                texture.SetPixel(
                    x,
                    y,
                    Color.white);
            }
        }

        texture.Apply();

        Sprite sprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                4f);

        sprite.name = name;

        return sprite;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        Assert.IsNotNull(
            field,
            $"Field '{fieldName}' was not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }

    private static FieldInfo FindField(
        System.Type type,
        string fieldName)
    {
        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}