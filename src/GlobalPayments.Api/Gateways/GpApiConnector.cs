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
                if (builder.TransactionType != TransactionType.Auth
                    && builder.TransactionType != TransactionType.Sale 
                    && builder.TransactionType != TransactionType.Refund)
                {
                    throw new ApiException("Transaction type not supported for this payment method");
                }

                var paymentMethod = new JsonDoc()
                    .Set("entry_mode", "MANUAL"); // [MOTO, ECOM, IN_APP, CHIP, SWIPE, MANUAL, CONTACTLESS_CHIP, CONTACTLESS_SWIPE]

                var cardData = builder.PaymentMethod as ICardData;

                string cvvIndicator;
                switch (cardData.CvnPresenceIndicator) {
                    case CvnPresenceIndicator.Present:
                        cvvIndicator = "PRESENT";
                        break;
                    case CvnPresenceIndicator.Illegible:
                        cvvIndicator = "ILLEGIBLE";
                        break;
                    default:
                        cvvIndicator = "NOT_PRESENT";
                        break;
                }

                var card = new JsonDoc()
                    .Set("number", cardData.Number)
                    .Set("expiry_month", cardData.ExpMonth.HasValue ? cardData.ExpMonth.ToString().PadLeft(2, '0') : string.Empty)
                    .Set("expiry_year", cardData.ExpYear.HasValue ? cardData.ExpYear.ToString().PadLeft(4, '0').Substring(2, 2) : string.Empty)
                    //.Set("track", "")
                    //.Set("tag", "")
                    //.Set("chip_condition", "") // [PREV_SUCCESS, PREV_FAILED]
                    .Set("cvv", cardData.Cvn)
                    .Set("cvv_indicator", cvvIndicator) // [ILLEGIBLE, NOT_PRESENT, PRESENT]
                    .Set("avs_address", "Flat 123")
                    .Set("avs_postal_code", "50001")
                    //.Set("funding", "") // [DEBIT, CREDIT]
                    .Set("authcode", builder.OfflineAuthCode);
                    //.Set("brand_reference", "")

                if (builder.PaymentMethod is CreditCardData) {
                    paymentMethod.Set("first_name", (builder.PaymentMethod as CreditCardData).CardHolderName);
                    //paymentMethod.Set("last_name", "");

                    var secureEcom = (builder.PaymentMethod as CreditCardData).ThreeDSecure;
                    if (secureEcom != null) {
                        var authentication = new JsonDoc()
                            .Set("xid", secureEcom.Xid)
                            .Set("cavv", secureEcom.Cavv)
                            .Set("eci", secureEcom.Eci);
                            //.Set("mac", ""); //A message authentication code submitted to confirm integrity of the request.

                        paymentMethod.Set("authentication", authentication);
                    }
                }

                // pin block
                if (builder.PaymentMethod is IPinProtected) {
                    if (!builder.TransactionType.HasFlag(TransactionType.Reversal)) {
                        card.Set("pin_block", ((IPinProtected)builder.PaymentMethod).PinBlock);
                    }
                }

                // encryption
                if (builder.PaymentMethod is IEncryptable) {
                    var encryptionData = ((IEncryptable)builder.PaymentMethod).EncryptionData;

                    if (encryptionData != null) {
                        var encryption = new JsonDoc()
                            .Set("version", encryptionData.Version);

                        if (!string.IsNullOrEmpty(encryptionData.KTB)) {
                            encryption.Set("method", "KTB");
                            encryption.Set("info", encryptionData.KTB);
                        }
                        else {
                            encryption.Set("method", "KSN");
                            encryption.Set("info", encryptionData.KSN);
                        }

                        paymentMethod.Set("encryption", encryption);
                    }
                }

                paymentMethod.Set("card", card);

                string captureMode = "AUTO";
                if (builder.MultiCapture) {
                    captureMode = "MULTIPLE";
                }
                else if (builder.TransactionType == TransactionType.Auth) {
                    captureMode = "LATER";
                }

                var data = new JsonDoc()
                    .Set("account_name", "Transaction_Processing")
                    .Set("type", builder.TransactionType == TransactionType.Sale ? "SALE" : "REFUND") // [SALE, REFUND]
                    .Set("channel", "CNP") // [CP, CNP] card present, card not present, check if add a config value
                    .Set("capture_mode", captureMode) // [AUTO, LATER, MULTIPLE]
                    //.Set("remaining_capture_count", "") //Pending Russell
                    .Set("authorization_mode", builder.AllowPartialAuth ? "PARTIAL" : "WHOLE") // [PARTIAL, WHOLE]
                    .Set("amount", builder.Amount.ToNumericCurrencyString())
                    .Set("currency", builder.Currency)
                    .Set("reference", builder.ClientTransactionId)
                    .Set("description", builder.Description)
                    .Set("order_reference", builder.OrderId)
                    //.Set("initiator", "") // [PAYER, MERCHANT] //default to PAYER
                    .Set("gratuity_amount", builder.Gratuity.ToNumericCurrencyString())
                    .Set("cashback_amount", builder.CashBackAmount.ToNumericCurrencyString())
                    .Set("surcharge_amount", builder.SurchargeAmount.ToNumericCurrencyString())
                    .Set("convenience_amount", builder.ConvenienceAmount.ToNumericCurrencyString())
                    .Set("country", builder.BillingAddress.Country)
                    //.Set("language", "") //Todo: add to the config
                    .Set("ip_address", builder.CustomerIpAddress)
                    //.Set("site_reference", "") //
                    .Set("payment_method", paymentMethod);
                
                var response = DoTransaction(HttpMethod.Post, "/ucp/transactions", data.ToString());

                return MapResponse(response);
            }

            throw new ApiException("Transaction type not implemented");
        }

        public Transaction ManageTransaction(ManagementBuilder builder) {
            string response = string.Empty;

            var data = new JsonDoc()
                .Set("amount", builder.Amount.ToNumericCurrencyString());

            if (builder.TransactionType == TransactionType.Capture) {
                data.Set("gratuity_amount", builder.Gratuity.ToNumericCurrencyString());
                response = DoTransaction(HttpMethod.Post, $"/ucp/transactions/{builder.TransactionId}/capture", data.ToString());
            }
            else if (builder.TransactionType == TransactionType.Refund) {
                response = DoTransaction(HttpMethod.Post, $"/ucp/transactions/{builder.TransactionId}/refund", data.ToString());
            }
            else if (builder.TransactionType == TransactionType.Reversal) {
                response = DoTransaction(HttpMethod.Post, $"/ucp/transactions/{builder.TransactionId}/reversal", data.ToString());
            }
            return MapResponse(response);
        }

        public string SerializeRequest(AuthorizationBuilder builder) {
            throw new NotImplementedException();
        }

        private Transaction MapResponse(string rawResponse) {
            Transaction transaction = new Transaction();

            if (!string.IsNullOrEmpty(rawResponse)) {
                JsonDoc json = JsonDoc.Parse(rawResponse);

                // ToDo: Map transaction values
                transaction.TransactionId = json.GetValue<string>("id");
                transaction.BalanceAmount = json.GetValue<decimal>("amount");
                transaction.Timestamp = json.GetValue<string>("time_created");
                transaction.ResponseMessage = json.GetValue<string>("status");
                transaction.ReferenceNumber = json.GetValue<string>("reference");
                transaction.BatchSummary = new BatchSummary
                {
                    SequenceNumber = json.GetValue<string>("batch_id")
                };
                transaction.ResponseCode = json.Get("action").GetValue<string>("result_code");
            }

            return transaction;
        }

        public T ProcessReport<T>(ReportBuilder<T> builder) where T : class {
            string reportUrl = "/ucp/transactions";
            Dictionary<string, string> queryStringParams = new Dictionary<string, string>();

            Action<Dictionary<string, string>, string, string> addQueryStringParam = (queryParams, name, value) => {
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value)) {
                    queryParams.Add(name, value);
                }
            };

            if (builder is TransactionReportBuilder<T>) {
                var trb = builder as TransactionReportBuilder<T>;

                if (builder.ReportType == ReportType.TransactionDetail) {
                    reportUrl += $"/{trb.TransactionId}";
                }
                else if (builder.ReportType == ReportType.FindTransactions) {
                    addQueryStringParam(queryStringParams, "PAGE", trb.Page?.ToString());
                    addQueryStringParam(queryStringParams, "PAGE_SIZE", trb.PageSize?.ToString());
                    addQueryStringParam(queryStringParams, "ORDER_BY", EnumConverter.GetMapping(Target.GP_API, trb.OrderProperty));
                    addQueryStringParam(queryStringParams, "ORDER", EnumConverter.GetMapping(Target.GP_API, trb.OrderDirection));
                    addQueryStringParam(queryStringParams, "ACCOUNT_NAME", trb.SearchBuilder.AccountName);
                    addQueryStringParam(queryStringParams, "ID", trb.TransactionId);
                    addQueryStringParam(queryStringParams, "BRAND", trb.SearchBuilder.CardBrand);
                    addQueryStringParam(queryStringParams, "MASKED_NUMBER_FIRST6LAST4", trb.SearchBuilder.MaskedCardNumber);
                    addQueryStringParam(queryStringParams, "ARN", trb.SearchBuilder.AquirerReferenceNumber);
                    addQueryStringParam(queryStringParams, "BRAND_REFERENCE", trb.SearchBuilder.BrandReference);
                    addQueryStringParam(queryStringParams, "AUTHCODE", trb.SearchBuilder.AuthCode);
                    addQueryStringParam(queryStringParams, "REFERENCE", trb.SearchBuilder.ReferenceNumber);
                    addQueryStringParam(queryStringParams, "STATUS", EnumConverter.GetMapping(Target.GP_API, trb.SearchBuilder.TransactionStatus));
                    addQueryStringParam(queryStringParams, "FROM_TIME_CREATED", (trb.StartDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "TO_TIME_CREATED", trb.EndDate?.ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "DEPOSIT_ID", trb.SearchBuilder.DepositReference);
                    addQueryStringParam(queryStringParams, "FROM_DEPOSIT_TIME_CREATED", trb.SearchBuilder.StartDepositDate?.ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "TO_DEPOSIT_TIME_CREATED", trb.SearchBuilder.EndDepositDate?.ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "FROM_BATCH_TIME_CREATED", trb.SearchBuilder.StartBatchDate?.ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "TO_BATCH_TIME_CREATED", trb.SearchBuilder.EndBatchDate?.ToString("yyyy-MM-dd"));
                    addQueryStringParam(queryStringParams, "SYSTEM.MID", trb.SearchBuilder.MerchantId);
                    // SYSTEM.HIERARCHY ??
                }
            }

            var response = DoTransaction(HttpMethod.Get, reportUrl, queryStringParams: queryStringParams);

            return MapReportResponse<T>(response, builder.ReportType);
        }

        private T MapReportResponse<T>(string rawResponse, ReportType reportType) where T : class {
            T result = Activator.CreateInstance<T>();

            JsonDoc json = JsonDoc.Parse(rawResponse);

            Func<JsonDoc, TransactionSummary> mapTransactionSummary = (doc) => {
                JsonDoc paymentMethod = doc.Get("payment_method");

                JsonDoc card = paymentMethod?.Get("card");

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
                    Country = doc.GetValue<string>("country"),
                    // ?? = doc.GetValue<string>("action_create_id"),
                    OriginalTransactionId = doc.GetValue<string>("parent_resource_id"),

                    GatewayResponseMessage = paymentMethod?.GetValue<string>("message"),
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
