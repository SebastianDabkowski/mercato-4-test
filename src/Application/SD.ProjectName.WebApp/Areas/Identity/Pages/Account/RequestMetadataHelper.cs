namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account
{
    internal static class RequestMetadataHelper
    {
        public static string? GetClientIp(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(ip) ? null : ip;
        }

        public static string? GetUserAgent(HttpContext context)
        {
            var userAgent = context.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
        }
    }
}
