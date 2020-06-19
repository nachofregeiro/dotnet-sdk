using GlobalPayments.Api.Utils;
using System;

namespace GlobalPayments.Api.Entities.GpApi {
    internal class TransactionsInfo : GpApiEntity {
        public string Id { get; private set; }
        public DateTime TimeCreated { get; set; }
        public string Status { get; private set; }

        internal override void FromJson(JsonDoc doc) {
            Id = doc.GetValue<string>("id");
            TimeCreated = doc.GetValue<DateTime>("time_created");
            Status = doc.GetValue<string>("status");
        }

        internal static GpApiRequest GetTransactionsRequest(int page, int pageSize, DateTime fromTimeCreated) {
            return new GpApiRequest {
                Endpoint = $"/ucp/transactions?page={page}&page_size={pageSize}&from_time_created={fromTimeCreated.ToString("yyyy-MM-dd")}",
                ResultsField = "transactions",
            };
        } 
    }
}
