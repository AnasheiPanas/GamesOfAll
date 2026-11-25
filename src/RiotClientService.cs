using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Web.Script.Serialization;

namespace GamesOfAll
{
    internal static class RiotClientService
    {
        public static GameAccountStats ReadCurrentAccount(string leagueDirectory)
        {
            string lockfile = Path.Combine(leagueDirectory, "lockfile");
            if (!File.Exists(lockfile)) return null;

            string[] parts = File.ReadAllText(lockfile).Trim().Split(':');
            int port;
            if (parts.Length < 5 || !int.TryParse(parts[2], out port) || port < 1 || port > 65535)
                return null;

            string password = parts[3];
            Dictionary<string, object> summoner = RequestJson(port, password,
                "/lol-summoner/v1/current-summoner");
            if (summoner == null) return null;

            string accountId = GetString(summoner, "puuid");
            if (string.IsNullOrWhiteSpace(accountId))
                accountId = GetString(summoner, "summonerId");

            string accountName = GetString(summoner, "gameName");
            string tagLine = GetString(summoner, "tagLine");
            if (!string.IsNullOrWhiteSpace(accountName) && !string.IsNullOrWhiteSpace(tagLine))
                accountName += "#" + tagLine;
            if (string.IsNullOrWhiteSpace(accountName))
                accountName = GetString(summoner, "displayName");
            if (string.IsNullOrWhiteSpace(accountId) && string.IsNullOrWhiteSpace(accountName))
                return null;

            GameAccountStats result = new GameAccountStats();
            result.AccountId = string.IsNullOrWhiteSpace(accountId) ? accountName : accountId;
            result.AccountName = accountName;
            result.Level = GetInt(summoner, "summonerLevel");
            result.Region = ReadPlatformRegion(port, password);
            result.UpdatedAt = Fmt.ToUnix(DateTime.UtcNow);
            try
            {
                Dictionary<string, object> ranked = RequestJson(port, password,
                    "/lol-ranked/v1/current-ranked-stats");
                result.Rank = ReadSoloRank(ranked);
            }
            catch (Exception ex)
            {
                Storage.Log("No se pudo actualizar el rango de League of Legends: " + ex.Message);
            }
            return result;
        }

        private static string ReadSoloRank(Dictionary<string, object> ranked)
        {
            Dictionary<string, object> queues = GetMap(ranked, "queues");
            Dictionary<string, object> solo = GetMap(queues, "RANKED_SOLO_5x5");
            if (solo == null) return "Sin rango";

            string tier = GetString(solo, "tier");
            if (string.IsNullOrWhiteSpace(tier) || string.Equals(tier, "NONE", StringComparison.OrdinalIgnoreCase))
                return "Sin rango";

            string division = GetString(solo, "division");
            string rank = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(division)) rank += " " + division;

            object points;
            if (solo.TryGetValue("leaguePoints", out points) && points != null)
            {
                int lp;
                if (int.TryParse(points.ToString(), out lp)) rank += " · " + lp + " LP";
            }
            return rank;
        }

        private static string ReadPlatformRegion(int port, string password)
        {
            try
            {
                Dictionary<string, object> region = RequestJson(port, password, "/riotclient/region-locale");
                return RiotApiService.NormalizeRegion(GetString(region, "region"));
            }
            catch (Exception ex)
            {
                Storage.Log("No se pudo detectar la región de Riot: " + ex.Message);
                return null;
            }
        }

        private static Dictionary<string, object> RequestJson(int port, string password, string path)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                "https://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + path);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Proxy = null;
            request.Timeout = 1200;
            request.ReadWriteTimeout = 1200;
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("riot:" + password));
            request.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
            request.ServerCertificateValidationCallback = delegate(object sender, X509Certificate certificate,
                X509Chain chain, SslPolicyErrors errors)
            {
                HttpWebRequest localRequest = sender as HttpWebRequest;
                return localRequest != null && localRequest.RequestUri.IsLoopback &&
                    localRequest.RequestUri.Port == port;
            };

            using (WebResponse response = request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            }
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static Dictionary<string, object> GetMap(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            if (map == null) return 0;
            object value;
            int result;
            return map.TryGetValue(key, out value) && value != null &&
                int.TryParse(value.ToString(), out result) ? result : 0;
        }
    }
}
