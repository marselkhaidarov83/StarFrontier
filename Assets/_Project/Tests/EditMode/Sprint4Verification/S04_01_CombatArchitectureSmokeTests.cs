using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class S04_01_CombatArchitectureSmokeTests
{
    [Test]
    public void CombatRuntime_Is_SystemScene()
    {
        Assert.AreEqual(
            CombatRuntimeMode2A.SystemScene,
            CombatRuntimeContract2A.RuntimeMode);

        Assert.AreEqual(
            "SystemScene",
            CombatRuntimeContract2A.CanonicalSceneName);

        Assert.AreEqual(
            "Assets/_Project/Scenes/SystemScene.unity",
            CombatRuntimeContract2A.CanonicalScenePath);
    }

    [Test]
    public void CombatRuntime_RequiredServiceNames_AreDeclared()
    {
        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredServiceNames,
            "ISystemEncounterService");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredServiceNames,
            "IPlayerCombatTargetService");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredServiceNames,
            "ISystemNpcCombatService");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredServiceNames,
            "IPlayerAttackService");
    }

    [Test]
    public void CombatRuntime_RequiredSceneRoots_AreDeclared()
    {
        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredSceneRootNames,
            "EnemiesRoot");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredSceneRootNames,
            "AlliesRoot");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredSceneRootNames,
            "ProjectilesRoot");

        CollectionAssert.Contains(
            CombatRuntimeContract2A.RequiredSceneRootNames,
            "VfxRoot");
    }

    [Test]
    public void SystemSceneRoot2A_Exposes_CombatRoots()
    {
        Assert.NotNull(typeof(SystemSceneRoot2A).GetProperty("EnemiesRoot"));
        Assert.NotNull(typeof(SystemSceneRoot2A).GetProperty("AlliesRoot"));
        Assert.NotNull(typeof(SystemSceneRoot2A).GetProperty("ProjectilesRoot"));
        Assert.NotNull(typeof(SystemSceneRoot2A).GetProperty("VfxRoot"));
    }

    [Test]
    public void SystemScene_CanOpen_And_CombatRootsAreBound()
    {
        EditorSceneManager.OpenScene(
            CombatRuntimeContract2A.CanonicalScenePath,
            OpenSceneMode.Single);

        SystemSceneRoot2A root =
            Object.FindObjectOfType<SystemSceneRoot2A>();

        Assert.NotNull(
            root,
            "SystemScene must contain SystemSceneRoot2A.");

        Assert.NotNull(
            root.EnemiesRoot,
            "SystemSceneRoot2A must have EnemiesRoot assigned.");

        Assert.NotNull(
            root.AlliesRoot,
            "SystemSceneRoot2A must have AlliesRoot assigned.");

        Assert.NotNull(
            root.ProjectilesRoot,
            "SystemSceneRoot2A must have ProjectilesRoot assigned.");

        Assert.NotNull(
            root.VfxRoot,
            "SystemSceneRoot2A must have VfxRoot assigned.");
    }

    [Test]
    public void CombatEventContract_TypesExist()
    {
        Assert.NotNull(typeof(CombatRuntimeStartedEvent2A));
        Assert.NotNull(typeof(CombatDamageEvent2A));
        Assert.NotNull(typeof(CombatProjectileCreatedEvent2A));
        Assert.NotNull(typeof(CombatProjectileImpactEvent2A));
        Assert.NotNull(typeof(CombatTargetDestroyedEvent2A));
        Assert.NotNull(typeof(CombatVictoryEvent2A));
        Assert.NotNull(typeof(CombatDefeatEvent2A));
        Assert.NotNull(typeof(CombatRewardPendingEvent2A));
    }
}