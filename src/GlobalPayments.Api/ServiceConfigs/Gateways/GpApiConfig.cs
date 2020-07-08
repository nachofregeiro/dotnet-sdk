using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Gateways;
using System;

namespace GlobalPayments.Api
{
    public class GpApiConfig : GatewayConfig {
        /// <summary>
        /// GP API app id
        /// </summary>
        public string AppId { get; set; }
        /// <summary>
        /// GP API app key
        /// </summary>
        public string AppKey { get; set; }
        /// <summary>
        /// A unique random value included while creating the secret
        /// </summary>
        public string Nonce { get; set; } = "transactionsapi";
        /// <summary>
        /// Channel
        /// </summary>
        public Channel Channel { get; set; } = Channel.ClientNotPresent;
        /// <summary>
        /// Language
        /// </summary>
        public Language Language { get; set; } = Language.English;

        public GpApiConfig() : base(GatewayProvider.GP_API) { }

        internal override void ConfigureContainer(ConfiguredServices services) {
            if (string.IsNullOrEmpty(ServiceUrl)) {
                if (Environment.Equals(Entities.Environment.TEST))
                    ServiceUrl = ServiceEndpoints.GP_API_TEST;
                else 
                    ServiceUrl = ServiceEndpoints.GP_API_PRODUCTION;
            }

            var gateway = new GpApiConnector
            {
                AppId = AppId,
                AppKey = AppKey,
                Nonce = Nonce,
                Channel = Channel,
                Language = Language,
                ServiceUrl = ServiceUrl,
                Timeout = Timeout
            };

            services.GatewayConnector = gateway;

            services.ReportingService = gateway;
        }

        internal override void Validate() {
            base.Validate();

            if (AppId == null || AppKey == null)
                throw new ConfigurationException("AppId and AppKey cannot be null.");
        }
    }
}
