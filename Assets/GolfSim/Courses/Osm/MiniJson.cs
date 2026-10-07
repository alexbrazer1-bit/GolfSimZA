using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>
    /// Small JSON reader for OpenStreetMap (Overpass) answers: objects become
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double, plus string,
    /// bool and null. (Unity's JsonUtility cannot read free-form tag lists.)
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWhite();
            object value = p.ReadValue();
            p.SkipWhite();
            if (!p.AtEnd) throw new FormatException("Unexpected text after the JSON value at " + p.Position);
            return value;
        }

        private sealed class Parser
        {
            private readonly string s;
            private int i;
            private readonly StringBuilder sb = new StringBuilder();

            public Parser(string text) { s = text; }

            public bool AtEnd => i >= s.Length;
            public int Position => i;

            public void SkipWhite()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            public object ReadValue()
            {
                SkipWhite();
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw new FormatException("Unexpected character '" + c + "' at " + i);
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("Expected " + word + " at " + i);
                i += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; // {
                SkipWhite();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWhite();
                    if (i >= s.Length || s[i] != '"') throw new FormatException("Expected a name at " + i);
                    string key = ReadString();
                    SkipWhite();
                    if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at " + i);
                    i++;
                    d[key] = ReadValue();
                    SkipWhite();
                    if (i >= s.Length) throw new FormatException("Unexpected end of JSON object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Expected ',' or '}' at " + i);
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                i++; // [
                SkipWhite();
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    SkipWhite();
                    if (i >= s.Length) throw new FormatException("Unexpected end of JSON array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw new FormatException("Expected ',' or ']' at " + i);
                }
            }

            private string ReadString()
            {
                i++; // opening quote
                sb.Length = 0;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Bad \\u escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw new FormatException("Unterminated string");
            }

            private double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length)
                {
                    char c = s[i];
                    if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') i++;
                    else break;
                }
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        // ------------------------------------------------------------ helpers

        public static Dictionary<string, object> Obj(object o, string key)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(key, out object v)) return v as Dictionary<string, object>;
            return null;
        }

        public static List<object> Arr(object o, string key)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(key, out object v)) return v as List<object>;
            return null;
        }

        public static string Str(object o, string key)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v != null)
                return v is string str ? str : Convert.ToString(v, CultureInfo.InvariantCulture);
            return null;
        }

        public static double Num(object o, string key, double fallback = 0)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v != null)
            {
                if (v is double dv) return dv;
                if (v is string sv && double.TryParse(sv, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            }
            return fallback;
        }

        /// <summary>Writes a string as a JSON string literal (for building small JSON files by hand).</summary>
        public static string Quote(string value)
        {
            var b = new StringBuilder(value.Length + 2);
            b.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
            return b.ToString();
        }
    }
}
