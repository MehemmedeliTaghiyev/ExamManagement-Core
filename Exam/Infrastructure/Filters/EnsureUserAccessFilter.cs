using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Exam.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Exam.Infrastructure.Filters
{
    public class EnsureUserAccessFilter : IAsyncActionFilter
    {
        private readonly IUserAdminService _users;

        public EnsureUserAccessFilter(IUserAdminService users)
        {
            _users = users;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var allowAnonymous = context.ActionDescriptor.EndpointMetadata.Any(m => m is AllowAnonymousAttribute);
            if (allowAnonymous)
            {
                await next();
                return;
            }

            if (RoleClaims.IsAdmin(context.HttpContext.User))
            {
                await next();
                return;
            }

            var userId = RoleClaims.UserId(context.HttpContext.User);
            if (userId is null or <= 0)
            {
                await next();
                return;
            }

            var allowed = await _users.IsAccessAllowedAsync(userId.Value);
            if (!allowed)
            {
                context.Result = new ObjectResult(new
                {
                    code = "ACCESS_CLOSED",
                    message = "Hesabınız bağlanıb. Giriş üçün adminə müraciət edin."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
