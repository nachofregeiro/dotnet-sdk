using GlobalPayments.Api.Builders;
using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Entities.GpApi;
using GlobalPayments.Api.PaymentMethods;
using GlobalPayments.Api.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;

namespace GlobalPayments.Api.Gateways {
    internal class GpApiConnector : RestGateway, IPaymentGateway, IReportingService {
        public string AppId { get; set; }
        public string AppKey { get; set; }
        public string Nonce { get; set; }
        public int? SecondsToExpire { get; set; }
        public IntervalToExpire? IntervalToExpire { get; set; }
        public Channel Channel { get; set; }
        public Language Language { get; set; }
        public string SessionToken { get; internal set; }

        public bool SupportsHostedPayments => throw new NotImplementedException();

        internal GpApiConnector() {
            // Set required api version header
            Headers["X-GP-Version"] = "2020-04-10"; //"2020-01-20";
            Headers["Accept"] = "application/json";
            Headers["Accept-Encoding"] = "gzip";
        }

        public void SignIn() {
            var request = SessionInfo.SignIn(AppId, AppKey, Nonce, SecondsToExpire, IntervalToExpire);

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

        public override string DoTransaction(HttpMethod verb, string endpoint, string data = null, Dictionary<string, string> queryStringParams = null) {
            if (string.IsNullOrEmpty(SessionToken)) {
                SignIn();
            }
            return base.DoTransaction(verb, endpoint, data, queryStringParams);
        }

        protected override string HandleResponse(GatewayResponse response) {
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NoContent) {
                var parsed = JsonDoc.Parse(response.RawResponse);
                if (parsed.Has("error_code")) {
                    string errorCode = parsed.GetValue<string>("error_code");
                    string detailedErrorCode = parsed.GetValue<string>("detailed_error_code");
                    string detailedErrorDescription = parsed.GetValue<string>("detailed_error_description");
                    
                    throw new GatewayException($"Status Code: {response.StatusCode} - {detailedErrorDescription}", errorCode, detailedErrorCode);
                }
                throw new GatewayException($"Status Code: {response.StatusCode}", responseMessage: response.RawResponse);
            }
            return response.RawResponse;
        }

        private string GetEntryMode(AuthorizationBuilder builder) {
            if (builder.PaymentMethod is ICardData card) {
                if (card.ReaderPresent) {
                    return card.CardPresent ? "MANUAL" : "IN_APP";
                }
                else {
                    return card.CardPresent ? "MANUAL" : "ECOM";
                }
            }
            else if (builder.PaymentMethod is ITrackData track) {
                if (builder.TagData != null) {
                    return track.EntryMethod.Equals(EntryMethod.Swipe) ? "CHIP" : "CONTACTLESS_CHIP";
                }
                else if (builder.HasEmvFallbackData) {
                    return "CONTACTLESS_SWIPE";
                }
                return "SWIPE";
            }
            return "ECOM";
        }

        private string GetCaptureMode(AuthorizationBuilder builder) {
            if (builder.MultiCapture) {
                return "MULTIPLE";
            }
            else if (builder.TransactionType == TransactionType.Auth) {
                return "LATER";
            }
            return "AUTO";
        }

        public Transaction ProcessAuthorization(AuthorizationBuilder builder) {

            var paymentMethod = new JsonDoc()
                .Set("entry_mode", GetEntryMode(builder)); // [MOTO, ECOM, IN_APP, CHIP, SWIPE, MANUAL, CONTACTLESS_CHIP, CONTACTLESS_SWIPE]

            if (builder.PaymentMethod is ICardData) {
                var cardData = builder.PaymentMethod as ICardData;

                var card = new JsonDoc()
                    .Set("number", cardData.Number)
                    .Set("expiry_month", cardData.ExpMonth.HasValue ? cardData.ExpMonth.ToString().PadLeft(2, '0') : string.Empty)
                    .Set("expiry_year", cardData.ExpYear.HasValue ? cardData.ExpYear.ToString().PadLeft(4, '0').Substring(2, 2) : string.Empty)
                    //.Set("track", "")
                    .Set("tag", builder.TagData)
                    .Set("chip_condition", EnumConverter.GetMapping(Target.GP_API, builder.EmvLastChipRead)) // [PREV_SUCCESS, PREV_FAILED]
                    .Set("cvv", cardData.Cvn)
                    .Set("cvv_indicator", EnumConverter.GetMapping(Target.GP_API, cardData.CvnPresenceIndicator)) // [ILLEGIBLE, NOT_PRESENT, PRESENT]
                    .Set("avs_address", "Flat 123")
                    .Set("avs_postal_code", "50001")
                    .Set("funding", builder.PaymentMethod?.PaymentMethodType == PaymentMethodType.Debit ? "DEBIT" : "CREDIT") // [DEBIT, CREDIT]
                    .Set("authcode", builder.OfflineAuthCode);
                    //.Set("brand_reference", "")

                paymentMethod.Set("card", card);
            }
            else if (builder.PaymentMethod is ITrackData) {
                var track = builder.PaymentMethod as ITrackData;

                var card = new JsonDoc()
                    .Set("number", track.Pan)
                    .Set("expiry_month", track.Expiry?.Substring(2, 2))
                    .Set("expiry_year", track.Expiry?.Substring(0, 2))
                    .Set("track", track.Value)
                    .Set("tag", builder.TagData)
                    .Set("chip_condition", EnumConverter.GetMapping(Target.GP_API, builder.EmvLastChipRead)) // [PREV_SUCCESS, PREV_FAILED]
                    //.Set("cvv", cardData.Cvn)
                    //.Set("cvv_indicator", "") // [ILLEGIBLE, NOT_PRESENT, PRESENT]
                    .Set("avs_address", "Flat 123")
                    .Set("avs_postal_code", "50001")
                    .Set("funding", builder.PaymentMethod?.PaymentMethodType == PaymentMethodType.Debit ? "DEBIT" : "CREDIT") // [DEBIT, CREDIT]
                    .Set("authcode", builder.OfflineAuthCode);
                    //.Set("brand_reference", "")

                paymentMethod.Set("card", card);
            }

            // pin block
            if (builder.PaymentMethod is IPinProtected) {
                paymentMethod.Get("card")?.Set("pin_block", ((IPinProtected)builder.PaymentMethod).PinBlock);
            }

            // authentication
            if (builder.PaymentMethod is CreditCardData) {
                paymentMethod.Set("name", (builder.PaymentMethod as CreditCardData).CardHolderName);

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
                    else if (!string.IsNullOrEmpty(encryptionData.KSN)) {
                        encryption.Set("method", "KSN");
                        encryption.Set("info", encryptionData.KSN);
                    }

                    if (encryption.Has("info")) {
                        paymentMethod.Set("encryption", encryption);
                    }
                }
            }

            var data = new JsonDoc()
                .Set("account_name", "Transaction_Processing")
                .Set("type", builder.TransactionType == TransactionType.Refund ? "REFUND" : "SALE") // [SALE, REFUND]
                .Set("channel", EnumConverter.GetMapping(Target.GP_API, Channel)) // [CP, CNP]
                .Set("capture_mode", GetCaptureMode(builder)) // [AUTO, LATER, MULTIPLE]
                //.Set("remaining_capture_count", "") //Pending Russell
                .Set("authorization_mode", builder.AllowPartialAuth ? "PARTIAL" : "WHOLE") // [PARTIAL, WHOLE]
                .Set("amount", builder.Amount.ToNumericCurrencyString())
                .Set("currency", builder.Currency)
                .Set("reference", builder.ClientTransactionId ?? Guid.NewGuid().ToString())
                .Set("description", builder.Description)
                .Set("order_reference", builder.OrderId)
                //.Set("initiator", "") // [PAYER, MERCHANT] //default to PAYER
                .Set("gratuity_amount", builder.Gratuity.ToNumericCurrencyString())
                .Set("cashback_amount", builder.CashBackAmount.ToNumericCurrencyString())
                .Set("surcharge_amount", builder.SurchargeAmount.ToNumericCurrencyString())
                .Set("convenience_amount", builder.ConvenienceAmount.ToNumericCurrencyString())
                .Set("country", builder.BillingAddress?.Country ?? "US")
                //.Set("language", EnumConverter.GetMapping(Target.GP_API, Language))
                .Set("ip_address", builder.CustomerIpAddress)
                //.Set("site_reference", "") //
                .Set("payment_method", paymentMethod);

            var response = DoTransaction(HttpMethod.Post, "/ucp/transactions", data.ToString());

            return MapResponse(response);
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
                    addQueryStringParam(queryStringParams, "SYSTEM.HIERARCHY", trb.SearchBuilder.SystemHierarchy);
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
                    Channel = doc.GetValue<string>("channel"),
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
