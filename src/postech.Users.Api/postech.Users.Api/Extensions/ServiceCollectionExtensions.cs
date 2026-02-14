using System.Security.Claims;
using System.Text;
using postech.Users.Api.Application.Events;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Authorization;
using postech.Users.Api.Infrastructure.Data;
using postech.Users.Api.Infrastructure.Messaging;
using postech.Users.Api.Infrastructure.Repositories;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using postech.Users.Api.Application.Utils;
using postech.Users.Api.Domain.Enums;
using Serilog;

namespace postech.Users.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                                 ?? throw new InvalidOperationException("Database connection string is not configured");

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // Messaging
        services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();

        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqPort = configuration.GetValue<ushort>("RabbitMQ:Port", 5672);
        var rabbitMqUser = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMqPass = configuration["RabbitMQ:Password"] ?? "guest";
        var rabbitMqVHost = configuration["RabbitMQ:VirtualHost"] ?? "/";
        
        Log.Information("Configuring RabbitMQ with Host: {Host}, Port: {Port}, User: {User}, VirtualHost: {VHost}",
            rabbitMqHost,
            rabbitMqPort,
            rabbitMqUser,
            rabbitMqVHost);
        
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqHost, rabbitMqPort, rabbitMqVHost, h =>
                {
                    h.Username(rabbitMqUser);
                    h.Password(rabbitMqPass);
                    
                    h.RequestedConnectionTimeout(TimeSpan.FromSeconds(30));
                    h.Heartbeat(TimeSpan.FromSeconds(10));
                });
                
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.PrefetchCount = 16;
                
                cfg.Message<UserCreatedEvent>(e => e.SetEntityName("user-created"));
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["JwtSettings:SecretKey"] 
                        ?? throw new InvalidOperationException("JWT Secret is not configured");

        var jwtIssuer = configuration["JwtSettings:Issuer"]
                        ?? throw new InvalidOperationException("JWT Issuer is not configured");

        var jwtAudience = configuration["JwtSettings:Audience"]
                          ?? throw new InvalidOperationException("JWT Audience is not configured");

        var key = Encoding.UTF8.GetBytes(jwtSecret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Dev only
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role
            };
        });
    
        // Configurar Policies
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireAdminRole, policy => policy.RequireRole(UserRoles.Administrator.ToString()))
            .AddPolicy(Policies.RequireUserRole, policy => policy.RequireRole(UserRoles.User.ToString(), UserRoles.Administrator.ToString()));

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