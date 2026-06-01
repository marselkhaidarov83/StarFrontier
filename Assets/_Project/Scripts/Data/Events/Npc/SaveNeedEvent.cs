using System;

public readonly struct SaveNeedEvent
{
    public readonly string Reason;

    public SaveNeedEvent(string reason)
    {
        Reason = reason;
    }
}