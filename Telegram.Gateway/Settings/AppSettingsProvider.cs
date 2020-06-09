using System.Net.NetworkInformation;

namespace Telegram.Gateway.MqttClient.Settings
{
    public class AppSettingsProvider
    {
        public static BrokerHostSettings BrokerHostSettings;
        public static ClientSettings ClientSettings;
        public static BrokerTopics BrokerTopics;
        public static TelegramSettings TelegramSettings;
    }
}
