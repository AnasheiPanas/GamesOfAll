using System;
using System.Collections.Generic;
using System.Text;

namespace GamesOfAll
{
    // Parser minimo del formato VDF de Valve (los .vdf/.acf son pares clave-valor
    // anidados con llaves). Devuelve Dictionary<string, object> donde object es
    // string (valor simple) o Dictionary<string, object> (bloque anidado).
    internal static class Vdf
    {
        public static Dictionary<string, object> Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int pos = 0;
            return ParseMap(text, ref pos);
        }

        private static Dictionary<string, object> ParseMap(string text, ref int pos)
        {
            Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                SkipWhitespace(text, ref pos);
                if (pos >= text.Length) break;

                if (text[pos] == '}')
                {
                    pos++;
                    break;
                }

                string key = ReadToken(text, ref pos);
                if (key == null) break;

                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == '{')
                {
                    pos++;
                    map[key] = ParseMap(text, ref pos);
                }
                else
                {
                    string val = ReadToken(text, ref pos);
                    map[key] = val;
                }
            }
            return map;
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length)
            {
                char c = text[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\0' || c == '\uFEFF') pos++;
                else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
                {
                    while (pos < text.Length && text[pos] != '\n') pos++;
                }
                else break;
            }
        }

        private static string ReadToken(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            if (pos >= text.Length) return null;

            if (text[pos] == '"')
            {
                pos++;
                StringBuilder sb = new StringBuilder();
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c == '\\' && pos + 1 < text.Length)
                    {
                        char n = text[pos + 1];
                        if (n == '"' || n == '\\') { sb.Append(n); pos += 2; continue; }
                        if (n == 'n') { sb.Append('\n'); pos += 2; continue; }
                        sb.Append(c); pos++;
                        continue;
                    }
                    if (c == '"') { pos++; break; }
                    sb.Append(c);
                    pos++;
                }
                return sb.ToString();
            }
            else if (text[pos] == '{' || text[pos] == '}')
            {
                return null;
            }
            else
            {
                int start = pos;
                while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '{' && text[pos] != '}') pos++;
                return text.Substring(start, pos - start);
            }
        }

        public static string GetString(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object v;
            if (!map.TryGetValue(key, out v)) return null;
            string s = v as string;
            return s;
        }

        public static Dictionary<string, object> GetMap(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object v;
            if (!map.TryGetValue(key, out v)) return null;
            return v as Dictionary<string, object>;
        }

        public static int GetInt(Dictionary<string, object> map, string key, int def)
        {
            string s = GetString(map, key);
            int n;
            if (s != null && int.TryParse(s.Trim(), out n)) return n;
            return def;
        }

        public static long GetLong(Dictionary<string, object> map, string key, long def)
        {
            string s = GetString(map, key);
            long n;
            if (s != null && long.TryParse(s.Trim(), out n)) return n;
            return def;
        }
    }
}
