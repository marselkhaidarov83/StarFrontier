using UnityEngine;

public static class SystemTravelMath
{
    public static SystemTravelMathResult MoveTowards(
        Vector3 currentPosition,
        Vector3 startPosition,
        Vector3 destinationPosition,
        float speed,
        float deltaTime,
        float arrivalDistanceThreshold)
    {
        currentPosition.z = -2f;
        startPosition.z = -2f;
        destinationPosition.z = -2f;

        Vector3 direction = destinationPosition - currentPosition;
        float distanceToDestination = direction.magnitude;

        if (distanceToDestination <= arrivalDistanceThreshold)
        {
            return new SystemTravelMathResult(
                destinationPosition,
                1f,
                true
            );
        }

        float movementDistance = Mathf.Max(0f, speed) * deltaTime;

        if (movementDistance >= distanceToDestination)
        {
            return new SystemTravelMathResult(
                destinationPosition,
                1f,
                true
            );
        }

        Vector3 newPosition = currentPosition + direction.normalized * movementDistance;

        float totalDistance = Vector3.Distance(startPosition, destinationPosition);
        float remainingDistance = Vector3.Distance(newPosition, destinationPosition);

        float progress01 = totalDistance > 0f
            ? Mathf.Clamp01(1f - remainingDistance / totalDistance)
            : 1f;

        return new SystemTravelMathResult(
            newPosition,
            progress01,
            false
        );
    }
}