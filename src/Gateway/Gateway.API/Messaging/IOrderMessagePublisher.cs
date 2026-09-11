using Shared.Contracts.Messages;

namespace Gateway.API.Messaging;

public interface IOrderMessagePublisher
{
    Task PublishCreateOrderAsync(CreateOrderMessage message, CancellationToken cancellationToken = default);
}
