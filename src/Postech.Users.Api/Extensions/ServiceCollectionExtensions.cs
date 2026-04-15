using Amazon.SimpleNotificationService;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Authorization;
using postech.Users.Api.Infrastructure.Data;
using postech.Users.Api.Infrastructure.Messaging;
using postech.Users.Api.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using postech.Users.Api.Application.Utils;
using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICognitoAuthService, CognitoAuthService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database — prefer environment variable, fall back to appsettings
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string is not configured. " +
                "Set the ConnectionStrings__DefaultConnection environment variable or ConnectionStrings:DefaultConnection in appsettings.");

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceUrl = configuration["AWS:ServiceURL"];

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
                new AmazonSimpleNotificationServiceClient(
                    new AmazonSimpleNotificationServiceConfig { ServiceURL = serviceUrl }
                ));
        }
        else
        {
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
            services.AddAWSService<IAmazonSimpleNotificationService>();
        }

        services.AddScoped<IEventPublisher, SnsEventPublisher>();
        return services;
    }
    
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var region     = configuration["CognitoSettings:Region"]     ?? "us-east-1";
        var userPoolId = configuration["CognitoSettings:UserPoolId"]
                         ?? throw new InvalidOperationException("CognitoSettings:UserPoolId is not configured");
        var clientId   = configuration["CognitoSettings:ClientId"]
                         ?? throw new InvalidOperationException("CognitoSettings:ClientId is not configured");

        var issuer = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority           = issuer;   // auto-fetches JWKS from Cognito
                options.Audience            = clientId;
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer           = true,
                    ValidIssuer              = issuer,
                    ValidateAudience         = false,   // Cognito uses 'client_id', not 'aud'
                    ValidateLifetime         = true,
                    RoleClaimType            = "cognito:groups" // roles assigned in Cognito groups
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireAdminRole, policy => policy.RequireRole(UserRoles.Administrator.ToString()))
            .AddPolicy(Policies.RequireUserRole,  policy => policy.RequireRole(UserRoles.User.ToString(), UserRoles.Administrator.ToString()));

        return services;
    }
    
    public static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                // Define the Bearer security scheme
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme."
                };

                // Apply global security requirement using the new syntax
                document.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                    }
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}