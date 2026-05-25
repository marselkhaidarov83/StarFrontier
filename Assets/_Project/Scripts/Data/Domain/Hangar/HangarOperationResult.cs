public class HangarOperationResult
{
    public bool Success;
    public HangarError Error;

    public static HangarOperationResult Ok()
    {
        return new HangarOperationResult
        {
            Success = true,
            Error = HangarError.None
        };
    }

    public static HangarOperationResult Fail(HangarError error)
    {
        return new HangarOperationResult
        {
            Success = false,
            Error = error
        };
    }
}