using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Gateway.MqttClient.Extensions;
using Telegram.Gateway.MqttClient.Settings;

namespace Telegram.Gateway.MqttClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            MapConfiguration();
        }

        public IConfiguration Configuration { get; }

        private void MapConfiguration()
        {
            MapBrokerHostSettings();
            MapClientSettings();
            MapBrokerTopics();
            MapTelegramSettings();
        }

        private void MapBrokerHostSettings()
        {
            BrokerHostSettings brokerHostSettings = new BrokerHostSettings();
            Configuration.GetSection(nameof(BrokerHostSettings)).Bind(brokerHostSettings);
            AppSettingsProvider.BrokerHostSettings = brokerHostSettings;
        }

        private void MapClientSettings()
        {
            ClientSettings clientSettings = new ClientSettings();
            Configuration.GetSection(nameof(ClientSettings)).Bind(clientSettings);
            AppSettingsProvider.ClientSettings = clientSettings;
        }

        private void MapBrokerTopics()
        {
            BrokerTopics brokerTopics = new BrokerTopics();
            Configuration.GetSection(nameof(BrokerTopics)).Bind(brokerTopics);
            AppSettingsProvider.BrokerTopics = brokerTopics;
        }

        private void MapTelegramSettings()
        {
            TelegramSettings telegramSettings = new TelegramSettings();
            Configuration.GetSection(nameof(TelegramSettings)).Bind(telegramSettings);
            AppSettingsProvider.TelegramSettings = telegramSettings;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddMqttClientHostedService();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
