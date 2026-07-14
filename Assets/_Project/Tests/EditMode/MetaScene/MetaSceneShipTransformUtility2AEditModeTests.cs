using NUnit.Framework;
using UnityEngine;

public sealed class MetaSceneShipTransformUtility2AEditModeTests
{
    [Test]
    public void RotationFromDirection_Up_ReturnsZeroZRotation()
    {
        Quaternion rotation =
            MetaSceneShipTransformUtility2A
                .RotationFromDirection(Vector2.up);

        Assert.That(
            NormalizeAngle(rotation.eulerAngles.z),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void RotationFromDirection_Right_ReturnsMinus90Equivalent()
    {
        Quaternion rotation =
            MetaSceneShipTransformUtility2A
                .RotationFromDirection(Vector2.right);

        Assert.That(
            NormalizeAngle(rotation.eulerAngles.z),
            Is.EqualTo(-90f).Within(0.001f));
    }

    [Test]
    public void DirectionFromRotation_Zero_ReturnsUp()
    {
        Vector2 direction =
            MetaSceneShipTransformUtility2A
                .DirectionFromRotation(
                    Quaternion.Euler(0f, 0f, 0f));

        Assert.That(
            direction.x,
            Is.EqualTo(0f).Within(0.001f));

        Assert.That(
            direction.y,
            Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void DirectionFromRotation_Minus90_ReturnsRight()
    {
        Vector2 direction =
            MetaSceneShipTransformUtility2A
                .DirectionFromRotation(
                    Quaternion.Euler(0f, 0f, -90f));

        Assert.That(
            direction.x,
            Is.EqualTo(1f).Within(0.001f));

        Assert.That(
            direction.y,
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void NormalizeDirectionOrUp_WithZero_ReturnsUp()
    {
        Vector2 direction =
            MetaSceneShipTransformUtility2A
                .NormalizeDirectionOrUp(Vector2.zero);

        Assert.AreEqual(Vector2.up, direction);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}
