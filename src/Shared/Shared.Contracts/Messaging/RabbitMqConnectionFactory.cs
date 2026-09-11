using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Shared.Contracts.Messaging;

public class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = opts.HostName,
            Port = opts.Port,
            UserName = opts.UserName,
            Password = opts.Password
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }
}
