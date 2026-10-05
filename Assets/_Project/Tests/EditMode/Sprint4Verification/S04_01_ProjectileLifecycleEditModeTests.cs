using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class S04_01_ProjectileLifecycleEditModeTests
{
    [Test]
    public void GalaxyNpcProjectileRuntimeState_StoresLogicalProjectileData()
    {
        GalaxyNpcProjectileRuntimeState projectile =
            new GalaxyNpcProjectileRuntimeState
            {
                ProjectileId = "projectile_test_01",
                SystemId = "system_test_01",
                ShooterType = CombatShooterType.Player,
                ShooterNpcId = string.Empty,
                TargetType = CombatTargetType.Npc,
                TargetNpcId = "npc_target_01",
                WeaponConfigId = "weapon_test_01",
                StartPosition = Vector3.zero,
                CurrentPosition = Vector3.one,
                LastKnownTargetPosition = new Vector3(3f, 4f, 0f),
                Damage = 12,
                CreatedTick = 10,
                ImpactTick = 12,
                ElapsedSeconds = 0.5f,
                LifetimeSeconds = 1.5f,
                IsResolved = false
            };

        Assert.AreEqual("projectile_test_01", projectile.ProjectileId);
        Assert.AreEqual("system_test_01", projectile.SystemId);
        Assert.AreEqual(CombatShooterType.Player, projectile.ShooterType);
        Assert.AreEqual(CombatTargetType.Npc, projectile.TargetType);
        Assert.AreEqual("npc_target_01", projectile.TargetNpcId);
        Assert.AreEqual("weapon_test_01", projectile.WeaponConfigId);
        Assert.AreEqual(12, projectile.Damage);
        Assert.AreEqual(10, projectile.CreatedTick);
        Assert.AreEqual(12, projectile.ImpactTick);
        Assert.IsFalse(projectile.IsResolved);
    }

    [Test]
    public void GalaxyNpcProjectileRuntimeState_ExposesRequiredLifecycleFields()
    {
        AssertFieldExists<string>("ProjectileId");
        AssertFieldExists<string>("SystemId");

        AssertFieldExists<CombatShooterType>("ShooterType");
        AssertFieldExists<string>("ShooterNpcId");

        AssertFieldExists<CombatTargetType>("TargetType");
        AssertFieldExists<string>("TargetNpcId");

        AssertFieldExists<string>("WeaponConfigId");

        AssertFieldExists<Vector3>("StartPosition");
        AssertFieldExists<Vector3>("CurrentPosition");
        AssertFieldExists<Vector3>("LastKnownTargetPosition");

        AssertFieldExists<int>("Damage");
        AssertFieldExists<int>("CreatedTick");
        AssertFieldExists<int>("ImpactTick");

        AssertFieldExists<float>("ElapsedSeconds");
        AssertFieldExists<float>("LifetimeSeconds");

        AssertFieldExists<bool>("IsResolved");
    }

    [Test]
    public void SystemNpcCombatService_ExposesProjectileLifecycleContract()
    {
        AssertMethodExists(
            "TickProjectiles",
            typeof(float));

        AssertMethodExists(
            "TryGetProjectile",
            typeof(string),
            typeof(GalaxyNpcProjectileRuntimeState).MakeByRefType());

        AssertMethodExists(
            "TryCreatePlayerProjectile",
            typeof(string),
            typeof(string),
            typeof(int));

        FieldInfo activeProjectilesField =
            typeof(SystemNpcCombatService).GetField(
                "_activeProjectiles",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(
            activeProjectilesField,
            "SystemNpcCombatService must keep logical active projectiles.");
    }

    [Test]
    public void CombatProjectileEvents_ExistForSpawnAndImpact()
    {
        Assert.NotNull(typeof(CombatProjectileCreatedEvent2A));
        Assert.NotNull(typeof(CombatProjectileImpactEvent2A));
    }

    private static void AssertFieldExists<TField>(
        string fieldName)
    {
        FieldInfo field =
            typeof(GalaxyNpcProjectileRuntimeState).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public);

        Assert.NotNull(
            field,
            "GalaxyNpcProjectileRuntimeState must expose field: " + fieldName);

        Assert.AreEqual(
            typeof(TField),
            field.FieldType,
            "GalaxyNpcProjectileRuntimeState field has wrong type: " + fieldName);
    }

    private static void AssertMethodExists(
        string methodName,
        params System.Type[] parameterTypes)
    {
        MethodInfo method =
            typeof(SystemNpcCombatService).GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public,
                null,
                parameterTypes,
                null);

        Assert.NotNull(
            method,
            "SystemNpcCombatService must expose method: " + methodName);
    }
}