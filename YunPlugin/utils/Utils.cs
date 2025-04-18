using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using YunPlugin;

public static class Utils
{
    // Http请求
    public static async Task<T> HttpGetAsync<T>(string url, Dictionary<string, string> header = null)
    {
        var request = WebWrapper.Request(url);
        
        if (header != null)
        {
            foreach (var item in header)
            {
                request.WithHeader(item.Key, item.Value);
            }
        }

        // YunPlugin.YunPlugin.GetLogger().Info($"HttpGetAsync: {url} header: {request.Headers}");

        return await request.AsJson<T>();
    }

    // "洗牌"
    public static void ShuffleArrayList<T>(List<T> arrayList)
    {
        Random random = new Random();

        int n = arrayList.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            var value = arrayList[k];
            arrayList[k] = arrayList[n];
            arrayList[n] = value;
        }
    }

    // 获得时间戳
    public static string GetTimeStamp()
    {
        TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds).ToString();
    }

    public static long GetTimeStampMs()
    {
        TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalMilliseconds);
    }

    public static string ConvertTimeStamp(long timestamp)
    {
        DateTime dtStart = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
        return dtStart.AddMilliseconds(timestamp).ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static bool IsNumber(string strNumber)
    {
        Regex regex = new Regex(@"^\d+$");
        return regex.IsMatch(strNumber);
    }

    static readonly string[] CookieBlackList = { "expires", "path", "domain", "max-age", "secure", "httponly", "samesite", "" };

    public static Dictionary<string, string> CookieToDict(string cookie)
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(cookie))
        {
            return dict;
        }
        string[] cookies = cookie.Split(';');
        foreach (var item in cookies)
        {
            var items = item.Trim().Split('=');
            var key = items[0].Trim();
            var value = items.Length == 2 ? items[1].Trim() : "";
            if (CookieBlackList.Contains(key.ToLower()))
            {
                continue;
            }

            dict[key] = value;
        }
        return dict;
    }

    public static string ProcessCookie(string cookie)
    {
        if (string.IsNullOrEmpty(cookie))
        {
            return "";
        }
        var cookies = CookieToDict(cookie);
        return string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}"));
    }

    public static string MergeCookie(string cookie, string new_cookie)
    {
        if (string.IsNullOrEmpty(cookie))
        {
            return new_cookie;
        }
        if (string.IsNullOrEmpty(new_cookie))
        {
            return cookie;
        }
        Dictionary<string, string> dict = CookieToDict(cookie);
        Dictionary<string, string> new_dict = CookieToDict(new_cookie);
        foreach (var item in new_dict)
        {
            dict[item.Key] = item.Value;
        }
        return string.Join("; ", dict.Select(x => $"{x.Key}={x.Value}"));
    }

    public static string[] ProcessArgs(string args)
    {
        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string RemoveBBCode(string str)
    {
        return Regex.Replace(str, @"\[.*?\]", "");
    }
}