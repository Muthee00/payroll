namespace PayMaster.Web.Infrastructure;

/// <summary>Adds defensive HTTP response headers. All scripts, styles and fonts are self-hosted.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        // Swagger UI and the Hangfire dashboard ship inline scripts; they are admin-only and get a relaxed policy.
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/swagger") && !path.StartsWithSegments("/hangfire"))
            headers["Content-Security-Policy"] = ContentSecurityPolicy;

        return next(context);
    }
}
