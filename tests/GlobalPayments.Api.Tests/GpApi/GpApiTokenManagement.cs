using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlobalPayments.Api.Tests.GpApi
{
    [TestClass]
    public class GpApiTokenManagement : BaseGpApiTests {
        private string _token;

        public GpApiTokenManagement() {
            ServicesContainer.ConfigureService(new GpApiConfig {
                AppId = "Uyq6PzRbkorv2D4RQGlldEtunEeGNZll",
                AppKey = "QDsW1ETQKHX6Y4TA",
            });

            try {
                CreditCardData card = new CreditCardData {
                    Number = "4111111111111111",
                    ExpMonth = 12,
                    ExpYear = 2015,
                    Cvn = "123"
                };
                _token = card.Tokenize();
                Assert.IsTrue(!string.IsNullOrEmpty(_token), "Token could not be generated.");
            }
            catch (ApiException ex) {
                Assert.Fail(ex.Message);
            }
        }

        [TestMethod]
        public void VerifyToken() {
            var token = new CreditCardData {
                Token = _token,
            };

            var response = token.Verify().Execute();

            Assert.IsNotNull(response);
            Assert.AreEqual("00", response.ResponseCode);
            Assert.AreEqual("ACTIVE", response.ResponseMessage);
        }

        [TestMethod]
        public void UpdateToken() {
            var token = new CreditCardData {
                Token = _token,
                ExpMonth = 12,
                ExpYear = 2025
            };
            Assert.IsTrue(token.UpdateTokenExpiry());

            var response = token.Verify().Execute();

            Assert.IsNotNull(response);
            Assert.AreEqual("00", response.ResponseCode);
            Assert.AreEqual("ACTIVE", response.ResponseMessage);
        }

        [TestMethod]
        public void DeleteToken() {
            var token = new CreditCardData {
                Token = _token
            };
            Assert.IsTrue(token.DeleteToken());

            try {
                token.Verify().Execute();
            }
            catch (GatewayException ex) {
                Assert.AreEqual("RESOURCE_NOT_FOUND", ex.ResponseCode);
            }
        }
    }
}
