using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SaveIntegrityStage
{
    public void Stamp(GameRuntimeState state)
    {
        if (state?.Meta == null)
            throw new ArgumentException(
                "Save state and Meta are required.",
                nameof(state));

        state.Meta.IntegrityChecksum = string.Empty;
        state.Meta.IntegrityChecksum = ComputeChecksum(state);
    }

    public SaveIntegrityStatus Verify(GameRuntimeState state)
    {
        if (state?.Meta == null)
            return SaveIntegrityStatus.Invalid;

        string expectedChecksum = state.Meta.IntegrityChecksum;

        if (string.IsNullOrWhiteSpace(expectedChecksum))
            return SaveIntegrityStatus.Missing;

        state.Meta.IntegrityChecksum = string.Empty;

        string actualChecksum;

        try
        {
            actualChecksum = ComputeChecksum(state);
        }
        finally
        {
            state.Meta.IntegrityChecksum = expectedChecksum;
        }

        return FixedTimeEquals(expectedChecksum, actualChecksum)
            ? SaveIntegrityStatus.Valid
            : SaveIntegrityStatus.Invalid;
    }

    private static string ComputeChecksum(GameRuntimeState state)
    {
        string canonicalJson = JsonUtility.ToJson(state, false);
        byte[] payload = Encoding.UTF8.GetBytes(canonicalJson);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(payload);
            var result = new StringBuilder(hash.Length * 2);

            foreach (byte value in hash)
                result.Append(value.ToString("x2"));

            return result.ToString();
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;

        int difference = 0;

        for (int index = 0; index < left.Length; index++)
            difference |= left[index] ^ right[index];

        return difference == 0;
    }
}

public enum SaveIntegrityStatus
{
    Valid = 0,
    Missing = 1,
    Invalid = 2
}
