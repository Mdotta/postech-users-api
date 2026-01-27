using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Authorization;

namespace postech.Users.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users");

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Get current authenticated user profile")
            .RequireAuthorization(Policies.RequireUserRole) 
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal user,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await userService.GetUserByIdAsync(userId, cancellationToken);

        if (result.IsFailure)
        {
            return Results.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = result.Error
            });
        }

        return Results.Ok(result.Value);
    }
}