using Microsoft.AspNetCore.Http;

namespace Qec.Itmg.Identity.Admin;

internal static class AdminApiResults
{
    public static IResult ValidationError(string code, string message, string? field = null) =>
        Results.Json(
            new
            {
                error = new
                {
                    code,
                    message,
                    details = field is null
                        ? Array.Empty<object>()
                        : new object[] { new { field, code, message } },
                },
            },
            statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string code, string message) =>
        Results.Json(
            new { error = new { code, message, details = Array.Empty<object>() } },
            statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string code, string message) =>
        Results.Json(
            new { error = new { code, message, details = Array.Empty<object>() } },
            statusCode: StatusCodes.Status409Conflict);
}
