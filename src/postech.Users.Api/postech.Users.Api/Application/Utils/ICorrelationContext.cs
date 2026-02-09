namespace postech.Users.Api.Application.Utils;

public interface ICorrelationContext
{
    Guid CorrelationId { get; set; }
}

public class CorrelationContext : ICorrelationContext
{
    public Guid CorrelationId { get; set; }
}