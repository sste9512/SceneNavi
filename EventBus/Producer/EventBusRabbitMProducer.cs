using System;
using MessageQueuing.Events;

namespace MessageQueuing.Producer
{
    public class EventBusRabbitMProducer
    {

        private readonly IRabbitMqConnection _connection;

        public EventBusRabbitMProducer(IRabbitMqConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void PublishErfSave(string queueName, OnErfSavedEvent erfSavedEvent)
        {
            using var channel = _connection.CreateModel();

        }
    }
}
