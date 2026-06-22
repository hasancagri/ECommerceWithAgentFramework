using Microsoft.AspNetCore.Diagnostics;

namespace WebApp.ExceptionHandlers;

public class UnauthorizedAccessExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is UnauthorizedAccessException)
        {
            httpContext.Response.Redirect("/Auth/SignIn");
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }
}