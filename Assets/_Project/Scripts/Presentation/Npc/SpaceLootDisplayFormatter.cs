using UnityEngine;

public static class SpaceLootDisplayFormatter
{
    public static string Format(int quantity, int unitCargoSize)
    {
        int count = Mathf.Max(0, quantity);
        long cargo = (long)count * Mathf.Max(0, unitCargoSize);
        return "Количество: " + count +
               "\nМесто в трюме: " + cargo;
    }
}
