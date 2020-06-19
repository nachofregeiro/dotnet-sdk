using GlobalPayments.Api.Utils;
using System;

namespace GlobalPayments.Api.Entities.GpApi {
    internal class TransactionInfo : GpApiEntity {
        public string Id { get; private set; }
        public DateTime TimeCreated { get; set; }
        public string Status { get; private set; }

        internal override void FromJson(JsonDoc doc) {
            Id = doc.GetValue<string>("id");
            TimeCreated = doc.GetValue<DateTime>("time_created");
            Status = doc.GetValue<string>("status");
        }

        internal static GpApiRequest GetTransactionRequest(string id) {
            return new GpApiRequest {
                Endpoint = $"/ucp/transactions/{id}",
            };
        } 
    }
}
