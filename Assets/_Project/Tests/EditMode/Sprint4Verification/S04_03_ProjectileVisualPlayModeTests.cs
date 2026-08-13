using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class S04_03_ProjectileVisualPlayModeTests
{
    private GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [UnityTest]
    public IEnumerator ProjectileVisual_SpawnMoveImpactCleanupAndReusePool()
    {
        GalaxyNpcCombatVisualController controller = CreateController();

        var created = new GalaxyNpcProjectileCreatedEvent(
            "projectile_test_01",
            "system_test",
            string.Empty,
            CombatTargetType.Npc,
            "enemy_test_01",
            "weapon_test",
            Vector3.zero,
            new Vector3(4f, 0f, 0f),
            12f);

        GalaxyNpcProjectileView firstView = controller.SpawnProjectile(created);

        Assert.That(firstView, Is.Not.Null);
        Assert.That(firstView.gameObject.activeSelf, Is.True);
        Assert.That(controller.ActiveProjectileCount, Is.EqualTo(1));
        Assert.That(firstView.transform.position, Is.EqualTo(Vector3.zero));

        controller.MoveProjectile("projectile_test_01", new Vector3(2f, 0f, 0f));

        Assert.That(firstView.transform.position.x, Is.EqualTo(2f).Within(0.001f));

        controller.CompleteProjectile("projectile_test_01");

        Assert.That(firstView.gameObject.activeSelf, Is.False);
        Assert.That(controller.ActiveProjectileCount, Is.EqualTo(0));
        Assert.That(controller.ProjectilePoolCount, Is.EqualTo(1));

        var createdAgain = new GalaxyNpcProjectileCreatedEvent(
            "projectile_test_02",
            "system_test",
            string.Empty,
            CombatTargetType.Npc,
            "enemy_test_01",
            "weapon_test",
            Vector3.zero,
            new Vector3(3f, 0f, 0f),
            12f);

        GalaxyNpcProjectileView reusedView = controller.SpawnProjectile(createdAgain);

        Assert.That(reusedView, Is.SameAs(firstView));
        Assert.That(reusedView.gameObject.activeSelf, Is.True);
        Assert.That(controller.ActiveProjectileCount, Is.EqualTo(1));
        Assert.That(controller.ProjectilePoolCount, Is.EqualTo(0));

        controller.CompleteProjectile("projectile_test_02");

        yield return null;
    }

    [UnityTest]
    public IEnumerator HitFx_AndExplosionFx_CleanupBackToPool()
    {
        GalaxyNpcCombatVisualController controller = CreateController();

        GalaxyNpcTimedFxView hitFx = controller.SpawnHitFx(new Vector3(1f, 2f, 0f));
        GalaxyNpcTimedFxView explosionFx = controller.SpawnExplosionFx(new Vector3(3f, 4f, 0f));

        Assert.That(hitFx, Is.Not.Null);
        Assert.That(explosionFx, Is.Not.Null);
        Assert.That(hitFx.gameObject.activeSelf, Is.True);
        Assert.That(explosionFx.gameObject.activeSelf, Is.True);

        hitFx.Tick(1f);
        explosionFx.Tick(1f);

        yield return null;

        Assert.That(hitFx.gameObject.activeSelf, Is.False);
        Assert.That(explosionFx.gameObject.activeSelf, Is.False);
        Assert.That(controller.HitFxPoolCount, Is.EqualTo(1));
        Assert.That(controller.ExplosionFxPoolCount, Is.EqualTo(1));
    }

    private GalaxyNpcCombatVisualController CreateController()
    {
        _root = new GameObject("Projectile Visual PlayMode Test Root");

        var projectileRoot = new GameObject("Projectile Root").transform;
        projectileRoot.SetParent(_root.transform);

        var fxRoot = new GameObject("FX Root").transform;
        fxRoot.SetParent(_root.transform);

        GalaxyNpcProjectileView projectilePrefab = CreateProjectilePrefab();
        GalaxyNpcTimedFxView hitFxPrefab = CreateFxPrefab("Hit FX Prefab");
        GalaxyNpcTimedFxView explosionFxPrefab = CreateFxPrefab("Explosion FX Prefab");

        var controllerObject = new GameObject("GalaxyNpcCombatVisualController");
        controllerObject.transform.SetParent(_root.transform);

        GalaxyNpcCombatVisualController controller =
            controllerObject.AddComponent<GalaxyNpcCombatVisualController>();

        SetPrivateField(controller, "projectileRoot", projectileRoot);
        SetPrivateField(controller, "fxRoot", fxRoot);
        SetPrivateField(controller, "projectilePrefab", projectilePrefab);
        SetPrivateField(controller, "hitFxPrefab", hitFxPrefab);
        SetPrivateField(controller, "explosionFxPrefab", explosionFxPrefab);
        SetPrivateField(controller, "hitFxLifetimeSeconds", 0.05f);
        SetPrivateField(controller, "explosionFxLifetimeSeconds", 0.05f);

        return controller;
    }

    private GalaxyNpcProjectileView CreateProjectilePrefab()
    {
        var prefab = new GameObject("Projectile Prefab");
        prefab.transform.SetParent(_root.transform);
        prefab.SetActive(false);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(prefab.transform);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        GalaxyNpcProjectileView view = prefab.AddComponent<GalaxyNpcProjectileView>();

        SetPrivateField(view, "spriteRenderer", renderer);
        SetPrivateField(view, "rotationOffsetDegrees", -90f);

        return view;
    }

    private GalaxyNpcTimedFxView CreateFxPrefab(string name)
    {
        var prefab = new GameObject(name);
        prefab.transform.SetParent(_root.transform);
        prefab.SetActive(false);

        prefab.AddComponent<SpriteRenderer>();
        return prefab.AddComponent<GalaxyNpcTimedFxView>();
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }
}