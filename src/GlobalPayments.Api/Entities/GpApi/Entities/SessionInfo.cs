using GlobalPayments.Api.Utils;
using System;
using System.Text;
using System.Security.Cryptography;

namespace GlobalPayments.Api.Entities.GpApi {
    internal class SessionInfo {

        /// <summary>
        /// A unique string created using the nonce and app-key.
        /// This value is used to further authenticate the request.
        /// Created as follows - SHA512(NONCE + APP-KEY).
        /// </summary>
        /// <param name="input">string to hash</param>
        /// <returns>hash of the input string</returns>
        private static string GenerateSecret(string nonce, string appKey) {
            byte[] data = Encoding.UTF8.GetBytes(nonce + appKey);

            using (SHA512 shaM = SHA512.Create()) {
                byte[] hash = shaM.ComputeHash(data);

                StringBuilder sb = new StringBuilder(64);
                foreach (var b in hash) {
                    sb.Append(b.ToString("X2"));
                }

                return sb.ToString().ToLower();
            }
        }

        internal static GpApiRequest SignIn(string appId, string appKey, string nonce) {
            var request = new JsonDoc()
                .Set("app_id", appId)
                .Set("nonce", nonce)
                .Set("grant_type", "client_credentials")
                .Set("secret", GenerateSecret(nonce, appKey));

            return new GpApiRequest {
                Endpoint = "/ucp/accesstoken",
                RequestBody = request.ToString()
            };
        }

        internal static GpApiRequest SignOut() {
            throw new Exception("SignOut not implemented");

            //return new PayrollRequest
            //{
            //    Endpoint = "/api/pos/session/signout"
            //};
        }
    }
}
