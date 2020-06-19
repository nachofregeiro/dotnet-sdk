using System;
using System.Collections.Generic;
using System.Text;

namespace GlobalPayments.Api.Entities.GpApi
{
    internal class GpApiRequest
    {
        public string Endpoint { get; set; }
        public string RequestBody { get; set; }
        public string ResultsField { get; set; }
    }
}
