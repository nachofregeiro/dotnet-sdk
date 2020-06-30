using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Gateways;
using System;

namespace GlobalPayments.Api
{
    public class GpApiConfig : GatewayConfig {
        public string AppId { get; set; }
        public string AppKey { get; set; }
        public string Nonce { get; set; } = "transactionsapi";
        //public int? SecondsToExpire { get; set; }
        //public string IntervalToExpire { get; set; }

        public GpApiConfig() : base(GatewayProvider.GP_Api) { }

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
