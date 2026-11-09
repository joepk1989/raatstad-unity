using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Kleine JSON-lezer en -schrijver voor modules (JsonUtility kan geen woordenboeken zoals "f": { "i,j,t": "paver" }).
// Lezen geeft Dictionary<string, object>, List<object>, double, string, bool of null.
public static class ModuleJson
{
    public static object Parse(string s) { int i = 0; var v = Val(s, ref i); return v; }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    static object Val(string s, ref int i)
    {
        Ws(s, ref i); if (i >= s.Length) return null;
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (i < s.Length)
            {
                Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++;   // ':'
                d[k] = Val(s, ref i); Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                i++; break;   // '}'
            }
            return d;
        }
        if (c == '[')
        {
            var l = new List<object>(); i++; Ws(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (i < s.Length)
            {
                l.Add(Val(s, ref i)); Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                i++; break;   // ']'
            }
            return l;
        }
        if (c == '"') return Str(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out double num); return num;
    }
    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder(); i++;   // '"'
        while (i < s.Length && s[i] != '"')
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break; case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        i++; return sb.ToString();
    }

    public static string Write(object o) { var sb = new StringBuilder(); W(sb, o); return sb.ToString(); }
    static void W(StringBuilder sb, object o)
    {
        switch (o)
        {
            case null: sb.Append("null"); break;
            case string s: Q(sb, s); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case float f: Num(sb, f); break;
            case double d: Num(sb, d); break;
            case IDictionary<string, object> d:
                { sb.Append('{'); bool first = true; foreach (var kv in d) { if (!first) sb.Append(','); first = false; Q(sb, kv.Key); sb.Append(':'); W(sb, kv.Value); } sb.Append('}'); break; }
            case System.Collections.IList l:
                { sb.Append('['); for (int k = 0; k < l.Count; k++) { if (k > 0) sb.Append(','); W(sb, l[k]); } sb.Append(']'); break; }
            default: Q(sb, o.ToString()); break;
        }
    }
    static void Num(StringBuilder sb, double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append('0'); return; }
        double r = System.Math.Round(d); if (System.Math.Abs(d - r) < 1e-9 && System.Math.Abs(r) < 1e15) { sb.Append(((long)r).ToString(CultureInfo.InvariantCulture)); return; }
        sb.Append(System.Math.Round(d, 6).ToString("R", CultureInfo.InvariantCulture));
    }
    static void Q(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\n': sb.Append("\\n"); break; case '\r': sb.Append("\\r"); break; case '\t': sb.Append("\\t"); break;
                default: if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
            }
        }
        sb.Append('"');
    }

    // hulpjes om velden te lezen
    public static float F(object o, float def = 0f) => o is double d ? (float)d : o is bool b ? (b ? 1f : 0f) : def;
    public static int I(object o, int def = 0) => o is double d ? (int)System.Math.Round(d) : def;
    public static string S(object o, string def = null) => o is string s ? s : def;
}
