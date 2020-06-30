using GlobalPayments.Api.Entities;
using GlobalPayments.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace GlobalPayments.Api.Tests.GpApi
{
    [TestClass]
    
    public class GpApiReportingTests
    {
        public GpApiReportingTests()
        {
            ServicesContainer.ConfigureService(new GpApiConfig
            {
                AppId = "Uyq6PzRbkorv2D4RQGlldEtunEeGNZll",
                AppKey = "QDsW1ETQKHX6Y4TA",
            });
        }

        [TestMethod]
        public void ReportTransactionDetail()
        {
            string transactionId = "TRN_TvY1QFXxQKtaFSjNaLnDVdo3PZ7ivz";
            TransactionSummary response = ReportingService.TransactionDetail(transactionId)
                .Execute();
            Assert.IsNotNull(response);
            Assert.IsTrue(response is TransactionSummary);
            Assert.AreEqual(transactionId, response.TransactionId);
        }

        [TestMethod]
        public void ReportFindTransactionWithCriteria()
        {
            List<TransactionSummary> summary = ReportingService.FindTransactions()
                .OrderBy("time_created")
                .WithPaging(1, 10)
                .Where(SearchCriteria.StartDate, DateTime.UtcNow.AddDays(-30))
                //.And(SearchCriteria.EndDate, DateTime.UtcNow)
                .Execute();
            Assert.IsNotNull(summary);
            Assert.IsNotNull(summary is List<TransactionSummary>);
        }
    }
}
