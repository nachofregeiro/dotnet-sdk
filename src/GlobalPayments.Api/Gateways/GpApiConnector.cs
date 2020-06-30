using GlobalPayments.Api.Builders;
using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Entities.GpApi;
using GlobalPayments.Api.PaymentMethods;
using GlobalPayments.Api.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;

namespace GlobalPayments.Api.Gateways {
    internal class GpApiConnector : RestGateway, IPaymentGateway, IReportingService {
        public string AppId { get; set; }
        public string AppKey { get; set; }
        public string Nonce { get; set; }
        public string SessionToken { get; internal set; }

        public bool SupportsHostedPayments => throw new NotImplementedException();

        internal GpApiConnector() {
            // Set required api version header
            Headers["X-GP-Version"] = "2020-04-10"; //"2020-01-20";
            Headers["Accept"] = "application/json";
            Headers["Accept-Encoding"] = "gzip";
        }

        public void SignIn() {
            var request = SessionInfo.SignIn(AppId, AppKey, Nonce);

            var response = SendAccessTokenRequest(request);

            //if (!string.IsNullOrEmpty(response.ErrorMessage))
            //    throw new ApiException(response.ErrorMessage);

            SessionToken = response.Token;

            // Set the authorization header
            Headers["Authorization"] = $"Bearer {response.Token}";
        }

        public void SignOut() {
            //SendEncryptedRequest<SessionInfo>(SessionInfo.SignOut());
        }

        private GpApiTokenResponse SendAccessTokenRequest(GpApiRequest request) {
            var response = base.DoTransaction(HttpMethod.Post, request.Endpoint, request.RequestBody);

            return Activator.CreateInstance(typeof(GpApiTokenResponse), new object[] { response }) as GpApiTokenResponse;
        }

        public override string DoTransaction(HttpMethod verb, string endpoint, string data = null, Dictionary<string, string> queryStringParams = null)
        {
            if (string.IsNullOrEmpty(SessionToken))
            {
                SignIn();
            }
            return base.DoTransaction(verb, endpoint, data, queryStringParams);
        }

        public Transaction ProcessAuthorization(AuthorizationBuilder builder)
        {
            if (builder.PaymentMethod is ICardData)
            {
                if (builder.TransactionType != TransactionType.Sale && builder.TransactionType != TransactionType.Refund)
                {
                    throw new ApiException("Transaction type not supported for this payment method");
                }

                var paymentMethod = new JsonDoc()
                    //.Set("first_name", "")
                    //.Set("last_name", "")
                    .Set("entry_mode", "ECOM") // [MOTO, ECOM, IN_APP, CHIP, SWIPE, MANUAL, CONTACTLESS_CHIP, CONTACTLESS_SWIPE]
                    .Set("id", "PMT_31087d9c-e68c-4389-9f13-39378e166ea5");

                var cardData = builder.PaymentMethod as ICardData;

                var card = new JsonDoc()
                    .Set("number", cardData.Number)
                    .Set("expiry_month", cardData.ExpMonth.HasValue ? cardData.ExpMonth.ToString().PadLeft(2, '0') : string.Empty)
                    .Set("expiry_year", cardData.ExpYear.HasValue ? cardData.ExpYear.ToString().PadLeft(4, '0').Substring(2, 2) : string.Empty)
                    //.Set("track", "")
                    //.Set("tag", "")
                    //.Set("chip_condition", "") // [PREV_SUCCESS, PREV_FAILED]
                    //.Set("pin_block", "")
                    .Set("cvv", cardData.Cvn)
                    .Set("cvv_indicator", "PRESENT") // [ILLEGIBLE, NOT_PRESENT, PRESENT]
                    .Set("avs_address", "Flat 123")
                    .Set("avs_postal_code", "50001");
                //.Set("funding", "") // [DEBIT, CREDIT]
                //.Set("authcode", "")
                //.Set("brand_reference", "")

                paymentMethod.Set("card", card);

                var data = new JsonDoc()
                    .Set("account_name", "Transaction_Processing")
                    .Set("type", builder.TransactionType == TransactionType.Sale ? "SALE" : "REFUND") // [SALE, REFUND]
                    .Set("channel", "CNP") // [CP, CNP]
                    .Set("capture_mode", "AUTO") // [AUTO, LATER, MULTIPLE]
                                                 //.Set("remaining_capture_count", "")
                                                 //.Set("authorization_mode", "") // [PARTIAL, WHOLE]
                    .Set("amount", builder.Amount.ToNumericCurrencyString())
                    .Set("currency", builder.Currency)
                    .Set("reference", Guid.NewGuid())
                    //.Set("description", "")
                    //.Set("order_reference", "")
                    //.Set("initiator", "") // [PAYER, MERCHANT]
                    .Set("gratuity_amount", builder.Gratuity.ToNumericCurrencyString())
                    .Set("cashback_amount", builder.CashBackAmount.ToNumericCurrencyString())
                    //.Set("surcharge_amount", "")
                    .Set("convenience_amount", builder.ConvenienceAmount.ToNumericCurrencyString())
                    .Set("country", "US")
                    //.Set("language", "")
                    //.Set("ip_address", "")
                    //.Set("site_reference", "")
                    .Set("payment_method", paymentMethod);

                var response = DoTransaction(HttpMethod.Post, "/ucp/transactions", data.ToString());

                return MapResponse(response);
            }
            else if (builder.PaymentMethod is ITrackData)
            {
                if (builder.TransactionType == TransactionType.Refund)
                {
                    string id = (builder.PaymentMethod as CreditTrackData).Value;

                    var data = new JsonDoc()
                        .Set("amount", builder.Amount.ToNumericCurrencyString());

                    var response = DoTransaction(HttpMethod.Post, $"/ucp/transactions/{id}/refund", data.ToString());

                    return MapResponse(response);
                }
                else if (builder.TransactionType == TransactionType.Reversal)
                {
                    string id = (builder.PaymentMethod as CreditTrackData).Value;

                    var data = new JsonDoc()
                        .Set("amount", builder.Amount.ToNumericCurrencyString());

                    var response = DoTransaction(HttpMethod.Post, $"/ucp/transactions/{id}/reversal", data.ToString());

                    return MapResponse(response);
                }
            }

            throw new ApiException("Transaction type not implemented");
        }

        public Transaction ManageTransaction(ManagementBuilder builder) {
            throw new NotImplementedException();
        }

        public string SerializeRequest(AuthorizationBuilder builder) {
            throw new NotImplementedException();
        }

        private Transaction MapResponse(string rawResponse) {
            JsonDoc json = JsonDoc.Parse(rawResponse);

            Transaction transaction = new Transaction();

            // ToDo: Map transaction values
            transaction.TransactionId = json.GetValue<string>("id");
            transaction.Timestamp = json.GetValue<string>("time_created");
            transaction.ResponseMessage = json.GetValue<string>("status");
            transaction.ReferenceNumber = json.GetValue<string>("reference");
            transaction.BatchSummary = new BatchSummary
            {
                SequenceNumber = json.GetValue<string>("batch_id")
            };
            transaction.ResponseCode = json.GetValue<JsonDoc>("action").GetValue<string>("result_code");

            return transaction;
        }

        public T ProcessReport<T>(ReportBuilder<T> builder) where T : class
        {
            string reportUrl = "/ucp/transactions";
            Dictionary<string, string> queryStringParams = new Dictionary<string, string>();

            if (builder is TransactionReportBuilder<T>)
            {
                var trb = builder as TransactionReportBuilder<T>;

                if (builder.ReportType == ReportType.TransactionDetail) {
                    reportUrl += $"/{trb.TransactionId}";
                }
                else if (builder.ReportType == ReportType.FindTransactions) {
                    //PAGE
                    if (trb.Page.HasValue) {
                        queryStringParams.Add("PAGE", trb.Page.ToString());
                    }
                    //PAGE_SIZE
                    if (trb.PageSize.HasValue) {
                        queryStringParams.Add("PAGE_SIZE", trb.PageSize.ToString());
                    }
                    //ORDER_BY
                    string[] orderByOptions = new string[] { "TIME_CREATED", "STATUS", "TYPE", "DEPOSIT_ID" };
                    if (!string.IsNullOrEmpty(trb.OrderProperty) && orderByOptions.Contains(trb.OrderProperty.ToUpper())) {
                        queryStringParams.Add("ORDER_BY", trb.OrderProperty.ToUpper());
                    }
                    //ORDER
                    string[] orderOptions = new string[] { "ASC", "DESC" };
                    if (!string.IsNullOrEmpty(trb.OrderDirection) && orderOptions.Contains(trb.OrderDirection.ToUpper())) {
                        queryStringParams.Add("ORDER", trb.OrderDirection.ToUpper());
                    }
                    //ACCOUNT_NAME
                    //ID
                    if (!string.IsNullOrEmpty(trb.TransactionId)) {
                        queryStringParams.Add("ID", trb.TransactionId);
                    }
                    //BRAND
                    //MASKED_NUMBER_FIRST6LAST4
                    if (!string.IsNullOrEmpty(trb.SearchBuilder.CardNumberFirstSix) && !string.IsNullOrEmpty(trb.SearchBuilder.CardNumberLastFour)) {
                        //queryStringParams.Add("MASKED_NUMBER_FIRST6LAST4", trb.SearchBuilder.CardNumberFirstSix);
                    }
                    //ARN
                    //BRAND_REFERENCE
                    //AUTHCODE
                    if (!string.IsNullOrEmpty(trb.SearchBuilder.AuthCode)) {
                        queryStringParams.Add("AUTHCODE", trb.SearchBuilder.AuthCode);
                    }
                    //REFERENCE
                    if (!string.IsNullOrEmpty(trb.SearchBuilder.ReferenceNumber)) {
                        queryStringParams.Add("REFERENCE", trb.SearchBuilder.ReferenceNumber);
                    }
                    //STATUS
                    //FROM_TIME_CREATED
                    queryStringParams.Add("FROM_TIME_CREATED", (trb.StartDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd"));
                    //TO_TIME_CREATED 
                    if (trb.EndDate.HasValue) {
                        queryStringParams.Add("TO_TIME_CREATED", trb.EndDate.Value.ToString("yyyy-MM-dd"));
                    }
                    //DEPOSIT_ID
                    if (!string.IsNullOrEmpty(trb.SearchBuilder.DepositReference)) {
                        queryStringParams.Add("DEPOSIT_ID", trb.SearchBuilder.DepositReference);
                    }
                    //FROM_DEPOSIT_TIME_CREATED
                    if (trb.SearchBuilder.StartDepositDate.HasValue) {
                        queryStringParams.Add("FROM_DEPOSIT_TIME_CREATED", trb.SearchBuilder.StartDepositDate.Value.ToString("yyyy-MM-dd"));
                    }
                    //TO_DEPOSIT_TIME_CREATED
                    if (trb.SearchBuilder.EndDepositDate.HasValue) {
                        queryStringParams.Add("TO_DEPOSIT_TIME_CREATED", trb.SearchBuilder.EndDepositDate.Value.ToString("yyyy-MM-dd"));
                    }
                    //FROM_BATCH_TIME_CREATED
                    if (trb.SearchBuilder.StartBatchDate.HasValue) {
                        queryStringParams.Add("FROM_BATCH_TIME_CREATED", trb.SearchBuilder.StartBatchDate.Value.ToString("yyyy-MM-dd"));
                    }
                    //TO_BATCH_TIME_CREATED
                    if (trb.SearchBuilder.EndBatchDate.HasValue) {
                        queryStringParams.Add("TO_BATCH_TIME_CREATED", trb.SearchBuilder.EndBatchDate.Value.ToString("yyyy-MM-dd"));
                    }
                    //SYSTEM.MID
                    if (!string.IsNullOrEmpty(trb.SearchBuilder.MerchantId)) {
                        queryStringParams.Add("SYSTEM.MID", trb.SearchBuilder.MerchantId);
                    }
                    //SYSTEM.HIERARCHY
                }
            }

            var response = DoTransaction(HttpMethod.Get, reportUrl, queryStringParams: queryStringParams);

            return MapReportResponse<T>(response, builder.ReportType);
        }

        private T MapReportResponse<T>(string rawResponse, ReportType reportType) where T : class {
            T result = Activator.CreateInstance<T>();

            JsonDoc json = JsonDoc.Parse(rawResponse);

            Func<JsonDoc, TransactionSummary> mapTransactionSummary = (doc) => {
                JsonDoc paymentMethod = doc.GetValue<JsonDoc>("payment_method");

                JsonDoc card = paymentMethod?.GetValue<JsonDoc>("card");

                var summary = new TransactionSummary {
                    //ToDo: Map all transaction properties
                    TransactionId = doc.GetValue<string>("id"),
                    TransactionDate = doc.GetValue<DateTime>("time_created"),
                    TransactionStatus = doc.GetValue<string>("status"),
                    TransactionType = doc.GetValue<string>("type"),
                    // ?? = doc.GetValue<string>("channel"),
                    Amount = doc.GetValue<decimal>("amount"),
                    Currency = doc.GetValue<string>("currency"),
                    ReferenceNumber = doc.GetValue<string>("reference"),
                    // ?? = doc.GetValue<DateTime>("time_created_reference"),
                    BatchSequenceNumber = doc.GetValue<string>("batch_id"),
                    // ?? = doc.GetValue<string>("country"),
                    // ?? = doc.GetValue<string>("action_create_id"),
                    OriginalTransactionId = doc.GetValue<string>("parent_resource_id"),

                    //?? = paymentMethod?.GetValue<string>("message"),
                    EntryMode = paymentMethod?.GetValue<string>("entry_mode"),
                    //?? = paymentMethod?.GetValue<string>("name"),

                    CardType = card?.GetValue<string>("brand"),
                    AuthCode = card?.GetValue<string>("authcode"),
                    //?? = card?.GetValue<string>("brand_reference"),
                    AquirerReferenceNumber = card?.GetValue<string>("arn"),
                    MaskedCardNumber = card?.GetValue<string>("masked_number_first6last4"),
                };

                return summary;
            };

            if (reportType == ReportType.TransactionDetail && result is TransactionSummary) {
                result = mapTransactionSummary(json) as T;
            }
            else if (reportType == ReportType.FindTransactions && result is IEnumerable<TransactionSummary>) {
                List<JsonDoc> transactions = json.GetValue<List<JsonDoc>>("transactions");
                foreach (var doc in transactions) {
                    (result as List<TransactionSummary>).Add(mapTransactionSummary(doc));
                }
            }

            return result;
        }
    }
}
