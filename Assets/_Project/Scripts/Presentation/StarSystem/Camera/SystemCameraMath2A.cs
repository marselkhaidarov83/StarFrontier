using UnityEngine;

/// <summary>
/// Чистые математические функции системной камеры.
///
/// Класс не обращается к сцене и пригоден для EditMode-тестов.
/// </summary>
public static class SystemCameraMath2A
{
    private const float Epsilon = 0.00001f;

    public static float ClampZoom(
        float value,
        float minimum,
        float maximum)
    {
        float safeMinimum =
            Mathf.Max(Epsilon, minimum);

        float safeMaximum =
            Mathf.Max(safeMinimum, maximum);

        if (!IsFinite(value))
            return safeMinimum;

        return Mathf.Clamp(
            value,
            safeMinimum,
            safeMaximum);
    }

    /// <summary>
    /// Ограничивает focus point так, чтобы проекция viewport
    /// не выходила за allowedBounds, пока viewport меньше карты.
    ///
    /// footprintMinimumOffset и footprintMaximumOffset —
    /// смещения углов проекции viewport относительно focus point.
    ///
    /// Если viewport больше карты по оси, допустимого диапазона
    /// focus point по этой оси не существует. Тогда ось либо
    /// центрируется на oversizedCenter, либо остаётся без изменения.
    /// </summary>
    public static Vector2 ClampFocusPoint(
        Rect allowedBounds,
        Vector2 requestedFocus,
        Vector2 footprintMinimumOffset,
        Vector2 footprintMaximumOffset,
        Vector2 oversizedCenter,
        bool centerOversizedAxes,
        bool forceCenterX,
        bool forceCenterY,
        out bool xLocked,
        out bool yLocked)
    {
        if (!IsFinite(requestedFocus) ||
            !IsFinite(footprintMinimumOffset) ||
            !IsFinite(footprintMaximumOffset) ||
            !IsFinite(oversizedCenter))
        {
            xLocked = false;
            yLocked = false;
            return requestedFocus;
        }

        float minimumFocusX =
            allowedBounds.xMin -
            footprintMinimumOffset.x;

        float maximumFocusX =
            allowedBounds.xMax -
            footprintMaximumOffset.x;

        float minimumFocusY =
            allowedBounds.yMin -
            footprintMinimumOffset.y;

        float maximumFocusY =
            allowedBounds.yMax -
            footprintMaximumOffset.y;

        bool hasHorizontalRange =
            minimumFocusX <=
            maximumFocusX + Epsilon;

        bool hasVerticalRange =
            minimumFocusY <=
            maximumFocusY + Epsilon;

        Vector2 result =
            requestedFocus;

        if (forceCenterX)
        {
            xLocked = true;
            result.x = oversizedCenter.x;
        }
        else if (hasHorizontalRange)
        {
            xLocked = false;
            result.x = Mathf.Clamp(
                requestedFocus.x,
                minimumFocusX,
                maximumFocusX);
        }
        else
        {
            xLocked = true;

            if (centerOversizedAxes)
            {
                result.x = oversizedCenter.x;
            }
        }

        if (forceCenterY)
        {
            yLocked = true;
            result.y = oversizedCenter.y;
        }
        else if (hasVerticalRange)
        {
            yLocked = false;
            result.y = Mathf.Clamp(
                requestedFocus.y,
                minimumFocusY,
                maximumFocusY);
        }
        else
        {
            yLocked = true;

            if (centerOversizedAxes)
            {
                result.y = oversizedCenter.y;
            }
        }

        return result;
    }

    /// <summary>
    /// Совместимость со старыми тестами и вызовами.
    ///
    /// Метод корректирует проекцию только тогда, когда viewport
    /// меньше карты по соответствующей оси. Если viewport больше
    /// карты, корректная позиция должна определяться через
    /// ClampFocusPoint и центр системы.
    /// </summary>
    public static Vector2 CalculateBoundsCorrection(
        Rect allowedBounds,
        Vector2 footprintMinimum,
        Vector2 footprintMaximum)
    {
        if (!IsFinite(footprintMinimum) ||
            !IsFinite(footprintMaximum))
        {
            return Vector2.zero;
        }

        float footprintWidth =
            footprintMaximum.x -
            footprintMinimum.x;

        float footprintHeight =
            footprintMaximum.y -
            footprintMinimum.y;

        if (!IsFinite(footprintWidth) ||
            !IsFinite(footprintHeight))
        {
            return Vector2.zero;
        }

        Vector2 correction =
            Vector2.zero;

        if (footprintWidth <
            allowedBounds.width - Epsilon)
        {
            if (footprintMinimum.x <
                allowedBounds.xMin)
            {
                correction.x =
                    allowedBounds.xMin -
                    footprintMinimum.x;
            }
            else if (footprintMaximum.x >
                     allowedBounds.xMax)
            {
                correction.x =
                    allowedBounds.xMax -
                    footprintMaximum.x;
            }
        }

        if (footprintHeight <
            allowedBounds.height - Epsilon)
        {
            if (footprintMinimum.y <
                allowedBounds.yMin)
            {
                correction.y =
                    allowedBounds.yMin -
                    footprintMinimum.y;
            }
            else if (footprintMaximum.y >
                     allowedBounds.yMax)
            {
                correction.y =
                    allowedBounds.yMax -
                    footprintMaximum.y;
            }
        }

        return correction;
    }

    /// <summary>
    /// Возвращает позицию камеры относительно focus point.
    ///
    /// tiltFromTopDegrees:
    /// 0 — камера строго сверху;
    /// увеличение значения — больший псевдо-3D наклон.
    /// </summary>
    public static Vector3 CreatePerspectiveOffset(
        float distance,
        float tiltFromTopDegrees,
        float yawDegrees)
    {
        float safeDistance =
            Mathf.Max(Epsilon, distance);

        float safeTilt =
            Mathf.Clamp(
                tiltFromTopDegrees,
                0f,
                55f);

        float radians =
            safeTilt *
            Mathf.Deg2Rad;

        Vector3 baseOffset =
            new Vector3(
                0f,
                -Mathf.Sin(radians),
                -Mathf.Cos(radians)) *
            safeDistance;

        Quaternion yawRotation =
            Quaternion.AngleAxis(
                yawDegrees,
                Vector3.forward);

        return yawRotation *
               baseOffset;
    }

    public static bool TryIntersectRayWithPlaneZ(
        Ray ray,
        float planeZ,
        out Vector3 point)
    {
        point =
            Vector3.zero;

        float denominator =
            ray.direction.z;

        if (Mathf.Abs(denominator) <
            Epsilon)
        {
            return false;
        }

        float distance =
            (planeZ - ray.origin.z) /
            denominator;

        if (distance < 0f ||
            !IsFinite(distance))
        {
            return false;
        }

        point =
            ray.origin +
            ray.direction * distance;

        return IsFinite(point);
    }

    public static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    public static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

    public static bool IsFinite(
        Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }
}