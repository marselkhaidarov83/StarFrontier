using NUnit.Framework;
using UnityEngine;

public sealed class ConstantSpeedMovement2AEditModeTests
{
    [Test]
    public void Step_WithZeroInput_DoesNotMove()
    {
        Vector2 result =
            ConstantSpeedMovement2A.Step(
                new Vector2(10f, 20f),
                Vector2.zero,
                100f,
                1f);

        Assert.AreEqual(
            new Vector2(10f, 20f),
            result);
    }

    [Test]
    public void Step_WithRightInput_MovesAtConfiguredSpeed()
    {
        Vector2 result =
            ConstantSpeedMovement2A.Step(
                Vector2.zero,
                Vector2.right,
                100f,
                1f);

        Assert.That(
            result.x,
            Is.EqualTo(100f).Within(0.0001f));

        Assert.That(
            result.y,
            Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void Step_DiagonalInput_DoesNotIncreaseSpeed()
    {
        Vector2 result =
            ConstantSpeedMovement2A.Step(
                Vector2.zero,
                new Vector2(1f, 1f),
                100f,
                1f);

        Assert.That(
            result.magnitude,
            Is.EqualTo(100f).Within(0.0001f));
    }

    [Test]
    public void Step_ThirtySixtyAndOneTwentyFps_EndAtSamePosition()
    {
        Vector2 at30 =
            SimulateMovement(
                framesPerSecond: 30,
                durationSeconds: 5f);

        Vector2 at60 =
            SimulateMovement(
                framesPerSecond: 60,
                durationSeconds: 5f);

        Vector2 at120 =
            SimulateMovement(
                framesPerSecond: 120,
                durationSeconds: 5f);

        Assert.That(
            Vector2.Distance(at30, at60),
            Is.LessThan(0.01f));

        Assert.That(
            Vector2.Distance(at60, at120),
            Is.LessThan(0.01f));
    }

    [Test]
    public void Step_WithNegativeDeltaTime_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => ConstantSpeedMovement2A.Step(
                Vector2.zero,
                Vector2.right,
                100f,
                -0.01f));
    }

    [Test]
    public void Step_WithNegativeSpeed_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => ConstantSpeedMovement2A.Step(
                Vector2.zero,
                Vector2.right,
                -1f,
                1f));
    }

    private static Vector2 SimulateMovement(
        int framesPerSecond,
        float durationSeconds)
    {
        Vector2 position = Vector2.zero;

        float deltaTime =
            1f / framesPerSecond;

        int frameCount =
            Mathf.RoundToInt(
                framesPerSecond *
                durationSeconds);

        for (int i = 0; i < frameCount; i++)
        {
            position =
                ConstantSpeedMovement2A.Step(
                    position,
                    Vector2.right,
                    100f,
                    deltaTime);
        }

        return position;
    }
}
