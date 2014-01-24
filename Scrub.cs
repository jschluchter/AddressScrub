using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using Dapper;
using Newtonsoft.Json;

namespace AddressScrub
{

    /// <summary>
    /// |simple object|
    /// a quick strongly typed object so we can use linq in the foreach
    /// </summary>
    class BasicAddress
    {
        public string Address { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Id { get; set; }
    }


    class Scrub
    {
        /// <summary>
        /// set the connection
        /// this uses your windows creds
        /// </summary>

        const string ConnectionString = "YOUR_CONNECTION_STRING_HERE;";
        const string LobKey = "YOUR_LOB_KEY_HERE";
        const string DefaultState = "FL";
        /// <summary>
        /// set your sql statement
        /// </summary>
        const string SelectSql =
            "SELECT D_IGD_ID AS ID, D_PropAddress1 AS Address, D_PropCity AS City, D_PropZip AS PostalCode, D_PropCountyName AS County " +
            "FROM tblIGDD " +
            "WHERE D_PropZip = '' " +
            "AND D_PropCity<>'UNICORPORATED' " +
            "AND D_PropCity<>'' " +
            "ORDER by D_PropCountyName";

        /// <summary>
        /// |meat and potatoes|
        /// use dapper to retrieve your results to a list 
        /// based on the above object
        /// run logic to filter and send it on to scrub
        /// </summary>
        static void Main(string[] args)
        {
            
            List<BasicAddress> results;
            using (var connection = new SqlConnection(ConnectionString))
            {
                results = connection.Query<BasicAddress>(SelectSql).ToList();
            }

            foreach (var result in results.Where(result => !string.IsNullOrEmpty(result.Address) && !string.IsNullOrEmpty(result.City) && result.City != "UNINCORPORATED"))
            {
                PostalScrub(result);
            }
        }

        /// <summary>
        /// lob.com address scrubbing
        /// pass in a partial address
        /// get back some json
        /// no error = send it to be updated
        /// </summary>
        /// <param name="input"></param>
        static void PostalScrub(BasicAddress input)
        {
            const string lobAddressUrl = "https://api.lob.com/v1/verify";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", LobKey, ""))));
                HttpContent data = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"address_line1", input.Address},
                        {"address_city", input.City},
                        {"address_zip", input.PostalCode},
                        {"address_state", DefaultState},
                    });
                var resp = client.PostAsync(lobAddressUrl, data).Result;
                var result = resp.Content.ReadAsStringAsync().Result;
                dynamic obj = JsonConvert.DeserializeObject(result);
                if (obj.errors != null) return;
                //uncomment the line below to update your sql db
                //UpdateSql(obj, input.Id);
            }
        }

        /// <summary>
        /// edit the obj variables as needed
        /// edit the update statement as necessary
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="id"></param>
        static void UpdateSql(dynamic obj, string id)
        {
            string lobZip = obj.address.address_zip.ToString().Substring(0, 5);
            string lobAddr = obj.address.address_line1.ToString();
            string lobCity = obj.address.address_city.ToString();

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Execute("UPDARE dbo.tblIgdd " +
                                   "SET D_PropAddress1 = @addr" +
                                   ", D_PropCity = @city" +
                                   ", D_PropZip = @zip " +
                                   "WHERE D_IGD_ID = @id",
                                   new { addr = lobAddr, city = lobCity, zip = lobZip, id });
            }
        }
    }
}
