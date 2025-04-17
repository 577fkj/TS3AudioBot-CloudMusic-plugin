using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot;
using TSLib.Full.Book;
using System.IO;
using System.Threading;
using YunPlugin.utils;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TS3AudioBot.CommandSystem;
using System.Net.Http;
using System.Linq;

namespace YunPlugin.api.netease
{
    public class NeteaseConfig : MusicApiConfig
    {
        public bool RefreshCookie { get; set; }
        public int CookieUpdateIntervalMin { get; set; }
        public Dictionary<string, string> Header { get; set; }

        public NeteaseConfig()
        {
            ApiServerUrl = "http://localhost:3000";
            RefreshCookie = false;
            CookieUpdateIntervalMin = 30;
            Header = new Dictionary<string, string>
            {
                { "Cookie", "" },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0" }
            };
        }
    }

    public class NeteaseMusicInfo : MusicInfo
    {
        private HttpClientWrapper httpClient;

        public override string ArtistUrl => "https://music.163.com/#/artist?id={0}";

        public NeteaseMusicInfo(HttpClientWrapper httpClient, string id, bool inPlayList = true) : base(id, inPlayList)
        {
            this.httpClient = httpClient;
        }

        public async override Task InitMusicInfo()
        {
            if (!string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Image))
            {
                return;
            }
            try
            {
                MusicDetail musicDetail = await httpClient.Get<MusicDetail>(
                    "/song/detail",
                    new Dictionary<string, string> { { "ids", Id } }
                );
                Image = musicDetail.songs[0].al.picUrl;
                Name = musicDetail.songs[0].name;
                DetailUrl = $"https://music.163.com/#/song?id={Id}";

                Author.Clear();

                var artists = musicDetail.songs[0].ar;
                if (artists != null)
                {
                    foreach (var artist in artists)
                    {
                        if (!string.IsNullOrEmpty(artist.name))
                        {
                            Author.Add(artist.name, artist.id.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "NeteaseMusic InitMusicInfo error");
                Name = "歌名获取失败!\n" + e.Message;
            }
        }

        public async override Task<string> GetMusicUrl()
        {
            MusicURL musicURL = await httpClient.Get<MusicURL>(
                    "/song/url",
                    new Dictionary<string, string> { { "id", Id } }
                );
            if (musicURL.data[0].freeTrialInfo != null)
            {
                return "error:VIP歌曲";
            }
            return musicURL.data[0].url;
        }
    }

    public class NeteaseMusic : MusicApi<NeteaseConfig>
    {
        public override string Name => "云音乐";
        public override MusicApiType Key => MusicApiType.Netease;
        public override string[] DefaultAlias => new[] { "n", "wy", "wyy" };
        public override string[] KeyInUrl => new[] { "163.com" };

        private Dictionary<string, string> Header
        {
            get => Config.Header; set
            {
                Config.Header = value;
                httpClient.SetHeader(value);
                Config.Save();
            }
        }
        private string Cookie
        {
            get => Header["Cookie"]; set
            {
                Header["Cookie"] = value;
                httpClient.SetHeader(Header);
                Config.Save();
            }
        }

        private HttpClientWrapper httpClient;

        private Timer cookieUpdateTimer;

        private async Task<Exception> HttpCallback(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string message;
                try
                {
                    var result = await response.AsJson<NeteaseError>();
                    message = $"[{response.StatusCode}] {result.code}: {result.message}";
                }
                catch (Exception e)
                {
                    message = $"[{response.StatusCode}] {e.Message}";
                }

                LogError(message);
                return new CommandException(message, CommandExceptionReason.CommandError);
            }
            return null;
        }

        public NeteaseMusic(PlayManager playManager, Ts3Client ts3Client, Connection serverView, NeteaseConfig config) : base(playManager, ts3Client, serverView, config)
        {
            httpClient = new HttpClientWrapper(config.ApiServerUrl, Header, true);
            httpClient.SetHttpCallback(HttpCallback);

            cookieUpdateTimer = new Timer(async (state) =>
            {
                try
                {
                    if (!config.RefreshCookie || string.IsNullOrEmpty(Cookie))
                    {
                        return;
                    }
                    Status1 status = await httpClient.Get<Status1>("/login/refresh");
                    if (status.code == 200)
                    {
                        var newCookie = Utils.MergeCookie(Cookie, Utils.ProcessCookie(status.cookie));
                        Cookie = newCookie;
                        LogInfo("Cookie update success");
                    }
                    else
                    {
                        LogWarning($"Cookie update failed: {JsonConvert.SerializeObject(status)}");
                    }
                }
                catch (Exception e)
                {
                    LogError(e, "Cookie update error");
                }
            }, null, TimeSpan.Zero.Milliseconds, TimeSpan.FromMinutes(config.CookieUpdateIntervalMin).Milliseconds);
        }

        public override Task<MusicInfo> GetMusicInfo(string id)
        {
            return Task.FromResult<MusicInfo>(new NeteaseMusicInfo(httpClient, id, false));
        }

        public override async Task<PlayListMeta> GetAlbums(string id, int limit)
        {
            Albums albums = await httpClient.Get<Albums>(
                    "/album",
                    new Dictionary<string, string> { { "id", id } }
                );
            string name = albums.album.name;
            string picUrl = albums.album.picUrl;

            long numOfSongs;
            if (limit == 0)
            {
                numOfSongs = albums.songs.Length;
            }
            else
            {
                numOfSongs = Math.Min(albums.songs.Length, limit);
            }

            if (numOfSongs > 100)
            {
                await ts3Client.SendChannelMessage($"警告：专辑过大，可能需要一定的时间生成 [{numOfSongs}]");
            }

            List<MusicInfo> list = new List<MusicInfo>();
            for (int i = 0; i < numOfSongs; i++)
            {
                long musicid = albums.songs[i].id;
                if (musicid > 0)
                {
                    list.Add(new NeteaseMusicInfo(httpClient, musicid.ToString()));
                }
            }
            return new PlayListMeta(id, name, $"https://music.163.com/#/album?id={id}", picUrl, list);
        }

        public override async Task<PlayListMeta> GetPlayList(string id, int limit)
        {
            var playListInfo = await httpClient.Get<PlayListInfo>(
                    "/playlist/detail",
                    new Dictionary<string, string> { { "id", id } }
                );
            string name = playListInfo.playlist.name;
            string imgUrl = playListInfo.playlist.coverImgUrl;

            await ts3Client.ChangeDescription(name);
            await MainCommands.CommandBotAvatarSet(ts3Client, imgUrl);
            await ts3Client.SendChannelMessage($"开始添加歌单 [{name}]");

            List<MusicInfo> musicInfos = new List<MusicInfo>();
            if (playListInfo.playlist.trackCount == 0)
            {
                PlayListTrackInfo playListTrackInfo = await httpClient.Get<PlayListTrackInfo>(
                        "/playlist/track/all",
                        new Dictionary<string, string> { { "id", id } }
                    );

                long numOfSongs;
                if (limit == 0)
                {
                    numOfSongs = playListTrackInfo.songs.Length;
                }
                else
                {
                    numOfSongs = Math.Min(playListTrackInfo.songs.Length, limit);
                }
                if (numOfSongs > 100)
                {
                    await ts3Client.SendChannelMessage($"警告：歌单过大，可能需要一定的时间生成 [{numOfSongs}]");
                }
                for (int i = 0; i < numOfSongs; i++)
                {
                    long musicid = playListTrackInfo.songs[i].id;
                    if (musicid > 0)
                    {
                        musicInfos.Add(new NeteaseMusicInfo(httpClient, musicid.ToString()));
                    }

                    await ts3Client.SendChannelMessage($"已添加歌曲 [{i + playListTrackInfo.songs.Length}-{numOfSongs}]");
                }
            }
            else
            {
                int trackCount = playListInfo.playlist.trackCount;
                if (limit != 0)
                {
                    trackCount = Math.Min(playListInfo.playlist.trackCount, limit);
                    limit = Math.Min(50, trackCount);
                }
                if (trackCount > 100)
                {
                    await ts3Client.SendChannelMessage($"警告：歌单过大，可能需要一定的时间生成 [{trackCount}]");
                }
                for (int i = 0; i < trackCount; i += limit)
                {
                    PlayListTrackInfo playListTrackInfo = await httpClient.Get<PlayListTrackInfo>(
                            "/playlist/track/all",
                            new Dictionary<string, string> { { "id", id }, { "limit", limit.ToString() }, { "offset", i.ToString() } }
                       );
                    for (int j = 0; j < playListTrackInfo.songs.Length; j++)
                    {
                        musicInfos.Add(new NeteaseMusicInfo(httpClient, playListTrackInfo.songs[j].id.ToString()));
                    }

                    await ts3Client.SendChannelMessage($"已添加歌曲 [{i + playListTrackInfo.songs.Length}-{trackCount}]");
                }
            }

            return new PlayListMeta(id, name, $"https://music.163.com/#/playlist?id={id}", imgUrl, musicInfos);
        }

        public async Task<string> GetLoginKey()
        {
            LoginKey loginKey = await httpClient.Get<LoginKey>("/login/qr/key");
            return loginKey.data.unikey;
        }

        public async Task<string> GetLoginQRImage(string key)
        {
            LoginImg loginImg = await httpClient.Get<LoginImg>("/login/qr/create", new Dictionary<string, string> { { "key", key }, { "qrimg", "true" } });
            return loginImg.data.qrimg;
        }

        public async Task<Status1> CheckLoginStatus(string key)
        {
            return await httpClient.Get<Status1>("/login/qr/check", new Dictionary<string, string> { { "key", key } });
        }

        public override async Task<string> Login(string[] args)
        {
            if (args.Length == 0)
            {
                return "参数错误: [qr|sms|cookie] {手机号|Cookie} {验证码}";
            }
            if (args[0] == "qr")
            {
                string key = await GetLoginKey();
                string qrimg = await GetLoginQRImage(key);

                await ts3Client.SendChannelMessage("正在生成二维码");
                await ts3Client.SendChannelMessage(qrimg);
                LogDebug(qrimg);
                string[] img = qrimg.Split(",");
                byte[] bytes = Convert.FromBase64String(img[1]);
                Stream stream = new MemoryStream(bytes);
                await ts3Client.UploadAvatar(stream);
                await ts3Client.ChangeDescription("请用网易云APP扫描二维码登录");

                int i = 0;
                long code;
                string result;
                string cookies;
                while (true)
                {
                    Status1 status = await CheckLoginStatus(key);
                    code = status.code;
                    cookies = status.cookie;
                    i++;
                    Thread.Sleep(1000);
                    if (i == 120)
                    {
                        result = "二维码登录失败或者超时";
                        //await ts3Client.SendChannelMessage("二维码登录失败或者超时");
                        break;
                    }
                    if (code == 803)
                    {
                        result = "二维码登录成功";
                        //await ts3Client.SendChannelMessage("二维码登录成功");
                        Config.RefreshCookie = true;
                        break;
                    }
                }
                await ts3Client.DeleteAvatar();
                await ts3Client.ChangeDescription("网易云已登录");
                Cookie = Utils.ProcessCookie(cookies);

                return result;
            }
            else if (args[0] == "sms")
            {
                if (args.Length == 2)
                {
                    string phone = args[1];
                    Status1 status = await httpClient.Get<Status1>("/captcha/sent", new Dictionary<string, string> { { "phone", phone } });
                    if (status.code == 200)
                    {
                        return "验证码已发送";
                    }
                    else
                    {
                        return "发送失败";
                    }
                }
                else if (args.Length == 3)
                {
                    string phone = args[1];
                    string captcha = args[2];
                    Status1 status = await httpClient.Get<Status1>("/captcha/verify", new Dictionary<string, string> { { "phone", phone }, { "captcha", captcha } });
                    if (status.code != 200)
                    {
                        return "验证码错误";
                    }
                    status = await httpClient.Get<Status1>("/login/cellphone", new Dictionary<string, string> { { "phone", phone }, { "captcha", captcha } });
                    if (status.code == 200)
                    {
                        Cookie = Utils.ProcessCookie(status.cookie);
                        return "登录成功";
                    }
                    else
                    {
                        return "登录失败";
                    }
                }
                else
                {
                    return "参数错误: sms [手机号] {验证码}";
                }
            }
            else if (args[0] == "cookie")
            {
                var Cookie = string.Join(" ", args.Skip(1));
                if (!string.IsNullOrEmpty(Cookie))
                {
                    this.Cookie = Cookie;
                    return "设置成功";
                }
                else
                {
                    return "参数错误: cookie {Cookie}";
                }
            }
            return "参数错误: [qr|sms|cookie] {手机号|Cookie} {验证码}";
        }

        public override async Task<List<MusicInfo>> SearchMusic(string keyword, int limit = 10, int offset = 0)
        {
            SearchResult searchResult = await httpClient.Get<SearchResult>(
                    "/search",
                    new Dictionary<string, string> { { "keywords", keyword }, { "limit", limit.ToString() }, { "offset", offset.ToString() } }
                );
            List<MusicInfo> list = new List<MusicInfo>();
            foreach (var song in searchResult.result.songs)
            {
                list.Add(new NeteaseMusicInfo(httpClient, song.id.ToString(), false));
            }
            return list;
        }

        public override async Task<List<PlayListMeta>> SearchPlaylist(string keyword, int limit = 10, int offset = 0)
        {
            SearchPlayList searchPlayList = await httpClient.Get<SearchPlayList>(
                    "/search",
                    new Dictionary<string, string> { { "keywords", keyword }, { "type", "1000" }, { "limit", limit.ToString() }, { "offset", offset.ToString() } }
                );
            List<PlayListMeta> list = new List<PlayListMeta>();
            foreach (var playList in searchPlayList.result.playlists)
            {
                list.Add(new PlayListMeta(playList.id.ToString(), playList.name, $"https://music.163.com/#/playlist?id={playList.id}", playList.coverImgUrl, null));
            }
            return list;
        }

        public override async Task<List<PlayListMeta>> SearchAlbum(string keyword, int limit = 10, int offset = 0)
        {
            SearchAlbums searchAlbums = await httpClient.Get<SearchAlbums>(
                    "/search",
                    new Dictionary<string, string> { { "keywords", keyword }, { "type", "10" }, { "limit", limit.ToString() }, { "offset", offset.ToString() } }
                );
            List<PlayListMeta> list = new List<PlayListMeta>();
            foreach (var album in searchAlbums.result.albums)
            {
                list.Add(new PlayListMeta(album.id.ToString(), album.name, $"https://music.163.com/#/album?id={album.id}", album.picUrl, null));
            }
            return list;
        }

        public Dictionary<string, string> GetHeader()
        {
            return Header;
        }

        public override void Dispose()
        {
            if (cookieUpdateTimer != null)
            {
                cookieUpdateTimer.Dispose();
            }
            cookieUpdateTimer = null;
            Config = null;
            httpClient.Dispose();
            httpClient = null;
        }

        public override void Refresh(NeteaseConfig config)
        {
            Config = config;
            httpClient.SetHeader(Header);
            httpClient.SetBaseUrl(config.ApiServerUrl);
        }

        public static string ExtractIdFromAddress(string address)
        {
            string pattern = @"id=(\d+)";
            Match match = Regex.Match(address, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            else
            {
                return address;
            }
        }

        public override MusicApiInputData GetInputData(string data)
        {
            var result = new MusicApiInputData();
            if (data.Contains("playlist"))
            {
                result.Type = MusicUrlType.PlayList;
                result.Id = ExtractIdFromAddress(data);
                result.Url = data;
            }
            else if (data.Contains("song"))
            {
                result.Type = MusicUrlType.Music;
                result.Id = ExtractIdFromAddress(data);
                result.Url = data;
            }
            else if (data.Contains("album"))
            {
                result.Type = MusicUrlType.Album;
                result.Id = ExtractIdFromAddress(data);
                result.Url = data;
            }
            else if (Utils.IsNumber(data))
            {
                result.Type = MusicUrlType.Number;
                result.Id = data;
                result.Url = "";
            }
            return result;
        }

        public override async Task<UserInfo> GetUserInfo()
        {
            if (string.IsNullOrEmpty(Cookie))
            {
                return null;
            }

            RespStatus status = await httpClient.Get<RespStatus>("/login/status");
            if (status == null || status.data == null || status.data.account == null)
            {
                return null;
            }

            if (status.data.code != 200 && status.data.account.status != 0)
            {
                return null;
            }

            VIPResult vipResult = await httpClient.Get<VIPResult>("/vip/info");

            string extra = "无VIP";
            if (vipResult != null && vipResult.code == 200 && vipResult.data != null)
            {
                var currentTime = Utils.GetTimeStampMs();
                if (vipResult.data.redplus.expireTime > currentTime)
                {
                    extra = $"SVIP {vipResult.data.redVipLevel}级 到期时间: {Utils.ConvertTimeStamp(vipResult.data.redplus.expireTime)}";
                }
                else if (vipResult.data.associator.expireTime > currentTime)
                {
                    extra = $"VIP {vipResult.data.associator.vipLevel}级 到期时间: {Utils.ConvertTimeStamp(vipResult.data.associator.expireTime)}";
                }
                else if (vipResult.data.musicPackage.expireTime > currentTime)
                {
                    extra = $"音乐包 {vipResult.data.musicPackage.vipLevel}级 到期时间: {Utils.ConvertTimeStamp(vipResult.data.musicPackage.expireTime)}";
                }
            }

            if (vipResult.data.redVipAnnualCount == 1)
            {
                extra = $"年费{extra}";
            }else{
                extra = $"非年费{extra}";
            }

            return new UserInfo
            {
                Id = status.data.profile.userId.ToString(),
                Name = status.data.profile.nickname,
                Url = $"https://music.163.com/#/user/home?id={status.data.profile.userId}",
                Extra = extra
            };
        }
    }
}
