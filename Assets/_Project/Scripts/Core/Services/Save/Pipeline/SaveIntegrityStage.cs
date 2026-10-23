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

    // Added fields alter JsonUtility's reserialization of old schema versions.
    // Verify the original legacy payload as well; never skip integrity validation.
    public SaveIntegrityStatus VerifySerializedJson(GameRuntimeState state, string json)
    {
        var status = Verify(state);
        if (status != SaveIntegrityStatus.Invalid || state?.Meta == null ||
            state.Meta.SaveDataVersion >= SaveDataVersions.MissionGameplay || string.IsNullOrWhiteSpace(json))
            return status;

        string expected = state.Meta.IntegrityChecksum;
        if (expected == null || expected.Length != 64)
            return SaveIntegrityStatus.Invalid;
        foreach (char c in expected)
            if (!Uri.IsHexDigit(c)) return SaveIntegrityStatus.Invalid;

        var compact = new StringBuilder(json.Length);
        bool inString = false;
        bool escaped = false;
        foreach (char c in json)
        {
            if (inString)
            {
                compact.Append(c);
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
            }
            else if (c == '"') { inString = true; compact.Append(c); }
            else if (!char.IsWhiteSpace(c)) compact.Append(c);
        }
        if (inString) return SaveIntegrityStatus.Invalid;
        string canonical = compact.ToString();
        string field = "\"IntegrityChecksum\":\"" + expected + "\"";
        int index = canonical.IndexOf(field, StringComparison.Ordinal);
        if (index < 0 || canonical.IndexOf(field, index + field.Length, StringComparison.Ordinal) >= 0)
            return SaveIntegrityStatus.Invalid;
        canonical = canonical.Remove(index, field.Length).Insert(index, "\"IntegrityChecksum\":\"\"");
        bool valid = FixedTimeEquals(expected, ComputeChecksum(canonical));
        if (valid)
            AppLog.Info(DebugLogChannel.Mission, "[MissionLifecycle] Legacy JSON integrity verified before migration.");
        return valid ? SaveIntegrityStatus.Valid : SaveIntegrityStatus.Invalid;
    }

    private static string ComputeChecksum(GameRuntimeState state)
    {
        string canonicalJson = JsonUtility.ToJson(state, false);
        return ComputeChecksum(canonicalJson);
    }

    private static string ComputeChecksum(string canonicalJson)
    {
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
