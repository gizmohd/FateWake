namespace Fatewake.Infrastructure.Authentication;

/// <summary>Story content levels in order of increasing maturity.</summary>
public enum ContentLevel
{
    /// <summary>Clean language and mild peril.</summary>
    Under14,
    /// <summary>Clean language and teen-appropriate themes.</summary>
    Teen,
    /// <summary>Adult themes with clean language.</summary>
    AdultClean,
    /// <summary>Adult themes with optional strong language.</summary>
    AdultLanguage
}

/// <summary>Available and selected content levels for the signed-in account.</summary>
public sealed record ContentPreferences(bool NeedsBirthDate, ContentLevel MaximumLevel, ContentLevel Level);

/// <summary>Updates the content level and records the birth date for a legacy account if needed.</summary>
public sealed record ContentPreferenceUpdate(ContentLevel? Level, DateOnly? BirthDate = null);

/// <summary>Applies the account age cap and clean-language defaults on the server.</summary>
public static class ContentAgePolicy
{
    /// <summary>Checks whether a supplied birth date is present and has already occurred.</summary>
    public static bool IsValidBirthDate(DateOnly? birthDate, DateOnly today) =>
        birthDate is { } date && date <= today;

    /// <summary>Builds the effective preferences for the current day.</summary>
    public static ContentPreferences Resolve(DateOnly? birthDate, ContentLevel? requested, DateOnly today)
    {
        if (!IsValidBirthDate(birthDate, today) || birthDate is not { } date)
            return new(true, ContentLevel.Under14, ContentLevel.Under14);

        var age = today.Year - date.Year;
        if (date.AddYears(age) > today)
            age--;

        var maximum = age < 14 ? ContentLevel.Under14 : age < 18 ? ContentLevel.Teen : ContentLevel.AdultLanguage;
        var defaultLevel = maximum == ContentLevel.AdultLanguage ? ContentLevel.AdultClean : maximum;
        var level = requested is { } value && Enum.IsDefined(value) && value <= maximum ? value : defaultLevel;
        return new(false, maximum, level);
    }

    /// <summary>Current date for account age calculations.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
