using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>One shared broker connection for the process; channels are created per operation.</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private bool _topologyDeclared;

    /// <summary>Returns the open connection, connecting (and declaring the exchange) when needed.</summary>
    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } open && _topologyDeclared)
        {
            return open;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }

                var settings = options.Value;
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    Port = settings.Port,
                    VirtualHost = settings.VirtualHost,
                    UserName = settings.UserName,
                    Password = settings.Password,
                    ClientProvidedName = AppDomain.CurrentDomain.FriendlyName,
                };
                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _topologyDeclared = false;
            }

            if (!_topologyDeclared)
            {
                await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true,
                    autoDelete: false, cancellationToken: cancellationToken);
                _topologyDeclared = true;
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}
