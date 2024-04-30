using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParsecVDisplay
{
    internal static class Updater
    {
        public static string DOWNLOAD_URL => $"https://github.com/{App.GITHUB_REPO}/releases/latest";
        static string GH_API_URL => $"https://api.github.com/repos/{App.GITHUB_REPO}/releases/latest";

        static Updater()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;
        }

        public static Task<string> CheckUpdate()
        {
            return Task.Run(async () =>
            {
                var localVersion = new Version(App.VERSION);
                var remoteVersion = await FetchLatestVersion();

                if (remoteVersion.CompareTo(localVersion) > 0)
                {
                    return remoteVersion.ToString();
                }

                return string.Empty;
            });
        }

        static async Task<Version> FetchLatestVersion()
        {
            try
            {
                var json = await DownloadString(GH_API_URL);

                if (!string.IsNullOrEmpty(json))
                {
                    var tagNameRegex = new Regex("\"tag_name\":\\s+\"(.*)\"");
                    var match = tagNameRegex.Match(json);

                    if (match.Success && match.Groups.Count > 1)
                    {
                        var vtag = match.Groups[1].Value.ToLower();
                        if (vtag.StartsWith("v"))
                            vtag = vtag.Substring(1);

                        return new Version(vtag);
                    }
                }
            }
            catch
            {
            }

            return new Version(App.VERSION);
        }

        static async Task<string> DownloadString(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                };

                return await client.GetStringAsync(url);
            }
        }
    }
}