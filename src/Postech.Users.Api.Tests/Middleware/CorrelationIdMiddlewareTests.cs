using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using postech.Users.Api.Application.Utils;
using postech.Users.Api.Middleware;

namespace postech.Users.Api.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private readonly ICorrelationContext _correlationContext = Substitute.For<ICorrelationContext>();
    private readonly ILogger<CorrelationIdMiddleware> _logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
    
    private CorrelationIdMiddleware GetMiddleware(RequestDelegate next) => new CorrelationIdMiddleware(next, _logger);

    [Fact]
    public async Task InvokeAsync_WithValidCorrelationIdHeader_ShouldUseProvidedId()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = expectedCorrelationId.ToString();
        
        var nextCalled = false;
        var next = new RequestDelegate(async ctx =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        nextCalled.Should().BeTrue();
        _correlationContext.CorrelationId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidCorrelationIdHeader_ShouldGenerateNewId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "invalid-guid";
        
        var nextCalled = false;
        var next = new RequestDelegate(async ctx =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        nextCalled.Should().BeTrue();
        _correlationContext.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationIdHeader_ShouldGenerateNewId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        
        var nextCalled = false;
        var next = new RequestDelegate(async ctx =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        nextCalled.Should().BeTrue();
        _correlationContext.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetCorrelationIdInContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var expectedCorrelationId = Guid.NewGuid();
        context.Request.Headers["X-Correlation-Id"] = expectedCorrelationId.ToString();
        
        var next = new RequestDelegate(async ctx =>
        {
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        _correlationContext.CorrelationId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetCorrelationIdInHttpContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var expectedCorrelationId = Guid.NewGuid();
        context.Request.Headers["X-Correlation-Id"] = expectedCorrelationId.ToString();
        
        var next = new RequestDelegate(async ctx =>
        {
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        context.Items["CorrelationId"].Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var next = new RequestDelegate(async ctx =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithValidAndInvalidHeaders_ShouldBeConsistent()
    {
        // Arrange
        var validId = Guid.NewGuid();
        var context1 = new DefaultHttpContext();
        context1.Request.Headers["X-Correlation-Id"] = validId.ToString();
        
        var context2 = new DefaultHttpContext();
        context2.Request.Headers["X-Correlation-Id"] = "invalid";

        var correlationContext1 = Substitute.For<ICorrelationContext>();
        var correlationContext2 = Substitute.For<ICorrelationContext>();

        var next = new RequestDelegate(async ctx => await Task.CompletedTask);
        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context1, correlationContext1);
        await middleware.InvokeAsync(context2, correlationContext2);

        // Assert
        correlationContext1.CorrelationId.Should().Be(validId);
        correlationContext2.CorrelationId.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task InvokeAsync_ShouldAddCorrelationIdToResponseHeader()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = expectedCorrelationId.ToString();
        
        var next = new RequestDelegate(async ctx =>
        {
            await Task.CompletedTask;
        });

        var middleware = GetMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _correlationContext);

        // Assert
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(expectedCorrelationId.ToString());
    }
}
