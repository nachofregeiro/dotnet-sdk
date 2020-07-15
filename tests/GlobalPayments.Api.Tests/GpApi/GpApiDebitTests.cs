using GlobalPayments.Api.Entities;
using GlobalPayments.Api.PaymentMethods;
using GlobalPayments.Api.Tests.TestData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlobalPayments.Api.Tests.GpApi
{
    [TestClass]
    public class GpApiDebitTests
    {
        public GpApiDebitTests() {
            ServicesContainer.ConfigureService(new GpApiConfig {
                AppId = "Uyq6PzRbkorv2D4RQGlldEtunEeGNZll",
                AppKey = "QDsW1ETQKHX6Y4TA",
                Channel = Channel.CardPresent,
            });
        }

        [TestMethod]
        public void DebitSaleSwipe()
        {
            var track = new DebitTrackData {
                Value = "%B4012002000060016^VI TEST CREDIT^251210118039000000000396?;4012002000060016=25121011803939600000?",
                PinBlock = "32539F50C245A6A93D123412324000AA",
                EntryMethod = EntryMethod.Swipe,
            };
            var response = track.Charge(17.01m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public void DebitSaleSwipeEncrypted()
        {
            var track = new DebitTrackData {
                Value = "&lt;E1050711%B4012001000000016^VI TEST CREDIT^251200000000000000000000?|LO04K0WFOmdkDz0um+GwUkILL8ZZOP6Zc4rCpZ9+kg2T3JBT4AEOilWTI|+++++++Dbbn04ekG|11;4012001000000016=25120000000000000000?|1u2F/aEhbdoPixyAPGyIDv3gBfF|+++++++Dbbn04ekG|00|||/wECAQECAoFGAgEH2wYcShV78RZwb3NAc2VjdXJlZXhjaGFuZ2UubmV0PX50qfj4dt0lu9oFBESQQNkpoxEVpCW3ZKmoIV3T93zphPS3XKP4+DiVlM8VIOOmAuRrpzxNi0TN/DWXWSjUC8m/PI2dACGdl/hVJ/imfqIs68wYDnp8j0ZfgvM26MlnDbTVRrSx68Nzj2QAgpBCHcaBb/FZm9T7pfMr2Mlh2YcAt6gGG1i2bJgiEJn8IiSDX5M2ybzqRT86PCbKle/XCTwFFe1X|&gt;",
                PinBlock = "32539F50C245A6A93D123412324000AA",
                EntryMethod = EntryMethod.Swipe,
                EncryptionData = new EncryptionData {
                    Version = "01"
                }
            };
            var response = track.Charge(17.01m)
                .WithCurrency("USD")
                .WithAllowDuplicates(true)
                .Execute();
            Assert.IsNotNull(response);
        }
    }
}
