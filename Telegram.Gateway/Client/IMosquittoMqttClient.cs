using System.Threading.Tasks;

namespace Telegram.Gateway.MqttClient.Client
{
    public interface IMosquittoMqttClient
    {
        Task StartMqttClientAsync();
        Task StopMqttClientAsync();
    }
}
