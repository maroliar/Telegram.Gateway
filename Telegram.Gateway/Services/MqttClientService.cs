using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Gateway.MqttClient.Entities;
using Telegram.Gateway.MqttClient.Settings;

namespace Telegram.Gateway.MqttClient.Services
{
    public class MqttClientService : IMqttClientService
    {
        private readonly IMqttClientOptions _options;
        private readonly ILogger<MqttClientService> _logger;

        private readonly IMqttClient mqttClient;
        private TelegramBotClient bot;

        private readonly BrokerTopics brokerTopics = AppSettingsProvider.BrokerTopics;
        private readonly ClientSettings clientSettings = AppSettingsProvider.ClientSettings;
        private readonly TelegramSettings telegramSettings = AppSettingsProvider.TelegramSettings;

        public MqttClientService(IMqttClientOptions options, ILogger<MqttClientService> logger)
        {
            _options = options;
            _logger = logger;
            mqttClient = new MqttFactory().CreateMqttClient();
            ConfigureMqttClient();

            bot = new TelegramBotClient(telegramSettings.Token);
            bot.OnMessage += HandleTelegramMessageReceivedAsync;
            bot.StartReceiving();
        }

        #region Telegram Events
        
        private async void HandleTelegramMessageReceivedAsync(object sender, MessageEventArgs e)
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

        public async Task SendTelegramMessage(long chatId, string message)
        {
            await bot.SendTextMessageAsync(chatId, message);
        }

        #endregion

        #region Mqtt Events

        private void ConfigureMqttClient()
        {
            mqttClient.ConnectedHandler = this;
            //mqttClient.DisconnectedHandler = this;
            mqttClient.ApplicationMessageReceivedHandler = this;
        }

        public async Task HandleConnectedAsync(MqttClientConnectedEventArgs eventArgs)
        {
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegram).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramSaida).Build());
        }

        //public Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
        //{
        //    // ação de gravar no log a desconeccao
        //    throw new NotImplementedException();
        //}

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await mqttClient.ConnectAsync(_options);

            // anuncia status online
            Payload payload = new Payload
            {
                device = clientSettings.Id,
                source = "Internal",
                message = "Online"
            };

            var serializedDeviceStatus = PrepareMsgToBroker(payload);
            await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegram, serializedDeviceStatus);

            _logger.LogInformation(serializedDeviceStatus);

            if (!mqttClient.IsConnected)
            {
                await mqttClient.ReconnectAsync();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // o app ser encerrado

            if (cancellationToken.IsCancellationRequested)
            {
                var disconnectOption = new MqttClientDisconnectOptions
                {
                    ReasonCode = MqttClientDisconnectReason.NormalDisconnection,
                    ReasonString = "NormalDiconnection"
                };
                await mqttClient.DisconnectAsync(disconnectOption, cancellationToken);
            }
            await mqttClient.DisconnectAsync();

            _logger.LogInformation("Encerrando...");
        }


        public async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            try
            {
                var jsonPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
                var topic = eventArgs.ApplicationMessage.Topic;

                if (topic.Contains(brokerTopics.TopicoGatewayTelegramSaida))
                {
                    _logger.LogInformation(string.Format("Nova mensagem recebida do broker: {0}", jsonPayload));

                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);

                    if (!string.IsNullOrEmpty(payload.message))
                    {
                        // must be a number!
                        long chatId = long.Parse(payload.device);

                        // ENVIA A MENSAGEM PARA O TELEGRAM
                        await SendTelegramMessage(chatId, payload.message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar ler a mensagem: " + ex.Message);
            }
        }

        public string PrepareMsgToBroker(Payload payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return jsonPayload;
        }

        public async Task PublishMqttClientAsync(string topic, string payload, bool retainFlag = false, int qos = 0)
        {
            _logger.LogInformation(string.Format("Enviando mensagem para o broker: {0}", payload));
            _logger.LogInformation(string.Format("Topico: {0}", topic));

            await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .WithRetainFlag(retainFlag)
                .Build());
        }

        #endregion
    }
}
