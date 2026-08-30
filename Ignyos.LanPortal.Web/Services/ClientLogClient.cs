using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Web.Services;

/// <summary>Forwards client-side failures to the Host so they appear in the Host log viewer.</summary>
public sealed class ClientLogClient(HttpClient httpClient, AuthSession authSession, ILogger<ClientLogClient> logger)
{
    public async Task ReportErrorAsync(
        string message,
        Exception? exception = null,
        string? source = null,
        string? correlationId = null,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(authSession.AccessToken))
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/client-logs")
            {
                Content = JsonContent.Create(new ClientLogRequestDto(
                    message,
                    "Error",
                    source ?? "WebClient",
                    correlationId,
                    exception?.GetType().Name,
                    exception is null ? null : DescribeException(exception),
                    detailsJson))
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authSession.AccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Client log endpoint returned {StatusCode}.", response.StatusCode);
            }
        }
        catch (Exception reportingException)
        {
            // Reporting must never replace or mask the original client-side failure.
            logger.LogWarning(reportingException, "Unable to report client error to the Host.");
        }
    }

    /// <summary>Flattens inner exceptions so wrappers like "Error while copying content to a stream" stay diagnosable.</summary>
    public static string DescribeException(Exception exception)
    {
        var messages = new List<string>();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            var text = current.Message.Trim();
            if (!string.IsNullOrWhiteSpace(text) && !messages.Contains(text, StringComparer.Ordinal))
            {
                messages.Add(text);
            }
        }

        return string.Join(" -> ", messages);
    }
}
