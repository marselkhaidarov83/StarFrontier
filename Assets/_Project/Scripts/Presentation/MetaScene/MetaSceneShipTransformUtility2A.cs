using UnityEngine;

/// <summary>
/// Вспомогательные методы для связи направления корабля
/// и визуального поворота SpriteRenderer.
///
/// Формула поворота совпадает с legacy-логикой
/// SystemShipMarkerController2:
/// angle = Atan2(y, x)
/// zRotation = angle - 90.
/// </summary>
public static class MetaSceneShipTransformUtility2A
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;

    public static Vector2 NormalizeDirectionOrUp(
        Vector2 direction)
    {
        if (!IsFinite(direction)
            || direction.sqrMagnitude < DirectionThresholdSqrMagnitude)
        {
            return Vector2.up;
        }

        return direction.normalized;
    }

    public static Quaternion RotationFromDirection(
        Vector2 direction)
    {
        Vector2 safeDirection =
            NormalizeDirectionOrUp(direction);

        float angle =
            Mathf.Atan2(
                safeDirection.y,
                safeDirection.x)
            * Mathf.Rad2Deg;

        return Quaternion.Euler(
            0f,
            0f,
            angle - 90f);
    }

    public static Vector2 DirectionFromRotation(
        Quaternion rotation)
    {
        float angleDegrees =
            rotation.eulerAngles.z + 90f;

        float radians =
            angleDegrees * Mathf.Deg2Rad;

        Vector2 direction =
            new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

        return NormalizeDirectionOrUp(direction);
    }

    public static Vector2 ToVector2(
        Vector3 position)
    {
        return new Vector2(
            position.x,
            position.y);
    }

    public static Vector3 ToVector3(
        Vector2 position,
        float z)
    {
        return new Vector3(
            position.x,
            position.y,
            z);
    }

    public static bool IsFinite(
        Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }

    public static bool IsFinite(
        Vector2 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y);
    }

    public static bool IsFinite(
        float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value);
    }
}
