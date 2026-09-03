using Social_Media_Studio.Models;

namespace Social_Media_Studio.Services.Interfaces;

public interface IConstraintValidator
{
    ValidationResult Validate(string platform, string content);
    ConstraintProfile? GetProfile(string platform);
}
