namespace Ignyos.LanPortal.Api.Services;

public interface IValueProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
