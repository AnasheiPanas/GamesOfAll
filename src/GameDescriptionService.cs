using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GamesOfAll
{
    internal sealed class GameDescriptionResult
    {
        public string Text;
        public string Source;
        public string SourceUrl;
    }

    internal static class GameDescriptionService
    {
        public static GameDescriptionResult Find(Game game)
        {
            if (game.Platform == Platform.Steam && IsAppId(game.LaunchTarget))
            {
                try
                {
                    GameDescriptionResult steam = FindSteam(game.LaunchTarget);
                    if (steam != null) return steam;
                }
                catch (WebException ex)
                {
                    Storage.Log("Steam Store no disponible para " + game.Title + ": " + ex.Message);
                }
            }
            return FindWikipedia(game.Title);
        }

        private static GameDescriptionResult FindSteam(string appId)
        {
            string json = Download("https://store.steampowered.com/api/appdetails?appids=" +
                Uri.EscapeDataString(appId) + "&l=spanish");
            IDictionary<string, object> root = Deserialize(json);
            IDictionary<string, object> app;
            if (!TryGetObject(root, appId, out app)) return null;

            object success;
            if (!app.TryGetValue("success", out success) || !Convert.ToBoolean(success)) return null;

            IDictionary<string, object> data;
            if (!TryGetObject(app, "data", out data)) return null;
            string description = GetString(data, "short_description");
            if (string.IsNullOrWhiteSpace(description))
                description = GetString(data, "about_the_game");
            description = CleanHtml(description);
            if (string.IsNullOrWhiteSpace(description)) return null;

            GameDescriptionResult result = new GameDescriptionResult();
            result.Text = description;
            result.Source = "Steam Store";
            result.SourceUrl = "https://store.steampowered.com/app/" + Uri.EscapeDataString(appId) + "/";
            return result;
        }

        private static GameDescriptionResult FindWikipedia(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            string queryUrl = "https://es.wikipedia.org/w/api.php?action=query&list=search&srsearch=" +
                Uri.EscapeDataString(title) + "&format=json&utf8=1&srlimit=1";
            IDictionary<string, object> queryRoot = Deserialize(Download(queryUrl));
            IDictionary<string, object> query;
            object searchValue;
            if (!TryGetObject(queryRoot, "query", out query) ||
                !query.TryGetValue("search", out searchValue)) return null;

            object[] searchResults = searchValue as object[];
            if (searchResults == null || searchResults.Length == 0) return null;
            IDictionary<string, object> first = searchResults[0] as IDictionary<string, object>;
            string pageTitle = first == null ? null : GetString(first, "title");
            if (string.IsNullOrWhiteSpace(pageTitle)) return null;

            string pageUrl = "https://es.wikipedia.org/w/api.php?action=query&prop=extracts%7Cinfo&inprop=url" +
                "&exintro=1&explaintext=1&redirects=1&format=json&utf8=1&titles=" + Uri.EscapeDataString(pageTitle);
            IDictionary<string, object> pageRoot = Deserialize(Download(pageUrl));
            IDictionary<string, object> pageQuery;
            IDictionary<string, object> pages;
            if (!TryGetObject(pageRoot, "query", out pageQuery) ||
                !TryGetObject(pageQuery, "pages", out pages)) return null;

            foreach (object value in pages.Values)
            {
                IDictionary<string, object> page = value as IDictionary<string, object>;
                string extract = page == null ? null : GetString(page, "extract");
                if (string.IsNullOrWhiteSpace(extract)) continue;

                GameDescriptionResult result = new GameDescriptionResult();
                result.Text = extract.Trim();
                result.Source = "Wikipedia en español: " + pageTitle;
                result.SourceUrl = GetString(page, "fullurl");
                if (string.IsNullOrEmpty(result.SourceUrl))
                    result.SourceUrl = "https://es.wikipedia.org/wiki/" +
                        Uri.EscapeDataString(pageTitle.Replace(' ', '_'));
                return result;
            }
            return null;
        }

        private static string Download(string url)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "Games/1.0 (desktop game library)";
            request.Accept = "application/json";
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static IDictionary<string, object> Deserialize(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 4 * 1024 * 1024;
            return serializer.Deserialize<Dictionary<string, object>>(json);
        }

        private static bool TryGetObject(IDictionary<string, object> source, string key,
            out IDictionary<string, object> value)
        {
            object item;
            if (source != null && source.TryGetValue(key, out item))
            {
                value = item as IDictionary<string, object>;
                return value != null;
            }
            value = null;
            return false;
        }

        private static string GetString(IDictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? Convert.ToString(value) : null;
        }

        private static string CleanHtml(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string text = Regex.Replace(value, "<[^>]*>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static bool IsAppId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char character in value)
                if (character < '0' || character > '9') return false;
            return true;
        }
    }
}
