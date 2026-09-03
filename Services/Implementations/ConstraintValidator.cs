using System.Text.RegularExpressions;
using Social_Media_Studio.Models;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Implementations;

public class ConstraintValidator : IConstraintValidator
{
    private static readonly Regex HashtagRegex = new(@"#\w+", RegexOptions.Compiled);

    public ConstraintProfile? GetProfile(string platform)
    {
        if (ConstraintProfile.Profiles.TryGetValue(platform, out var profile))
        {
            return profile;
        }
        return null;
    }

    public ValidationResult Validate(string platform, string content)
    {
        var profile = GetProfile(platform);
        if (profile == null)
        {
            return ValidationResult.Fail("UnknownPlatform", $"Platform '{platform}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return ValidationResult.Fail("ContentEmpty", "Post content cannot be empty.");
        }

        if (content.Length > profile.MaxLength)
        {
            return ValidationResult.Fail(
                "MaxLengthExceeded",
                $"Content length ({content.Length}) exceeds {platform} maximum allowed length of {profile.MaxLength} characters."
            );
        }

        if (content.Length < profile.MinLength)
        {
            return ValidationResult.Fail(
                "MinLengthNotMet",
                $"Content length ({content.Length}) is below {platform} minimum required length of {profile.MinLength} characters."
            );
        }

        var hashtags = HashtagRegex.Matches(content);
        if (hashtags.Count < profile.MinHashtags)
        {
            return ValidationResult.Fail(
                "MinHashtagsNotMet",
                $"Platform {platform} requires at least {profile.MinHashtags} hashtags, but found {hashtags.Count}."
            );
        }

        if (hashtags.Count > profile.MaxHashtags)
        {
            return ValidationResult.Fail(
                "MaxHashtagsExceeded",
                $"Platform {platform} allows at most {profile.MaxHashtags} hashtags, but found {hashtags.Count}."
            );
        }

        foreach (var word in profile.ProhibitedWords)
        {
            if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Fail(
                    "ProhibitedWordFound",
                    $"Platform {platform} prohibits the phrase '{word}'."
                );
            }
        }

        return ValidationResult.Success();
    }
}
