using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TSLib.Full.Book;
using YunPlugin.utils;

namespace YunPlugin.api.qq
{
    public class QQMusicConfig : MusicApiConfig
    {
        public bool RefreshCookie { get; set; }
        public int CookieUpdateIntervalMin { get; set; }
        public Dictionary<string, string> Header { get; set; }
        public string Uin { get; set; }

        public QQMusicConfig()
        {
            ApiServerUrl = "http://localhost:3001";
            RefreshCookie = false;
            CookieUpdateIntervalMin = 30;
            Header = new Dictionary<string, string>
            {
                { "Cookie", "" },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0" }
            };
            Uin = "123456";
        }
    }

    public class QQMusicInfo : MusicInfo
    {
        private HttpClientWrapper httpClient;

        public override string ArtistUrl => "https://y.qq.com/n/ryqq/singer/{0}";

        public QQMusicInfo(HttpClientWrapper httpClient, string id, bool inPlayList = true) : base(id, inPlayList)
        {
            this.httpClient = httpClient;
        }

        public override async Task<string> GetMusicUrl()
        {
            var musicURL = await httpClient.Get<Result<string>>(
                    "/song/url",
                    new Dictionary<string, string> { { "id", Id } }
                );
            if (musicURL == null || musicURL.result != 100 || musicURL.data == null)
            {
                Log.Error($"获取音乐链接失败 [{musicURL.result}] {musicURL.errMsg}");
                throw new Exception($"获取音乐链接失败 [{musicURL.result}] {musicURL.errMsg}");
            }
            return musicURL.data;
        }

        public override async Task InitMusicInfo()
        {
            if (!string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Image))
            {
                return;
            }
            try
            {
                var musicInfo = await httpClient.Get<Result<SongInfo>>(
                        "/song",
                        new Dictionary<string, string> { { "songmid", Id } }
                    );
                if (musicInfo == null || musicInfo.result != 100 || musicInfo.data == null)
                {
                    Name = $"歌名获取失败! [{musicInfo.result}] {musicInfo.errMsg}";
                    return;
                }
                Name = musicInfo.data.track_info.name;
                Image = $"https://y.gtimg.cn/music/photo_new/T002R300x300M000{musicInfo.data.track_info.album.mid}.jpg";
                DetailUrl = $"https://y.qq.com/n/ryqq/songDetail/{Id}";

                Author.Clear();

                var singers = musicInfo.data.track_info.singer;
                if (singers != null)
                {
                    foreach (var singer in singers)
                    {
                        if (!string.IsNullOrEmpty(singer.name))
                        {
                            Author.Add(singer.name, singer.mid);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "QQMusic InitMusicInfo error");
                Name = "歌名获取失败!\n" + e.Message;
            }
        }
    }

    public class QQMusic : MusicApi<QQMusicConfig>
    {
        public override string Name => "QQ音乐";
        public override MusicApiType Key => MusicApiType.QQMusic;
        public override string[] DefaultAlias => new[] { "q", "qq" };
        public override string[] KeyInUrl => new[] { "qq.com" };

        private readonly HttpClientWrapper httpClient;

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

        private Timer cookieUpdateTimer;

        private Task<Exception> HttpCallback(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Task.FromResult<Exception>(new CommandException($"请求失败 [{response.StatusCode}] {response.ReasonPhrase}", CommandExceptionReason.CommandError));
            }
            return Task.FromResult<Exception>(null);
        }

        public QQMusic(PlayManager playManager, Ts3Client ts3Client, Connection serverView, QQMusicConfig config) : base(playManager, ts3Client, serverView, config)
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
                    var response = await httpClient.GetHttpResponse("/user/refresh");
                    var heades = response.Headers;

                    IEnumerable<string> cookies = heades.GetValues("Set-Cookie");
                    string cookie = string.Join("; ", cookies);

                    var result = await response.AsJson<Result<Dictionary<string, string>>>();
                    if (result.result == 100)
                    {
                        var newCookie = Utils.MergeCookie(Cookie, Utils.ProcessCookie(cookie));
                        Cookie = newCookie;
                        LogInfo("Cookie update success");
                    }
                    else
                    {
                        LogWarning($"Cookie update failed: {JsonConvert.SerializeObject(result)}");
                    }
                }
                catch (Exception e)
                {
                    LogError(e, "Cookie update error");
                }
            }, null, TimeSpan.Zero.Milliseconds, TimeSpan.FromMinutes(config.CookieUpdateIntervalMin).Milliseconds);
        }

        public override void Dispose()
        {
            httpClient.Dispose();
            cookieUpdateTimer.Dispose();
        }

        public override async Task<PlayListMeta> GetAlbums(string id, int limit)
        {
            var info = await httpClient.Get<Result<AlbumInfo>>("/album", new Dictionary<string, string> { { "albummid", id } });
            if (info == null || info.result != 100 || info.data == null)
            {
                throw new Exception($"获取专辑信息失败 [{info.result}] {info.errMsg}");
            }
            var name = info.data.name;
            var image = info.data.headpiclist[0].picurl;

            var musics = await httpClient.Get<Result<AlbumData>>("/album/songs", new Dictionary<string, string> { { "albummid", id } });
            if (musics == null || musics.result != 100 || musics.data == null)
            {
                throw new Exception($"获取专辑音乐失败 [{musics.result}] {musics.errMsg}");
            }

            long numOfSongs;
            if (limit == 0)
            {
                numOfSongs = musics.data.total;
            }
            else
            {
                numOfSongs = Math.Min(musics.data.total, limit);
            }

            if (numOfSongs > 100)
            {
                await ts3Client.SendChannelMessage($"警告：专辑过大，可能需要一定的时间生成 [{numOfSongs}]");
            }

            var musicList = new List<MusicInfo>();
            foreach (var song in musics.data.list)
            {
                var musicInfo = new QQMusicInfo(httpClient, song.mid);
                musicList.Add(musicInfo);
            }
            return new PlayListMeta(id, name, $"https://y.qq.com/n/ryqq/albumDetail/{id}", image, musicList);
        }

        private string GetUrlId(string url)
        {
            var sp = url.Split("/");
            return sp[sp.Length - 1];
        }

        public override MusicApiInputData GetInputData(string url)
        {
            var result = new MusicApiInputData();
            if (url.Contains("albumDetail"))
            {
                result.Type = MusicUrlType.Album;
                result.Url = url;
                result.Id = GetUrlId(url);
            }
            else if (url.Contains("songDetail"))
            {
                result.Type = MusicUrlType.Music;
                result.Url = url;
                result.Id = GetUrlId(url);
            }
            else if (url.Contains("playlist"))
            {
                result.Type = MusicUrlType.PlayList;
                result.Url = url;
                result.Id = GetUrlId(url);
            }
            else if (url.StartsWith("00"))
            {
                result.Type = MusicUrlType.Music;
                result.Url = url;
                result.Id = url;
            }
            else if (Utils.IsNumber(url))
            {
                result.Type = MusicUrlType.Number;
                result.Url = url;
                result.Id = url;
            }
            return result;
        }

        public override Task<MusicInfo> GetMusicInfo(string id)
        {
            return Task.FromResult<MusicInfo>(new QQMusicInfo(httpClient, id, false));
        }

        public override async Task<PlayListMeta> GetPlayList(string id, int limit)
        {
            var info = await httpClient.Get<Result<PlaylistInfo>>("/songlist", new Dictionary<string, string> { { "id", id } });
            if (info == null || info.result != 100 || info.data == null)
            {
                throw new Exception($"获取歌单信息失败 [{info.result}] {info.errMsg}");
            }
            var name = info.data.dissname;
            var image = info.data.logo;

            long numOfSongs;
            if (limit == 0)
            {
                numOfSongs = info.data.songnum;
            }
            else
            {
                numOfSongs = Math.Min(info.data.songnum, limit);
            }

            if (numOfSongs > 100)
            {
                await ts3Client.SendChannelMessage($"警告：歌单过大，可能需要一定的时间生成 [{numOfSongs}]");
            }

            var musicList = new List<MusicInfo>();
            foreach (var song in info.data.songlist)
            {
                var musicInfo = new QQMusicInfo(httpClient, song.songmid);
                musicList.Add(musicInfo);
            }
            return new PlayListMeta(id, name, $"https://y.qq.com/n/ryqq/playlist/{id}", image, musicList);
        }

        public override async Task<UserInfo> GetUserInfo()
        {
            if (string.IsNullOrEmpty(Cookie))
            {
                return null;
            }

            var myInfo = await httpClient.Get<Result<MyInfo>>("/user/detail", new Dictionary<string, string> { { "id", Config.Uin } });
            if (myInfo == null || myInfo.result != 100 || myInfo.data == null)
            {
                LogInfo($"获取用户信息失败 [{myInfo.result}] {myInfo.errMsg}");
                throw new Exception($"获取用户信息失败 [{myInfo.result}] {myInfo.errMsg}");
            }
            return new UserInfo
            {
                Id = Config.Uin,
                Name = myInfo.data.creator.nick,
                Url = $"https://y.qq.com/n/ryqq/profile/like/song?uin={myInfo.data.creator.encrypt_uin}",
            };
        }

        public override async Task<string> Login(string[] args)
        {
            var type = args[0];
            var data = string.Join(" ", args.Skip(1));
            if (data == "")
            {
                return "参数错误 [set|get] {cookie|uin}";
            }
            if (type == "set")
            {
                var cookie = Utils.ProcessCookie(data);
                var cookieDict = Utils.CookieToDict(cookie);
                if (!cookieDict.ContainsKey("uin"))
                {
                    return "Cookie中未找到uin";
                }

                var result = await httpClient.Post<Result<string>>("/user/setCookie", new Dictionary<string, string> { { "data", cookie } }, true);
                if (result == null || result.result != 100)
                {
                    return $"设置失败 [{result.result}] {result.errMsg}";
                }

                Config.Uin = cookieDict["uin"];
                Cookie = cookie;
                Config.Save();

                return "设置成功";
            }
            else if (type == "get")
            {
                var id = data;
                var response = await httpClient.GetHttpResponse($"/user/getCookie?id={id}");
                var heades = response.Headers;

                IEnumerable<string> cookies = heades.GetValues("Set-Cookie");
                if (cookies == null || cookies.Count() == 0)
                {
                    return "未找到用户";
                }
                string cookie = string.Join("; ", cookies);

                var result = await response.AsJson<Result<string>>();
                if (result.result == 100)
                {
                    var newCookie = Utils.MergeCookie(Cookie, Utils.ProcessCookie(cookie));
                    Cookie = newCookie;
                    Config.Uin = data;
                    Config.Save();
                    return "获取成功";
                }
                else
                {
                    return $"获取失败 [{result.result}] {result.message}";
                }
            }
            return "参数错误 [set|get] {cookie|uin}";
        }

        public override void Refresh(QQMusicConfig config)
        {
            Config = config;
            httpClient.SetHeader(Header);
            httpClient.SetBaseUrl(config.ApiServerUrl);
        }

        public override async Task<List<PlayListMeta>> SearchAlbum(string keyword, int limit = 10, int offset = 0)
        {
            if (offset == 0)
            {
                offset = 1;
            }
            var search = await httpClient.Get<Result<SearchData<SearchAlbum>>>("/search", new Dictionary<string, string> { { "key", keyword }, { "pageSize", limit.ToString() }, { "pageNo", offset.ToString() }, { "t", "8" } });
            if (search == null || search.result != 100 || search.data == null)
            {
                throw new Exception($"搜索专辑失败 [{search.result}] {search.errMsg}");
            }
            var list = new List<PlayListMeta>();
            foreach (var album in search.data.list)
            {
                var item = new PlayListMeta(album.albumMID, album.albumName, $"https://y.qq.com/n/ryqq/albumDetail/{album.albumMID}", album.albumPic, null);
                list.Add(item);
            }
            return list;
        }

        public override async Task<List<MusicInfo>> SearchMusic(string keyword, int limit = 10, int offset = 0)
        {
            if (offset == 0)
            {
                offset = 1;
            }
            var search = await httpClient.Get<Result<SearchData<SearchMusic>>>("/search", new Dictionary<string, string> { { "key", keyword }, { "pageSize", limit.ToString() }, { "pageNo", offset.ToString() }, { "t", "0" } });
            if (search == null || search.result != 100 || search.data == null)
            {
                throw new Exception($"搜索歌曲失败 [{search.result}] {search.errMsg}");
            }
            var list = new List<MusicInfo>();
            foreach (var music in search.data.list)
            {
                list.Add(new QQMusicInfo(httpClient, music.songmid, false));
            }
            return list;
        }

        public override async Task<List<PlayListMeta>> SearchPlaylist(string keyword, int limit = 10, int offset = 0)
        {
            if (offset == 0)
            {
                offset = 1;
            }
            var search = await httpClient.Get<Result<SearchData<SearchPlaylist>>>("/search", new Dictionary<string, string> { { "key", keyword }, { "pageSize", limit.ToString() }, { "pageNo", offset.ToString() }, { "t", "2" } });
            if (search == null || search.result != 100 || search.data == null)
            {
                throw new Exception($"搜索歌单失败 [{search.result}] {search.errMsg}");
            }
            var list = new List<PlayListMeta>();
            foreach (var playlist in search.data.list)
            {
                var item = new PlayListMeta(playlist.dissid, playlist.dissname, $"https://y.qq.com/n/ryqq/playlist/{playlist.dissid}", playlist.imgurl, null);
                list.Add(item);
            }
            return list;
        }
    }
}
