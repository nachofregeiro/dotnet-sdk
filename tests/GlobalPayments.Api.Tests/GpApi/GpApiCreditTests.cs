﻿using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlobalPayments.Api.Tests.GpApi
{
    [TestClass]
    public class GpApiCreditTests {
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

            var capture = transaction.Capture(16m)
                .WithGratuity(2m)
                .Execute();
            Assert.IsNotNull(capture);
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
        }

        [TestMethod]
        public void CreditRefund() {
            var response = card.Refund(16m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(response);
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
        }
    }
}
