using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class S04_02_SystemSceneNpcViewTests
{
    private const string SystemScenePath =
        "Assets/_Project/Scenes/SystemScene.unity";

    [Test]
    public void SystemScene_HasNpcViewBinderWithPrefab()
    {
        Scene scene =
            EditorSceneManager.OpenScene(
                SystemScenePath,
                OpenSceneMode.Single);

        Assert.IsTrue(
            scene.IsValid(),
            "SystemScene could not be opened: " + SystemScenePath);

        SystemNpcViewBinder binder =
            Object.FindFirstObjectByType<SystemNpcViewBinder>();

        Assert.NotNull(
            binder,
            "SystemScene must contain SystemNpcViewBinder.");

        SerializedObject serializedBinder =
            new SerializedObject(binder);

        SerializedProperty enemyRoot =
            serializedBinder.FindProperty("enemyRoot");

        SerializedProperty allyRoot =
            serializedBinder.FindProperty("allyRoot");

        SerializedProperty npcViewPrefab =
            serializedBinder.FindProperty("npcViewPrefab");

        Assert.NotNull(
            enemyRoot,
            "SystemNpcViewBinder must have serialized field enemyRoot.");

        Assert.NotNull(
            allyRoot,
            "SystemNpcViewBinder must have serialized field allyRoot.");

        Assert.NotNull(
            npcViewPrefab,
            "SystemNpcViewBinder must have serialized field npcViewPrefab.");

        Assert.NotNull( 
            enemyRoot.objectReferenceValue,
            "SystemNpcViewBinder Enemy Root must be assigned.");

        Assert.NotNull(
            allyRoot.objectReferenceValue,
            "SystemNpcViewBinder Ally Root must be assigned.");

        Assert.NotNull(
            npcViewPrefab.objectReferenceValue,
            "SystemNpcViewBinder Npc View Prefab must be assigned.");
    }
}