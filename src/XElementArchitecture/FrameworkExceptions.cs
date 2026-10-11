namespace XElementArchitecture;

public class XElementFrameworkException : Exception
{
    public XElementFrameworkException(string message) : base(message) { }
    public XElementFrameworkException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class XElementOptimizationException : XElementFrameworkException
{
    public XElementOptimizationException(string message) : base(message) { }
}

[Serializable]
public sealed class Error : XElementFrameworkException
{
    public Error(UIWrapper apm)
        : base($"Test Status: {Global.TEST_STATUS}; Test: {apm.Title}; Error: {apm.ErrorMsg}")
    {
        Global.TEST_STATUS = "FAILED";
    }
}
