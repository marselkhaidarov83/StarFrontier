using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Sprint 3 movement persistence; works without a scene or the global Bootstrapper.
/// </summary>
public sealed class S03_MovementSaveRestoreRegressionTests
{
    private ShipMovementConfig _config;
    private ShipMovementService2A _movement;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<ShipMovementConfig>();
        _movement = new ShipMovementService2A(_config, new SystemGameplayStateService());
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
            Object.DestroyImmediate(_config);
    }

    [Test]
    public void SaveAndRestore_PreservesPositionDirectionAndUnrelatedZCoordinate()
    {
        _movement.SetPosition(new Vector2(17.5f, -9.25f));
        _movement.SetFacingDirection(new Vector2(3f, 4f));
        var player = new PlayerState
        {
            SystemMapShipPosition = new Vector3(0f, 0f, 33f)
        };

        _movement.WriteToPlayerState(player);

        Assert.That(player.SystemMapShipPosition.x, Is.EqualTo(17.5f).Within(0.0001f));
        Assert.That(player.SystemMapShipPosition.y, Is.EqualTo(-9.25f).Within(0.0001f));
        Assert.That(player.SystemMapShipPosition.z, Is.EqualTo(33f));

        var restored = new ShipMovementService2A(_config, new SystemGameplayStateService());
        restored.InitializeFromPlayerState(player);

        Assert.That(restored.State.Position.x, Is.EqualTo(17.5f).Within(0.0001f));
        Assert.That(restored.State.Position.y, Is.EqualTo(-9.25f).Within(0.0001f));
        Assert.That(restored.State.FacingDirection.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(restored.State.FacingDirection.y, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [TestCase(0f, 0f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 0f)]
    public void NonFiniteOrZeroSavedFacingDirection_UsesSafeUpVector(float x, float y)
    {
        var player = new PlayerState
        {
            SystemMapShipDirection = new Vector3(x, y, 0f)
        };

        _movement.InitializeFromPlayerState(player);

        Assert.That(_movement.State.FacingDirection, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void DisabledMovement_RetainsSavedPositionAndCannotContinueMoving()
    {
        _movement.SetPosition(new Vector2(12f, -7f));
        _movement.SetEnabled(false);
        _movement.Tick(0.5f);

        Assert.That(_movement.State.Position, Is.EqualTo(new Vector2(12f, -7f)));
        Assert.That(_movement.State.Velocity, Is.EqualTo(Vector2.zero));
    }
}
