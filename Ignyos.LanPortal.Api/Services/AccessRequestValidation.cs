namespace Ignyos.LanPortal.Api.Services;

public static class AccessRequestValidation
{
    public const int MaxUserNameLength = 128;
    public const int MaxDeviceNameLength = 128;
    public const int MaxReasonLength = 512;
    public const int MaxSourceIpLength = 64;
    public const int MaxUserAgentLength = 1024;
    public const int MaxRolesLength = 512;

    public static string? ValidateUserName(string? value)
        => string.IsNullOrWhiteSpace(value) ? "UserName is required." :
            value.Trim().Length > MaxUserNameLength ? $"UserName cannot exceed {MaxUserNameLength} characters." : null;

    public static string? ValidateDeviceName(string? value)
        => string.IsNullOrWhiteSpace(value) ? "DeviceName is required." :
            value.Trim().Length > MaxDeviceNameLength ? $"DeviceName cannot exceed {MaxDeviceNameLength} characters." : null;

    public static string? ValidateRoles(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Roles is required." :
            value.Trim().Length > MaxRolesLength ? $"Roles cannot exceed {MaxRolesLength} characters." : null;

    public static string? ValidateReason(string? value)
        => value is not null && value.Length > MaxReasonLength
            ? $"Reason cannot exceed {MaxReasonLength} characters."
            : null;

    public static string? ValidateSourceIp(string? value)
        => value is not null && value.Length > MaxSourceIpLength
            ? $"SourceIp cannot exceed {MaxSourceIpLength} characters."
            : null;

    public static string? ValidateUserAgent(string? value)
        => value is not null && value.Length > MaxUserAgentLength
            ? $"UserAgent cannot exceed {MaxUserAgentLength} characters."
            : null;
}
