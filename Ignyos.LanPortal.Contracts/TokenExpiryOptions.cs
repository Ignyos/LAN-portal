namespace Ignyos.LanPortal.Contracts;

public sealed record TokenExpiryOptionDto(string Value, string Label);

/// <summary>Single source of truth for the approval token-expiry choices shared by the host and client admin UIs.</summary>
public static class TokenExpiryOptions
{
    public const string NeverValue = "never";
    public const string CustomValue = "custom";
    public const string DefaultValue = "60";

    public const int MinMinutes = 5;
    public const int MaxHours = 87600;
    public const int MaxMinutes = MaxHours * 60;
    public const int DefaultCustomHours = 24;

    public static IReadOnlyList<TokenExpiryOptionDto> All { get; } =
    [
        new("60", "1 hour"),
        new("1440", "1 day"),
        new("10080", "1 week"),
        new(NeverValue, "Never"),
        new(CustomValue, "Custom")
    ];

    /// <summary>Converts a selected option into token minutes; null means the token never expires.</summary>
    public static int? ToTokenMinutes(string? option, int customHours)
    {
        if (string.Equals(option, NeverValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(option, CustomValue, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(customHours, 1, MaxHours) * 60;
        }

        return int.TryParse(option, out var minutes)
            ? Math.Clamp(minutes, MinMinutes, MaxMinutes)
            : int.Parse(DefaultValue);
    }
}
