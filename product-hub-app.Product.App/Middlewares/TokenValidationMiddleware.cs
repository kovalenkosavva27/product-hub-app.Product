using System.IdentityModel.Tokens.Jwt;

namespace product_hub_app.Product.App.Middlewares
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var currentToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(currentToken);

            if (token.ValidTo.Subtract(DateTime.UtcNow) < TimeSpan.Zero)
            {
                throw new ApplicationException("Токен недействителен");
            }

            await _next(context);
        }
    }
}
