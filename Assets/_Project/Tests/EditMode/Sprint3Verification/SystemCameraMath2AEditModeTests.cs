using NUnit.Framework;
using UnityEngine;

public sealed class
    SystemCameraMath2AEditModeTests
{
    [Test]
    public void ClampZoom_ValueBelowMinimum_ReturnsMinimum()
    {
        float result =
            SystemCameraMath2A.ClampZoom(
                100f,
                650f,
                2200f);

        Assert.AreEqual(
            650f,
            result,
            0.0001f);
    }

    [Test]
    public void ClampZoom_ValueAboveMaximum_ReturnsMaximum()
    {
        float result =
            SystemCameraMath2A.ClampZoom(
                5000f,
                650f,
                2200f);

        Assert.AreEqual(
            2200f,
            result,
            0.0001f);
    }

    [Test]
    public void ClampZoom_ValueInsideRange_IsNotChanged()
    {
        float result =
            SystemCameraMath2A.ClampZoom(
                1200f,
                650f,
                2200f);

        Assert.AreEqual(
            1200f,
            result,
            0.0001f);
    }

    [Test]
    public void CalculateBoundsCorrection_LeftOverflow_MovesRight()
    {
        Rect bounds =
            new Rect(
                -100f,
                -200f,
                200f,
                400f);

        Vector2 correction =
            SystemCameraMath2A
                .CalculateBoundsCorrection(
                    bounds,
                    new Vector2(-150f, -50f),
                    new Vector2(50f, 50f));

        Assert.AreEqual(
            50f,
            correction.x,
            0.0001f);

        Assert.AreEqual(
            0f,
            correction.y,
            0.0001f);
    }

    [Test]
    public void CalculateBoundsCorrection_RightOverflow_MovesLeft()
    {
        Rect bounds =
            new Rect(
                -100f,
                -200f,
                200f,
                400f);

        Vector2 correction =
            SystemCameraMath2A
                .CalculateBoundsCorrection(
                    bounds,
                    new Vector2(-50f, -50f),
                    new Vector2(150f, 50f));

        Assert.AreEqual(
            -50f,
            correction.x,
            0.0001f);
    }

    [Test]
    public void CalculateBoundsCorrection_TopOverflow_MovesDown()
    {
        Rect bounds =
            new Rect(
                -100f,
                -200f,
                200f,
                400f);

        Vector2 correction =
            SystemCameraMath2A
                .CalculateBoundsCorrection(
                    bounds,
                    new Vector2(-50f, 100f),
                    new Vector2(50f, 250f));

        Assert.AreEqual(
            -50f,
            correction.y,
            0.0001f);
    }

    [Test]
    public void CalculateBoundsCorrection_FootprintInside_ReturnsZero()
    {
        Rect bounds =
            new Rect(
                -100f,
                -200f,
                200f,
                400f);

        Vector2 correction =
            SystemCameraMath2A
                .CalculateBoundsCorrection(
                    bounds,
                    new Vector2(-50f, -50f),
                    new Vector2(50f, 50f));

        Assert.AreEqual(
            Vector2.zero,
            correction);
    }

    [Test]
    public void PerspectiveOffset_LengthMatchesDistance()
    {
        Vector3 offset =
            SystemCameraMath2A
                .CreatePerspectiveOffset(
                    1250f,
                    28f,
                    0f);

        Assert.AreEqual(
            1250f,
            offset.magnitude,
            0.01f);
    }

    [Test]
    public void PerspectiveOffset_ZeroTilt_IsBehindGameplayPlane()
    {
        Vector3 offset =
            SystemCameraMath2A
                .CreatePerspectiveOffset(
                    1000f,
                    0f,
                    0f);

        Assert.AreEqual(
            0f,
            offset.x,
            0.0001f);

        Assert.AreEqual(
            0f,
            offset.y,
            0.0001f);

        Assert.AreEqual(
            -1000f,
            offset.z,
            0.0001f);
    }

    [Test]
    public void RayIntersection_HitsGameplayPlane()
    {
        Ray ray =
            new Ray(
                new Vector3(0f, 0f, -10f),
                Vector3.forward);

        bool success =
            SystemCameraMath2A
                .TryIntersectRayWithPlaneZ(
                    ray,
                    0f,
                    out Vector3 point);

        Assert.IsTrue(success);

        Assert.AreEqual(
            Vector3.zero,
            point);
    }

    [Test]
    public void RayIntersection_ParallelRay_ReturnsFalse()
    {
        Ray ray =
            new Ray(
                new Vector3(0f, 0f, -10f),
                Vector3.right);

        bool success =
            SystemCameraMath2A
                .TryIntersectRayWithPlaneZ(
                    ray,
                    0f,
                    out _);

        Assert.IsFalse(success);
    }
}