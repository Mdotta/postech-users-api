using postech.Users.Api.Application.Utils;

namespace postech.Users.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var value)
            ? Guid.TryParse(value.ToString(), out var id) ? id : Guid.NewGuid()
            : Guid.NewGuid();

        correlationContext.CorrelationId = correlationId;
        context.Items["CorrelationId"] = correlationId;
        
        await _next(context);
    }
}