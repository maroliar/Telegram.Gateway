using MQTTnet.Client.Options;
using System;

namespace Telegram.Gateway.MqttClient.Options
{
    public class MosquittoMqttClientOptionBuilder : MqttClientOptionsBuilder
    {
        public IServiceProvider ServiceProvider { get; }

        public MosquittoMqttClientOptionBuilder(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }        
    }
}
