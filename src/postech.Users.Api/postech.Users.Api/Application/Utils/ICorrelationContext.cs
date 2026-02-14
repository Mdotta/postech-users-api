namespace postech.Users.Api.Application.Utils;

public interface ICorrelationContext
{
    Guid CorrelationId { get; set; }
}

