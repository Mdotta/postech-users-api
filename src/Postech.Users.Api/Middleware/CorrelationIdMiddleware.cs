using postech.Users.Api.Application.Utils;
using Serilog.Context;

namespace postech.Users.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var value)
            ? Guid.TryParse(value.ToString(), out var id) ? id : Guid.NewGuid()
            : Guid.NewGuid();

        correlationContext.CorrelationId = correlationId;
        context.Items["CorrelationId"] = correlationId;
        
        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId.ToString());
        
        _logger.LogInformation("Request started for {Method} {Path}", 
            context.Request.Method, 
            context.Request.Path);
        
        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation("Request completed for {Method} {Path} with status {StatusCode}", 
                context.Request.Method, 
                context.Request.Path,
                context.Response.StatusCode);
        }
    }
}