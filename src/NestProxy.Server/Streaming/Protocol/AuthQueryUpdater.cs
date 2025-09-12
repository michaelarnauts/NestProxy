namespace NestProxy.Server.Streaming.Protocol;

/// <summary>
/// Utility helpers for updating (or inserting) authentication query parameters on upstream RTSP URLs when
/// a new stream token is issued during extension. Existing key casing is preserved to avoid unnecessary
/// differences that could affect server-side caching heuristics.
/// </summary>
public static class AuthQueryUpdater
{
    /// <summary>
    /// Returns a new URL string with the auth query parameter replaced (if present) or appended (if absent).
    /// </summary>
    /// <param name="url">Original RTSP(S) URL.</param>
    /// <param name="newAuth">New auth token value (unescaped).</param>
    /// <returns>Updated URL string containing the new auth token.</returns>
    public static string UpdateAuth(string url, string newAuth)
    {
        UriBuilder ub = new(url);
        string q = ub.Query;
        string[] parts = (q ?? string.Empty).TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> list = [];
        bool replaced = false;
        foreach (string p in parts)
        {
            string[] kv = p.Split('=', 2);
            string key = kv[0];
            if (key.Equals("auth", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(key + "=" + Uri.EscapeDataString(newAuth));
                replaced = true;
            }
            else
            {
                list.Add(p);
            }
        }

        if (!replaced)
        {
            list.Add("auth=" + Uri.EscapeDataString(newAuth));
        }

        ub.Query = string.Join("&", list);
        return ub.Uri.ToString();
    }
}