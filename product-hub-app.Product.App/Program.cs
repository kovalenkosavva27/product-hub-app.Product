using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using product_hub_app.Product.App.Middlewares;
using product_hub_app.Product.Bll;
using product_hub_app.Product.Bll.DbConfiguration;
using product_hub_app.Product.Bll.RabbitMq;
using product_hub_app.Product.Contracts.Interfaces;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();
IConfiguration keycloakConfig = configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakConfig["auth-server-url"] + "realms/" + keycloakConfig["realm"];
        options.Audience = keycloakConfig["resource"];
        options.RequireHttpsMetadata = keycloakConfig["ssl-required"] != "none"; // Проверка HTTPS
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = keycloakConfig["verify-token-audience"] == "true",
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "Product_";
});
builder.Services.AddHostedService<RabbitMqListener>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddControllers();
builder.Services.AddDbContext<ProductDbContext>(
    options =>
    {
        var connectionString = configuration.GetConnectionString("Product");

        options.UseNpgsql(connectionString);
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{keycloakConfig["auth-server-url"]}realms/{keycloakConfig["realm"]}/protocol/openid-connect/auth"),
                TokenUrl = new Uri($"{keycloakConfig["auth-server-url"]}realms/{keycloakConfig["realm"]}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
            {
                { "openid", "OpenID" },
                { "profile", "Profile" },
            }

            }
        }

    });
    c.OperationFilter<SecurityRequirementsOperationFilter>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TokenValidationMiddleware>();
app.MapControllers();

app.Run();
