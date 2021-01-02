using System;
using RabbitMQ.Client;

namespace MessageQueuing
{
    public interface IRabbitMqConnection : IDisposable
    {
        bool IsConnected { get; }
        bool TryConnect();
        IModel CreateModel();
    }
}