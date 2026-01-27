using Microsoft.AspNetCore.Mvc;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Services;

namespace postech.Users.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Register a new user")
            .Produces(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
        
        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Login a user")
            .Produces<LoginResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.LoginAsync(request, cancellationToken);
        
        if (result.IsFailure)
        {
            return Results.Unauthorized();
        }
        var response = new LoginResponse(result.Value);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterUserRequest request,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.RegisterAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Register failed",
                Detail = result.Error
            });
        }

        return TypedResults.Created($"/api/users/{result.Value.Id}", result.Value);
    }
}