using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>Broker settings (<c>RabbitMq__Host</c>, <c>RabbitMq__UserName</c>, <c>RabbitMq__Password</c>, ...).</summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Host name.</summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>AMQP port.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host.</summary>
    [Required]
    public string VirtualHost { get; set; } = "/";

    /// <summary>User name.</summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Topic exchange of the events.</summary>
    [Required]
    public string Exchange { get; set; } = Exchanges.Events;

    /// <summary>Publish attempts before an event is given up (logged).</summary>
    [Range(1, 10)]
    public int PublishAttempts { get; set; } = 3;

    /// <summary>Delivery attempts of a consumed message before it is dead-lettered.</summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay before a failed message is delivered again.</summary>
    [Range(1, 3600)]
    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>Unacknowledged messages a consumer receives at once.</summary>
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;
}
