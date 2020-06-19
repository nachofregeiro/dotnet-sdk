using GlobalPayments.Api.Builders;
using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Entities.GpApi;
using GlobalPayments.Api.PaymentMethods;
using GlobalPayments.Api.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace GlobalPayments.Api.Gateways {
    internal class GpApiConnector : RestGateway, IPaymentGateway {
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
            var response = DoTransaction(HttpMethod.Post, request.Endpoint, request.RequestBody);

            return Activator.CreateInstance(typeof(GpApiTokenResponse), new object[] { response }) as GpApiTokenResponse;
        }

        public object SendRequest<T>(GpApiRequest request) where T : GpApiEntity {
            if (string.IsNullOrEmpty(SessionToken))
                throw new ApiException("GP Api connector is not signed in, please check your configuration.");

            var response = DoTransaction(HttpMethod.Get, request.Endpoint);

            return Activator.CreateInstance(typeof(GpApiResponse<T>), new object[] { response, request.ResultsField }) as GpApiResponse<T>;
        }

        public Transaction ProcessAuthorization(AuthorizationBuilder builder)
        {
            if (string.IsNullOrEmpty(SessionToken))
            {
                SignIn();
            }

            if (builder.TransactionType == TransactionType.Auth) {

            } else if (builder.TransactionType == TransactionType.Capture) {

            } else if (builder.TransactionType == TransactionType.Sale) {
                var paymentMethod = new JsonDoc()
                    //.Set("first_name", "")
                    //.Set("last_name", "")
                    .Set("entry_mode", "ECOM") // [MOTO, ECOM, IN_APP, CHIP, SWIPE, MANUAL, CONTACTLESS_CHIP, CONTACTLESS_SWIPE]
                    .Set("id", "PMT_31087d9c-e68c-4389-9f13-39378e166ea5");

                if (builder.PaymentMethod is ICardData) {
                    var cardData = builder.PaymentMethod as ICardData;

                    var expiryMonth = cardData.ExpMonth.HasValue ? cardData.ExpMonth.ToString().PadLeft(2, '0') : string.Empty;
                    var expiryYear = cardData.ExpYear.HasValue ? cardData.ExpYear.ToString().PadLeft(4, '0').Substring(2, 2) : string.Empty;

                    var card = new JsonDoc()
                        .Set("number", cardData.Number)
                        .Set("expiry_month", expiryMonth)
                        .Set("expiry_year", expiryYear)
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
                }

                var data = new JsonDoc()
                    .Set("account_name", "Transaction_Processing")
                    .Set("type", "SALE") // [SALE, REFUND]
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
    }
}
