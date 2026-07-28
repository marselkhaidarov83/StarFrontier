using NUnit.Framework;
using UnityEngine;

public sealed class TargetService2AEditModeTests
{
    private SystemGameplayStateService
        _gameplayStateService;

    private TargetingConfig
        _targetingConfig;

    private SimpleEventBus
        _eventBus;

    private TargetService2A
        _targetService;

    [SetUp]
    public void SetUp()
    {
        _gameplayStateService =
            new SystemGameplayStateService();

        _targetingConfig =
            ScriptableObject
                .CreateInstance<
                    TargetingConfig>();

        _eventBus =
            new SimpleEventBus();

        _targetService =
            new TargetService2A(
                _gameplayStateService,
                _targetingConfig,
                _eventBus);

        _gameplayStateService
            .Movement
            .SetPosition(
                Vector2.zero);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(
            _targetingConfig);
    }

    [Test]
    public void
        TrySelectTarget_ValidPlanet_SelectsTarget()
    {
        bool selected =
            _targetService.TrySelectTarget(
                "planet_01",
                SystemGameplayTargetType.Planet,
                new Vector2(2f, 0f),
                true,
                true,
                true,
                out TargetSelectionFailReason2A
                    failReason);

        Assert.IsTrue(selected);

        Assert.AreEqual(
            TargetSelectionFailReason2A.None,
            failReason);

        Assert.IsTrue(
            _targetService.HasTarget);

        Assert.AreEqual(
            "planet_01",
            _targetService
                .State
                .CurrentTargetId);

        Assert.IsTrue(
            _targetService
                .State
                .IsTargetInRange);
    }

    [Test]
    public void
        TrySelectTarget_SecondTarget_ReplacesFirst()
    {
        _targetService.TrySelectTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            new Vector2(1f, 0f),
            true,
            true,
            true,
            out _);

        _targetService.TrySelectTarget(
            "planet_02",
            SystemGameplayTargetType.Planet,
            new Vector2(2f, 0f),
            true,
            true,
            true,
            out _);

        Assert.AreEqual(
            "planet_02",
            _targetService
                .State
                .CurrentTargetId);
    }

    [Test]
    public void
        TrySelectTarget_EmptyId_DoesNotReplaceCurrentTarget()
    {
        _targetService.TrySelectTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            new Vector2(1f, 0f),
            true,
            true,
            true,
            out _);

        bool selected =
            _targetService.TrySelectTarget(
                string.Empty,
                SystemGameplayTargetType.Planet,
                new Vector2(2f, 0f),
                true,
                true,
                true,
                out TargetSelectionFailReason2A
                    failReason);

        Assert.IsFalse(selected);

        Assert.AreEqual(
            TargetSelectionFailReason2A.EmptyTargetId,
            failReason);

        Assert.AreEqual(
            "planet_01",
            _targetService
                .State
                .CurrentTargetId);
    }

    [Test]
    public void
        TrySelectTarget_OutOfRange_StillSelectsTarget()
    {
        bool selected =
            _targetService.TrySelectTarget(
                "planet_far",
                SystemGameplayTargetType.Planet,
                new Vector2(100f, 0f),
                true,
                true,
                true,
                out _);

        Assert.IsTrue(selected);

        Assert.IsTrue(
            _targetService.HasTarget);

        Assert.IsFalse(
            _targetService
                .State
                .IsTargetInRange);
    }

    [Test]
    public void
        ClearTarget_SelectedTarget_ClearsState()
    {
        _targetService.TrySelectTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            Vector2.one,
            true,
            true,
            true,
            out _);

        _targetService.ClearTarget();

        Assert.IsFalse(
            _targetService.HasTarget);

        Assert.AreEqual(
            string.Empty,
            _targetService
                .State
                .CurrentTargetId);
    }

    [Test]
    public void
        SuccessfulSelection_PublishesOneEvent()
    {
        int eventCount =
            0;

        _eventBus
            .Subscribe<TargetChangedEvent2A>(
                _ => eventCount++);

        _targetService.TrySelectTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            Vector2.one,
            true,
            true,
            true,
            out _);

        Assert.AreEqual(
            1,
            eventCount);
    }
}