using GlobalPayments.Api.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace GlobalPayments.Api.Entities.GpApi
{
    internal class GpApiTokenResponse
    {
        internal string Token { get; private set; }
        internal string Type { get; private set; }
        internal string AppId { get; private set; }
        internal string AppName { get; private set; }
        internal DateTime TimeCreated { get; private set; }
        internal int SecondsToExpire { get; private set; }
        internal string Email { get; private set; }

        //internal int StatusCode { get; private set; }
        //internal string ResponseMessage { get; private set; }

        public GpApiTokenResponse(string jsonString) {
            var doc = JsonDoc.Parse(jsonString);
            
            MapResponseValues(doc);

            //if (StatusCode != 200) {
            //    throw new ApiException(ResponseMessage);
            //}
        }

        internal virtual void MapResponseValues(JsonDoc doc) {
            Token = doc.GetValue<string>("token");
            Type = doc.GetValue<string>("type");
            AppId = doc.GetValue<string>("app_id");
            AppName = doc.GetValue<string>("app_name");
            TimeCreated = doc.GetValue<DateTime>("time_created");
            SecondsToExpire = doc.GetValue<int>("seconds_to_expire");
            Email = doc.GetValue<string>("email");
            //StatusCode = doc.GetValue<int>("StatusCode");
            //ResponseMessage = doc.GetValue<string>("ResponseMessage");
        }
    }
}
