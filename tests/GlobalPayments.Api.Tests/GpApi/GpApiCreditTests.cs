using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlobalPayments.Api.Tests.GpApi
{
    [TestClass]
    public class GpApiCreditTests : BaseGpApiTests {
        CreditCardData card;

        public GpApiCreditTests() {
            ServicesContainer.ConfigureService(new GpApiConfig {
                AppId = "Uyq6PzRbkorv2D4RQGlldEtunEeGNZll",
                AppKey = "QDsW1ETQKHX6Y4TA",
                Channel = Channel.CardNotPresent,
            });

            card = new CreditCardData {
                Number = "4263970000005262",
                ExpMonth = 05,
                ExpYear = 2025,
                Cvn = "852",
            };
        }

        [TestMethod]
        public void CreditAuthorization() {
            var transaction = card.Authorize(14m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(transaction);
            Assert.AreEqual(SUCCESS, transaction?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Preauthorized), transaction?.ResponseMessage);

            var capture = transaction.Capture(16m)
                .WithGratuity(2m)
                .Execute();
            Assert.IsNotNull(capture);
            Assert.AreEqual(SUCCESS, capture?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), capture?.ResponseMessage);
        }

        [TestMethod]
        public void CreditSale() {
            var address = new Address {
                StreetAddress1 = "123 Main St.",
                City = "Downtown",
                State = "NJ",
                Country = "US",
                PostalCode = "12345"
            };
            var response = card.Charge(19.99m)
                .WithCurrency("USD")
                .WithAddress(address)
                .Execute();
            Assert.IsNotNull(response);
            Assert.AreEqual(SUCCESS, response?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), response?.ResponseMessage);
        }

        [TestMethod]
        public void CreditRefund() {
            var response = card.Refund(16m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(response);
            Assert.AreEqual(SUCCESS, response?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), response?.ResponseMessage);
        }

        [TestMethod]
        public void CreditRefundTransaction() {
            var transaction = card.Charge(10.95m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(transaction);

            var response = transaction.Refund(10.95m)
                .WithCurrency("USD")
                .Execute();
            Assert.IsNotNull(response);
            Assert.AreEqual(SUCCESS, response?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), response?.ResponseMessage);
        }

        [TestMethod]
        public void CreditReverseTransaction() {
            var transaction = card.Charge(12.99m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(transaction);

            var response = transaction.Reverse(12.99m)
                .Execute();
            Assert.IsNotNull(response);
            Assert.AreEqual(SUCCESS, response?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Reversed), response?.ResponseMessage);
        }

        [TestMethod]
        public void CreditAuthorizationForMultiCapture()
        {
            var authorization = card.Authorize(14m)
                .WithCurrency("USD")
                .WithMultiCapture(true)
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(authorization);
            Assert.AreEqual(SUCCESS, authorization?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Preauthorized), authorization?.ResponseMessage);

            var capture1 = authorization.Capture(3m)
                .Execute();
            Assert.IsNotNull(capture1);
            Assert.AreEqual(SUCCESS, capture1?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), capture1?.ResponseMessage);

            var capture2 = authorization.Capture(5m)
                .Execute();
            Assert.IsNotNull(capture2);
            Assert.AreEqual(SUCCESS, capture2?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), capture2?.ResponseMessage);

            var capture3 = authorization.Capture(7m)
                .Execute();
            Assert.IsNotNull(capture3);
            Assert.AreEqual(SUCCESS, capture3?.ResponseCode);
            Assert.AreEqual(GetMapping(TransactionStatus.Captured), capture3?.ResponseMessage);
        }
    }
}
