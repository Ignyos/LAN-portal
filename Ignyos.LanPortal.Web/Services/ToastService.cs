namespace Ignyos.LanPortal.Web.Services;

public enum ToastPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error
}

public sealed record ToastMessage(
    Guid Id,
    string Message,
    ToastKind Kind,
    ToastPosition Position,
    TimeSpan Duration);

public sealed class ToastService
{
    public event Action<ToastMessage>? ToastShown;

    public void Show(
        string message,
        ToastKind kind = ToastKind.Info,
        TimeSpan? duration = null,
        ToastPosition position = ToastPosition.TopRight)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ToastShown?.Invoke(new ToastMessage(
            Guid.NewGuid(),
            message,
            kind,
            position,
            duration ?? TimeSpan.FromSeconds(3)));
    }

    public void Success(string message, TimeSpan? duration = null, ToastPosition position = ToastPosition.TopRight)
        => Show(message, ToastKind.Success, duration, position);

    public void Info(string message, TimeSpan? duration = null, ToastPosition position = ToastPosition.TopRight)
        => Show(message, ToastKind.Info, duration, position);

    public void Warning(string message, TimeSpan? duration = null, ToastPosition position = ToastPosition.TopRight)
        => Show(message, ToastKind.Warning, duration, position);

    public void Error(string message, TimeSpan? duration = null, ToastPosition position = ToastPosition.TopRight)
        => Show(message, ToastKind.Error, duration, position);
}
