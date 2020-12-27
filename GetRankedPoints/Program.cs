using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetRankedPoints
{

    class Program
    {

        public static string[] Ranks =
        {
            "Unranked 1",
            "Unranked 2",
            "Unranked 3",

            "Iron 1",
            "Iron 2",
            "Iron 3",

            "Bronze 1",
            "Bronze 2",
            "Bronze 3",

            "Silver 1",
            "Silver 2",
            "Silver 3",

            "Gold 1",
            "Gold 2",
            "Gold 3",

            "Platinum 1",
            "Platinum 2",
            "Platinum 3",

            "Diamond 1",
            "Diamond 2",
            "Diamond 3",

            "Immortal 1",
            "Immortal 2",
            "Immortal 3",

            "Radiant"
        };

        public static NameValueCollection CompetitiveMovement = new NameValueCollection()
        {
            {"MINOR_INCREASE", "+1"},
            {"INCREASE", "+2"},
            {"MAJOR_INCREASE", "+3"},
            {"STABLE", "0"},
            {"MINOR_DECREASE","-1"},
            {"DECREASE", "-2"},
            {"MAJOR_DECREASE", "-3"},
            {"PROMOTED", "RankUp ("},
            {"DEMOTED", "Derank ("}
        };

        public static NameValueCollection MapName = new NameValueCollection()
        {
            {"/Game/Maps/Bonsai/Bonsai", "Split"},
            {"/Game/Maps/Triad/Triad", "Heaven"},
            {"/Game/Maps/Port/Port", "IceBox"},
            {"/Game/Maps/Ascent/Ascent", "Ascent"},
            {"/Game/Maps/Duality/Duality", "Bind"}
        };
        public static string AccessToken { get; set; }
        public static string EntitlementToken { get; set; }
        public static string username { get; set; }
        public static string password { get; set; }
        public static string UserID  { get; set; }
        public static string region { get; set; }
        
        private class JConfig
        {
            public string Username;
            public string Password;
            public string Region;
        }
        
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Ranked Point Checker");
            Console.WriteLine("Checking Config File..");
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.json")))
            {
                Console.WriteLine("Config File not found!\nCreating new config file!\nPlease fill it and restart :)");
                JConfig config = new JConfig() //instantiate it
                {
                    Password = "PASSWORD HERE",
                    Username = "USERNAME HERE",
                    Region = "REGION HERE (na/eu/ap/ko/br)"
                };
                File.WriteAllText((Path.Combine(Directory.GetCurrentDirectory(),"config.json")),JsonConvert.SerializeObject(config, Formatting.Indented));
                Console.ReadKey();
                Environment.Exit(1);
            }
            else
                Console.WriteLine("Config File found, Reading File...");

            ReadConfig();
            Console.WriteLine("Finished Reading Config, Logging in..");
            
            Login();
            Console.WriteLine("Finished Logging in, checking rank progression...");
            CheckRankedUpdates();
        }

        static void ReadConfig()
        {
            StreamReader r = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
            string json = r.ReadToEnd();
            // DEBUGGING IGNORE Console.WriteLine(json);
            var localJSON = JsonConvert.DeserializeObject(json);
            JToken localObj = JToken.FromObject(localJSON);
            username = localObj["Username"].Value<string>();
            password = localObj["Password"].Value<string>();
            region = localObj["Region"].Value<string>();
            Console.WriteLine($"Found Username: {username}");
            
        }
        
        
        static void Login()
        {
            try
            {
                CookieContainer cookie = new CookieContainer();
                Authentication.GetAuthorization(cookie);

                var authJson = JsonConvert.DeserializeObject(Authentication.Authenticate(cookie, username, password));
                JToken authObj = JObject.FromObject(authJson);

                string authURL = authObj["response"]["parameters"]["uri"].Value<string>();
                var access_tokenVar = Regex.Match(authURL, @"access_token=(.+?)&scope=").Groups[1].Value;
                AccessToken = $"{access_tokenVar}";

                RestClient client = new RestClient(new Uri("https://entitlements.auth.riotgames.com/api/token/v1"));
                RestRequest request = new RestRequest(Method.POST);

                request.AddHeader("Authorization", $"Bearer {AccessToken}");
                request.AddJsonBody("{}");

                string response = client.Execute(request).Content;
                var entitlement_token = JsonConvert.DeserializeObject(response);
                JToken entitlement_tokenObj = JObject.FromObject(entitlement_token);

                EntitlementToken = entitlement_tokenObj["entitlements_token"].Value<string>();

                
                RestClient userid_client = new RestClient(new Uri("https://auth.riotgames.com/userinfo"));
                RestRequest userid_request = new RestRequest(Method.POST);

                userid_request.AddHeader("Authorization", $"Bearer {AccessToken}");
                userid_request.AddJsonBody("{}");

                string userid_response = userid_client.Execute(userid_request).Content;
                dynamic userid = JsonConvert.DeserializeObject(userid_response);
                JToken useridObj = JObject.FromObject(userid);

                //Console.WriteLine(userid_response);

                UserID = useridObj["sub"].Value<string>();

                Console.WriteLine($"Logged in successfully! ");
            }
            catch (Exception e)
            {
                Console.WriteLine("BAD LOGIN INFORMATION!!");
                Console.ReadKey();
                throw;
            }
            
        }

        static void CheckRankedUpdates()
        {
            try
            {
                RestClient ranked_client = new RestClient(new Uri($"https://pd.{region}.a.pvp.net/mmr/v1/players/{UserID}/competitiveupdates?startIndex=0&endIndex=20"));
                RestRequest ranked_request = new RestRequest(Method.GET);
            
                ranked_request.AddHeader("Authorization", $"Bearer {AccessToken}");
                ranked_request.AddHeader("X-Riot-Entitlements-JWT", EntitlementToken);
            
                IRestResponse rankedresp = ranked_client.Get(ranked_request);
                if (rankedresp.IsSuccessful)
                {
                    dynamic RankedJson = JsonConvert.DeserializeObject<JObject>(rankedresp.Content);
                    // Debugging IGNORE Console.WriteLine(RankedJson);
                    var store = RankedJson["Matches"];
                    foreach (var game in store)
                    {
                        if (game["CompetitiveMovement"] != "MOVEMENT_UNKNOWN")
                        {
                            Console.WriteLine("\nRanked Game detected.");

                            Console.WriteLine($"Last match time: {DateTimeOffset.FromUnixTimeMilliseconds((long)game["MatchStartTime"])}");
                            Console.WriteLine($"Last match map: {MapName[game["MapID"].ToString()]}");
                            string gCM = game["CompetitiveMovement"].ToString();
                            string CM = CompetitiveMovement[gCM];
                            /*
                             * -1 = 9 punti persi
                             * -2 = 20 punti persi
                             * -3 =
                             *  0 =
                             * +1 =
                             * +2 = 21/19 punti
                             * +3 = 35 punti
                             * Demoted 71 punti
                             */

                            string result = gCM == "PROMOTED" || gCM == "DEMOTED" ? CM+Ranks[game["TierAfterUpdate"]]+")" : Int16.Parse(CM) < 0 ? "Lost "+"("+CM+")" : Int16.Parse(CM) == 0 ? "Draw "+"("+CM+")" : "Win "+"("+CM+")";
                            
                            Console.WriteLine($"Last match result: {result}");
                            int before = game["TierProgressBeforeUpdate"];
                            int after = game["TierProgressAfterUpdate"];
                            Console.WriteLine($"Points Before: {before}");
                            Console.WriteLine($"Points After: {after}");

                            int num = after - before;

                            string str = before < after ? str = $"Congrats you gained: {num} points"
                                : str = $"Congrats you lost: {num * -1} points";
                            Console.WriteLine(str);
                            Console.WriteLine($"\nActual Rank: {Ranks[game["TierAfterUpdate"]]}\n");
                            Console.WriteLine($"Points to RankUp ({Ranks[game["TierAfterUpdate"]+1]}): {100-after}!");
                            string equals = new string('=', after/2);
                            string space = new string('-', (100 - after)/2);
                            Console.WriteLine($"[{equals}>{space}] {after}%\n");
                           /* Console.WriteLine($"Points to Derank ({Ranks[game["TierAfterUpdate"]-1]}): {after}!");
                            equals = new string('=', (100 - after)/2);
                            space = new string('-', after/2);
                            Console.WriteLine($"[{equals}>{space}] {100-after}%");*/
                            Console.ReadKey();
                            Environment.Exit(1);

                            //int num = before - after;

                            //Console.WriteLine($"Net gain/loss: {num} points");
                        }
                        else
                        {
                            Console.WriteLine("No ranked games detected!");
                            // Game does not register as a ranked game.
                        }
                    }
                }
            
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
    }
}