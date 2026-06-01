using Hangfire.Dashboard;

namespace swipefilm.Auth
{
    public class HangfireAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // ✅ En dev → tout le monde accède
            if (httpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment())
                return true;

            // ✅ En prod → seulement les users authentifiés
            return httpContext.User.Identity?.IsAuthenticated ?? false;
        }
    }
}