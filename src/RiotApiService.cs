using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace GamesOfAll
{
    internal static class RiotApiService
    {
        private static readonly string[] Regions =
        {
            "na1", "br1", "la1", "la2", "euw1", "eun1", "kr", "jp1",
            "oc1", "tr1", "ru", "ph2", "sg2", "th2", "tw2", "vn2"
        };

        public static string ProtectKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string UnprotectKey(string protectedKey)
        {
            if (string.IsNullOrWhiteSpace(protectedKey)) return null;
            try
            {
                byte[] encrypted = Convert.FromBase64String(protectedKey);
                byte[] clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch (Exception ex)
            {
                Storage.Log("No se pudo descifrar la clave de Riot guardada: " + ex.Message);
                return null;
            }
        }

        public static string NormalizeRegion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string region = value.Trim().ToUpperInvariant();
            switch (region)
            {
                case "NA": case "NA1": case "US": return "na1";
                case "BR": case "BR1": return "br1";
                case "LAN": case "LA1": return "la1";
                case "LAS": case "LA2": return "la2";
                case "EUW": case "EUW1": return "euw1";
                case "EUNE": case "EUN": case "EUN1": return "eun1";
                case "KR": return "kr";
                case "JP": case "JP1": return "jp1";
                case "OCE": case "OC": case "OC1": return "oc1";
                case "TR": case "TR1": return "tr1";
                case "RU": return "ru";
                case "PH": case "PH2": return "ph2";
                case "SG": case "SG2": return "sg2";
                case "TH": case "TH2": return "th2";
                case "TW": case "TW2": return "tw2";
                case "VN": case "VN2": return "vn2";
                default: return null;
            }
        }

        public static string[] SupportedRegions
        {
            get { return (string[])Regions.Clone(); }
        }

        public static GameAccountStats LookupOnline(GameAccountStats account, string apiKey, string fallbackRegion)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.AccountId) ||
                string.IsNullOrWhiteSpace(apiKey)) return null;

            string region = NormalizeRegion(account.Region);
            if (region == null) region = NormalizeRegion(fallbackRegion);
            if (region == null)
            {
                Storage.Log("No se pudo consultar Riot en internet: configura la región de la cuenta.");
                return null;
            }

            try
            {
                string host = "https://" + region + ".api.riotgames.com";
                string summonerPath = "/lol/summoner/v4/summoners/by-puuid/" +
                    Uri.EscapeDataString(account.AccountId);
                Dictionary<string, object> summoner = RequestJson(host + summonerPath, apiKey);
                string summonerId = GetString(summoner, "id");
                if (string.IsNullOrWhiteSpace(summonerId))
                    throw new InvalidDataException("La API no devolvió el identificador de invocador.");

                GameAccountStats result = Copy(account);
                result.Region = region;
                int level = GetInt(summoner, "summonerLevel");
                if (level > 0) result.Level = level;

                string leaguePath = "/lol/league/v4/entries/by-summoner/" +
                    Uri.EscapeDataString(summonerId);
                List<Dictionary<string, object>> entries = RequestList(host + leaguePath, apiKey);
                Dictionary<string, object> solo = null;
                foreach (Dictionary<string, object> entry in entries)
                {
                    if (string.Equals(GetString(entry, "queueType"), "RANKED_SOLO_5x5",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        solo = entry;
                        break;
                    }
                }
                result.Rank = FormatRank(solo);
                result.UpdatedAt = Fmt.ToUnix(DateTime.UtcNow);
                result.OnlineUpdatedAt = result.UpdatedAt;
                return result;
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                string status = response == null ? "sin respuesta" :
                    ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                Storage.Log("No se pudo actualizar una cuenta desde Riot API (HTTP " + status + ").");
                return null;
            }
            catch (Exception ex)
            {
                Storage.Log("No se pudo actualizar una cuenta desde Riot API: " + ex.Message);
                return null;
            }
        }

        private static string FormatRank(Dictionary<string, object> entry)
        {
            if (entry == null) return "Sin rango";
            string tier = GetString(entry, "tier");
            string division = GetString(entry, "rank");
            if (string.IsNullOrWhiteSpace(tier)) return "Sin rango";
            string result = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(division)) result += " " + division;
            int points = GetInt(entry, "leaguePoints");
            return result + " · " + points.ToString(CultureInfo.InvariantCulture) + " LP";
        }

        private static Dictionary<string, object> RequestJson(string url, string apiKey)
        {
            using (WebResponse response = CreateRequest(url, apiKey).GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            }
        }

        private static List<Dictionary<string, object>> RequestList(string url, string apiKey)
        {
            using (WebResponse response = CreateRequest(url, apiKey).GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                return serializer.Deserialize<List<Dictionary<string, object>>>(reader.ReadToEnd());
            }
        }

        private static HttpWebRequest CreateRequest(string url, string apiKey)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.Headers["X-Riot-Token"] = apiKey;
            return request;
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            int result;
            string value = GetString(map, key);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result : 0;
        }

        private static GameAccountStats Copy(GameAccountStats source)
        {
            GameAccountStats copy = new GameAccountStats();
            copy.AccountId = source.AccountId;
            copy.AccountName = source.AccountName;
            copy.PlaySeconds = source.PlaySeconds;
            copy.BaselinePlaySeconds = source.BaselinePlaySeconds;
            copy.LocalPlaySeconds = source.LocalPlaySeconds;
            copy.Rank = source.Rank;
            copy.Level = source.Level;
            copy.Region = source.Region;
            copy.UpdatedAt = source.UpdatedAt;
            copy.OnlineUpdatedAt = source.OnlineUpdatedAt;
            return copy;
        }
    }
}
