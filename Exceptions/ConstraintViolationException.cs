namespace Social_Media_Studio.Exceptions;

public class ConstraintViolationException : Exception
{
    public string BrokenRule { get; }
    public string Platform { get; }

    public ConstraintViolationException(string platform, string brokenRule, string message)
        : base(message)
    {
        Platform = platform;
        BrokenRule = brokenRule;
    }
}
