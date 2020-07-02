using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Gateway.MqttClient.Entities;
using Telegram.Gateway.MqttClient.Settings;

namespace Telegram.Gateway.MqttClient.Client
{
    public class MosquittoMqttClient : IMosquittoMqttClient
    {
        private readonly BrokerTopics brokerTopics = AppSettingsProvider.BrokerTopics;
        private readonly TelegramSettings telegramSettings = AppSettingsProvider.TelegramSettings;
        private readonly ClientSettings clientSettings = AppSettingsProvider.ClientSettings;
        
        private readonly TelegramBotClient bot;

        private readonly IMqttClientOptions Options;
        private readonly IMqttClient mqttClient;

        public MosquittoMqttClient(IMqttClientOptions options)
        {
            Options = options;
            mqttClient = new MqttFactory().CreateMqttClient();
            mqttClient.UseApplicationMessageReceivedHandler(OnMqttMessage);

            bot = new TelegramBotClient(telegramSettings.Token);
            bot.OnMessage += OnTelegramMessage;
            bot.StartReceiving();

            //Console.ReadKey();
            //bot.StopReceiving();
        }

        #region Telegram Events

        private async void OnTelegramMessage(object sender, MessageEventArgs e)
        {
            var brokerTopics = AppSettingsProvider.BrokerTopics;

            if (e.Message.Type == MessageType.Text)
            {
                // publica a mensagem no broker
                Payload payload = new Payload
                {
                    device = e.Message.Chat.Id.ToString(),
                    source = "Telegram",
                    message = e.Message.Text
                };

                var serializedPayload = PrepareMsgToBroker(payload);

                await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramEntrada, serializedPayload);
            }
        }

        public void SendTelegramMessage(long chatId, string message)
        {
            bot.SendTextMessageAsync(chatId, message);
        }

        #endregion

        #region Mqtt Events

        public void OnMqttMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            try
            {
                var jsonPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
                var topic = eventArgs.ApplicationMessage.Topic;

                if (topic.Contains(brokerTopics.TopicoGatewayTelegramSaida))
                {
                    Console.WriteLine("Nova mensagem recebida do broker: ");
                    Console.WriteLine(jsonPayload);

                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);

                    if (!string.IsNullOrEmpty(payload.message))
                    {
                        // must be a number!
                        long chatId = long.Parse(payload.device);

                        // ENVIA A MENSAGEM PARA O TELEGRAM
                        SendTelegramMessage(chatId, payload.message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao tentar ler a mensagem: " + ex.Message);
                //throw;
            }
        }

        public string PrepareMsgToBroker(Payload payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                //WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return jsonPayload;
        }

        public async Task PublishMqttClientAsync(string topic, string payload, bool retainFlag = false, int qos = 0)
        {
            Console.WriteLine("Enviando mensagem para o broker: ");
            Console.WriteLine(payload);

            Console.Write("Topico: ");
            Console.WriteLine(topic);

            await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .WithRetainFlag(retainFlag)
                .Build());
        }

        public async Task StartMqttClientAsync()
        {
            await mqttClient.ConnectAsync(Options);

            // anuncia status online
            Payload payload = new Payload
            {
                device = clientSettings.Id,
                source = "Internal",
                message = "Online"
            };

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegram).Build());

            var serializedDeviceStatus = PrepareMsgToBroker(payload);
            await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegram, serializedDeviceStatus);

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramSaida).Build());

            if (!mqttClient.IsConnected)
            {
                await mqttClient.ReconnectAsync();
            }
        }

        public async Task StopMqttClientAsync()
        {
            await mqttClient.DisconnectAsync();
        }

        #endregion
    }
}