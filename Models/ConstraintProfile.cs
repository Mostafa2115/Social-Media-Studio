namespace Social_Media_Studio.Models;

public class ConstraintProfile
{
    public string Platform { get; set; } = string.Empty;
    public int MaxLength { get; set; }
    public int MinLength { get; set; } = 1;
    public int MinHashtags { get; set; } = 0;
    public int MaxHashtags { get; set; } = 10;
    public List<string> RequiredKeywords { get; set; } = new();
    public List<string> ProhibitedWords { get; set; } = new();

    public static readonly Dictionary<string, ConstraintProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["X"] = new ConstraintProfile
        {
            Platform = "X",
            MaxLength = 280,
            MinLength = 10,
            MinHashtags = 1,
            MaxHashtags = 3,
            ProhibitedWords = new List<string> { "click here now", "buy cheap" }
        },
        ["LinkedIn"] = new ConstraintProfile
        {
            Platform = "LinkedIn",
            MaxLength = 3000,
            MinLength = 50,
            MinHashtags = 2,
            MaxHashtags = 5,
            ProhibitedWords = new List<string> { "rt this", "viral tweet" }
        },
        ["Telegram"] = new ConstraintProfile
        {
            Platform = "Telegram",
            MaxLength = 4096,
            MinLength = 10,
            MinHashtags = 0,
            MaxHashtags = 5,
            ProhibitedWords = new List<string>()
        }
    };
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? BrokenRule { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string rule, string message) => new() 
    { 
        IsValid = false, 
        BrokenRule = rule, 
        ErrorMessage = message 
    };
}
