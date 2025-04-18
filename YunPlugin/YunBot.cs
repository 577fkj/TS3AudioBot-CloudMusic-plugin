using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Plugins;
using TSLib;
using TSLib.Full;
using TSLib.Full.Book;
using TSLib.Messages;
using YunPlugin.api;

namespace YunPlugin
{
    public class YunPlugin : IBotPlugin /* or ICorePlugin */
    {
        private static YunPlugin Instance;
        public static Config config;
        private static string LoggerName = $"TS3AudioBot.Plugins.{typeof(YunPlugin).Namespace}";
        private static NLog.Logger Log = NLog.LogManager.GetLogger(LoggerName);

        private static string PluginVersion = "1.1.3";

        public static NLog.Logger GetLogger(string name = "")
        {
            if (!string.IsNullOrEmpty(name))
            {
                return NLog.LogManager.GetLogger($"{LoggerName}.{name}");
            }
            return Log;
        }

        private PlayManager playManager;
        private Ts3Client ts3Client;
        private Connection serverView;
        private PlayControl playControl;
        private SemaphoreSlim slimlock = new SemaphoreSlim(1, 1);

        TsFullClient TS3FullClient { get; set; }
        public Player PlayerConnection { get; set; }

        private static ulong ownChannelID;
        private static List<ulong> ownChannelClients = new List<ulong>();

        private Dictionary<MusicApiType, IMusicApiInterface> musicApiInterfaces = new Dictionary<MusicApiType, IMusicApiInterface>();

        public YunPlugin(PlayManager playManager, Ts3Client ts3Client, Connection serverView)
        {
            Instance = this;
            this.playManager = playManager;
            this.ts3Client = ts3Client;
            this.serverView = serverView;
        }

        public void Initialize()
        {
            playControl = new PlayControl(playManager, ts3Client, Log);
            loadConfig(playControl);

            playManager.AfterResourceStarted += PlayManager_AfterResourceStarted;
            playManager.PlaybackStopped += PlayManager_PlaybackStopped;


            if (config.AutoPause) {
                TS3FullClient.OnEachClientLeftView += OnEachClientLeftView;
                TS3FullClient.OnEachClientEnterView += OnEachClientEnterView;
                TS3FullClient.OnEachClientMoved += OnEachClientMoved;
            }

            _ = updateOwnChannel();

            ts3Client.SendChannelMessage($"云音乐插件加载成功！Ver: {PluginVersion}");
        }

        private IMusicApiInterface GetApiInterface(MusicApiType type = MusicApiType.None)
        {
            if (type == MusicApiType.None)
            {
                type = config.DefaultApi;
            }
            if (!musicApiInterfaces.ContainsKey(type))
            {
                throw new CommandException("未找到对应的API", CommandExceptionReason.CommandError);
            }
            return musicApiInterfaces[type];
        }

        private void loadConfig(PlayControl playControl)
        {
            var configFileName = "YunSettings.json";
            var configPath = "plugins/";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                string dockerEnvFilePath = "/.dockerenv";

                if (System.IO.File.Exists(dockerEnvFilePath))
                {
                    Log.Info("运行在Docker环境.");
                    configPath = $"{location}/data/plugins/";
                }
                else
                {
                    Log.Info("运行在Linux环境.");
                    configPath = $"{location}/plugins/";
                }
            }
            config = Config.GetConfig($"{configPath}{configFileName}");

            Mode playMode = config.PlayMode;
            playControl.SetMode(playMode);

            Log.Info("Yun Plugin loaded");
            Log.Info($"Play mode: {playMode}");

            var isUpdate = false;
            foreach (var api in MusicApiRegister.ApiInterface)
            {
                var apiContainer = config.GetApiConfig(api.Key);
                if (apiContainer == null)
                {
                    apiContainer = new ApiContainer { Type = api.Key };
                    config.Apis.Add(apiContainer);
                    isUpdate = true;
                }
                if (!apiContainer.Enable)
                {
                    Log.Info($"Api: {api.Key} Disabled");
                    if (musicApiInterfaces.ContainsKey(api.Key))
                    {
                        musicApiInterfaces[api.Key].Dispose();
                        musicApiInterfaces.Remove(api.Key);
                    }
                    continue;
                }
                var apiConfig = apiContainer.Config;
                if (apiConfig == null)
                {
                    var tType = api.Value.BaseType.GetGenericArguments()[0];
                    apiConfig = ((MusicApiConfig)Activator.CreateInstance(tType));
                    apiContainer.Config = apiConfig;
                    isUpdate = true;
                }

                apiConfig.SetSaveAction(() => config.Save());

                if (musicApiInterfaces.ContainsKey(api.Key))
                {
                    musicApiInterfaces[api.Key].RefreshInterface(apiConfig);
                }
                else
                {
                    var instance = (IMusicApiInterface)Activator.CreateInstance(api.Value, playManager, ts3Client, serverView, apiConfig);
                    musicApiInterfaces.Add(api.Key, instance);
                }

                if (apiContainer.Alias == null)
                {
                    var interfaces = musicApiInterfaces[api.Key];
                    apiContainer.Alias = interfaces.DefaultAlias;
                    isUpdate = true;
                }

                Log.Info($"Api: {api.Key} Alias: {string.Join(", ", apiContainer.Alias)}");
            }
            if (isUpdate)
            {
                config.Save();
            }

            Log.Info("Config: {0}", JsonConvert.SerializeObject(config));
        }

        private async Task updateOwnChannel(ulong channelID = 0)
        {
            if (channelID < 1) channelID = (await TS3FullClient.WhoAmI()).Value.ChannelId.Value;
            ownChannelID = channelID;
            ownChannelClients.Clear();
            R<ClientList[], CommandError> r = await TS3FullClient.ClientList();
            if (!r)
            {
                Log.Warn($"Clientlist failed ({r.Error.ErrorFormat()})");
                return;
            }
            foreach (var client in r.Value.ToList())
            {
                if (client.ChannelId.Value == channelID)
                {
                    if (client.ClientId == TS3FullClient.ClientId) continue;
                    ownChannelClients.Add(client.ClientId.Value);
                }
            }
        }

        private void checkOwnChannel()
        {
            if (!config.AutoPause)
            {
                return;
            }
            if (ownChannelClients.Count < 1)
            {
                PlayerConnection.Paused = true;
            }
            else
            {
                PlayerConnection.Paused = false;
            }
            Log.Debug("ownChannelClients: {}", ownChannelClients.Count);
        }

        private async void OnEachClientMoved(object sender, ClientMoved e)
        {
            if (e.ClientId == TS3FullClient.ClientId)
            {
                await updateOwnChannel(e.TargetChannelId.Value);
                return;
            }
            var hasClient = ownChannelClients.Contains(e.ClientId.Value);
            if (e.TargetChannelId.Value == ownChannelID)
            {
                if (!hasClient) ownChannelClients.Add(e.ClientId.Value);
                checkOwnChannel();
            }
            else if (hasClient)
            {
                ownChannelClients.Remove(e.ClientId.Value);
                checkOwnChannel();
            }
        }

        private void OnEachClientEnterView(object sender, ClientEnterView e)
        {
            if (e.ClientId == TS3FullClient.ClientId) return;
            if (e.TargetChannelId.Value == ownChannelID) ownChannelClients.Add(e.ClientId.Value);
            checkOwnChannel();
        }
        private void OnEachClientLeftView(object sender, ClientLeftView e)
        {
            if (e.ClientId == TS3FullClient.ClientId) return;
            if (e.SourceChannelId.Value == ownChannelID) ownChannelClients.Remove(e.ClientId.Value);
            checkOwnChannel();
        }

        private Task PlayManager_AfterResourceStarted(object sender, PlayInfoEventArgs value)
        {
            playControl.SetInvoker(value.Invoker);
            return Task.CompletedTask;
        }

        public async Task PlayManager_PlaybackStopped(object sender, EventArgs e) //当上一首音乐播放完触发
        {
            await slimlock.WaitAsync();
            try
            {
                Log.Debug("上一首歌结束");
                if (playControl.GetPlayList().Count == 0)
                {
                    await ts3Client.ChangeDescription("当前无正在播放歌曲");
                    return;
                }
                await playControl.PlayNextMusic();
            }
            finally
            {
                slimlock.Release();
            }
        }

        [Command("yun mode")]
        public Task<string> PlayMode(int mode)
        {
            if (Enum.IsDefined(typeof(Mode), mode))
            {
                Mode playMode = (Mode)mode;
                playControl.SetMode(playMode);
                config.PlayMode = playMode;
                config.Save();

                return Task.FromResult(playMode switch
                {
                    Mode.SeqPlay => "当前播放模式为顺序播放",
                    Mode.SeqLoopPlay => "当前播放模式为顺序循环",
                    Mode.RandomPlay => "当前播放模式为随机播放",
                    Mode.RandomLoopPlay => "当前播放模式为随机循环",
                    _ => "请输入正确的播放模式",
                });
            }
            else
            {
                return Task.FromResult("请输入正确的播放模式");
            }
        }

        [Command("yun gedan")]
        public async Task<string> CommandPlaylist(string data)
        {
            try
            {
                var input = ProcessArgs(data, "{平台} [歌单名/ID] {长度|max}");
                var api = input.Api;
                var raw = input.Data;
                var inputData = input.InputData;
                var listId = inputData.Id;

                if (inputData.Type == MusicUrlType.None)
                {
                    var playlist = await api.SearchPlaylist(raw, 1);
                    if (playlist.Count == 0)
                    {
                        return "未找到歌单";
                    }
                    listId = playlist[0].Id;
                }
                if (listId == null && inputData.Type != MusicUrlType.PlayList)
                {
                    return "未找到歌单";
                }

                var playListMeta = await api.GetPlayList(listId, input.Limit);
                await ts3Client.SendChannelMessage($"歌单添加完毕：{playListMeta.Name} [{playListMeta.MusicList.Count}]");
                playControl.SetPlayList(playListMeta);
                await playControl.PlayNextMusic();
            }
            catch (Exception e)
            {
                Log.Error(e, "play playlist fail");
                return $"播放歌单失败 {e.Message}";
            }

            return "开始播放歌单";
        }

        [Command("yun play")]
        public async Task<string> CommandYunPlay(string arguments)
        {
            try
            {
                var input = ProcessArgs(arguments, "{平台} [歌名/ID]", 2);
                var api = input.Api;
                var raw = input.Data;
                var inputData = input.InputData;

                switch (inputData.Type)
                {
                    case MusicUrlType.PlayList:
                        return await CommandPlaylist(arguments);
                    case MusicUrlType.Album:
                        return await CommandAlbums(arguments);
                }

                MusicInfo music;
                if (inputData.Type == MusicUrlType.None)
                {
                    var song = await api.SearchMusic(raw, 1);
                    if (song.Count == 0)
                    {
                        return "未找到歌曲";
                    }
                    music = song[0];
                }
                else
                {
                    music = await api.GetMusicInfo(inputData.Id);
                }
                if (config.PlayMode != Mode.SeqPlay && config.PlayMode != Mode.RandomPlay)
                {
                    // 如不是顺序播放或随机播放，添加到播放列表尾
                    playControl.AddMusic(music, false);
                }
                await playControl.PlayMusic(music);
                return null;
            }
            catch (Exception e)
            {
                Log.Error(e, "play music fail");
                return $"播放歌曲失败 {e.Message}";
            }
        }

        [Command("yun add")]
        public async Task<string> CommandYunAdd(string arguments)
        {
            try
            {
                var input = ProcessArgs(arguments, "{平台} [歌名/ID]", 2);
                var api = input.Api;
                var raw = input.Data;
                var inputData = input.InputData;

                MusicInfo music;
                if (inputData.Type == MusicUrlType.None)
                {
                    var song = await api.SearchMusic(raw, 1);
                    if (song.Count == 0)
                    {
                        return "未找到歌曲";
                    }
                    music = song[0];
                }
                else
                {
                    music = await api.GetMusicInfo(inputData.Id);
                }
                playControl.AddMusic(music);
                return "已添加到下一首播放";
            }
            catch (Exception e)
            {
                Log.Error(e, "add music fail");
                return $"播放歌曲失败  {e.Message}";
            }
        }

        [Command("yun next")]
        public async Task<string> CommandYunNext(PlayManager playManager)
        {
            var playList = playControl.GetPlayList();
            if (playList.Count == 0)
            {
                return "播放列表为空";
            }
            if (playManager.IsPlaying)
            {
                await playManager.Stop();
            }
            return null;
        }

        [Command("yun reload")]
        public string CommandYunReload()
        {
            loadConfig(playControl);
            return "配置已重新加载";
        }

        [Command("yun login")]
        public async Task<string> CommandYunLogin(string arguments)
        {
            try
            {
                var args = Utils.ProcessArgs(arguments);
                var api = config.GetApiConfig(args[0]);
                if (api == null)
                {
                    return $"未找到对应的API [{args[0]}]";
                }
                var apiInterface = GetApiInterface(api.Type);
                args = args.Skip(1).ToArray();
                return await apiInterface.Login(args);
            }
            catch (Exception e)
            {
                Log.Error(e, "login fail");
                return $"登录失败 {e.Message}";
            }
        }

        [Command("yun list")]
        public async Task<string> PlayList()
        {
            var playList = playControl.GetPlayList();
            if (playList.Count == 0)
            {
                return "播放列表为空";
            }
            return await playControl.GetPlayListString();
        }

        [Command("yun status")]
        public async Task<string> CommandStatus()
        {
            string result = "\n";
            foreach (var api in musicApiInterfaces)
            {
                result += $"[{api.Value.Name}]\nApi: {api.Value.GetApiServerUrl()}\n当前用户: ";
                try
                {
                    var userInfo = await api.Value.GetUserInfo();
                    if (userInfo == null)
                    {
                        result += "未登录\n";
                    }
                    else
                    {
                        result += $"[URL={userInfo.Url}]{userInfo.Name}[/URL]";
                        if (userInfo.Extra != null)
                        {
                            result += $" ({userInfo.Extra})";
                        }
                        result += "\n";
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "get user info error");
                    result += $"获取失败 {e.Message}\n";
                }
            }
            return result;
        }

        [Command("here")]
        public async Task<string> CommandHere(ClientCall invoker, string password = null)
        {
            ChannelId channel = invoker.ChannelId.Value!;
            await ts3Client.MoveTo(channel, password);
            return "已移动到你所在的频道";
        }

        [Command("yun zhuanji")]
        public async Task<string> CommandAlbums(string arguments)
        {
            var input = ProcessArgs(arguments, "{平台} [专辑/ID] {长度|max}");
            var api = input.Api;
            var raw = input.Data;
            var inputData = input.InputData;
            var id = inputData.Id;

            try
            {
                if (inputData.Type == MusicUrlType.None)
                {
                    var album = await api.SearchAlbum(raw, 1);
                    if (album.Count == 0)
                    {
                        return "未找到专辑";
                    }
                    id = album[0].Id.ToString();
                }

                var albumDetail = await api.GetAlbums(id, input.Limit);
                await ts3Client.SendChannelMessage($"专辑添加完毕：{albumDetail.Name} [{albumDetail.MusicList.Count}]");
                playControl.SetPlayList(albumDetail);
                await playControl.PlayNextMusic();
            }
            catch (Exception e)
            {
                Log.Error(e, "play album error");
                return "播放专辑失败";
            }

            return "开始播放专辑";
        }

        [Command("yun clear")]
        public async Task<string> CommandYunClear(PlayManager playManager)
        {
            playControl.Clear();
            if (playManager.IsPlaying)
            {
                await playManager.Stop();
            }
            return "已清除歌单";
        }

        [Command("yun stop")]
        public async Task<string> CommandYunPause()
        {
            if (playManager.IsPlaying)
            {
                await playManager.Stop();
                return "已停止播放";
            }
            return "当前没有播放";
        }

        [Command("yun start")]
        public async Task<string> CommandYunStart()
        {
            if (!playManager.IsPlaying)
            {
                await playControl.PlayNextMusic();
                return "开始播放";
            }
            return "当前正在播放";
        }

        public void Dispose()
        {
            Instance = null;
            config = null;
            playControl = null;
            foreach (var api in musicApiInterfaces)
            {
                api.Value.Dispose();
            }
            musicApiInterfaces = null;

            playManager.AfterResourceStarted -= PlayManager_AfterResourceStarted;
            playManager.PlaybackStopped -= PlayManager_PlaybackStopped;
            TS3FullClient.OnEachClientLeftView -= OnEachClientLeftView;
            TS3FullClient.OnEachClientEnterView -= OnEachClientEnterView;
            TS3FullClient.OnEachClientMoved -= OnEachClientMoved;
        }

        public CommandArgs ProcessArgs(string args, string useHelp, int argsLen = 3)
        {
            // netease url max
            // netease url
            // url max
            // url
            var result = new CommandArgs();
            var sp = Utils.ProcessArgs(Utils.RemoveBBCode(args));
            if (sp.Length == 0)
            {
                throw new CommandException(useHelp, CommandExceptionReason.CommandError);
            }
            if (sp.Length > argsLen)
            {
                //throw new CommandException($"参数过多 {useHelp}", CommandExceptionReason.CommandError);
                string[] newSP = new string[argsLen];
                Array.Copy(sp, newSP, argsLen - 1);
                newSP[argsLen - 1] = string.Join(" ", sp.Skip(argsLen - 1));
                sp = newSP;
            }

            if (sp.Length >= 2 && (sp.Last() == "max" || (Utils.IsNumber(sp.Last()) && sp.Length <= 4)))
            {
                if (sp.Last() == "max")
                {
                    result.Limit = 0;
                }
                else
                {
                    result.Limit = int.Parse(sp.Last());
                }
                sp = sp.Take(sp.Length - 1).ToArray();
            }

            switch (sp.Length)
            {
                case 2:
                    var api = config.GetApiConfig(sp[0]);
                    if (api == null || !api.Enable)
                    {
                        throw new CommandException($"未找到对应的API [{sp[0]}]", CommandExceptionReason.CommandError);
                    }
                    result.Api = GetApiInterface(api.Type);
                    sp = sp.Skip(1).ToArray();
                    goto case 1;
                case 1:
                    result.Data = sp[0];
                    if (result.Api == null)
                    {
                        foreach (var item in musicApiInterfaces)
                        {
                            foreach (var a in item.Value.KeyInUrl)
                            {
                                if (result.Data.Contains(a))
                                {
                                    result.Api = item.Value;
                                    break;
                                }
                            }
                            if (result.Api != null)
                            {
                                break;
                            }
                        }
                        if (result.Api == null)
                        {
                            foreach (var item in musicApiInterfaces)
                            {
                                var input = item.Value.GetInputData(result.Data);
                                if (input.Type != MusicUrlType.None)
                                {
                                    result.Api = item.Value;
                                    result.InputData = input;
                                    break;
                                }
                            }
                        }
                    }
                    if (result.Api == null)
                    {
                        result.Api = GetApiInterface();
                    }
                    if (result.InputData == null)
                    {
                        result.InputData = result.Api.GetInputData(result.Data);
                    }

                    break;
            }

            return result;
        }
    }

    public class CommandArgs
    {
        public IMusicApiInterface Api { get; set; }
        public MusicApiInputData InputData { get; set; }
        public string Data { get; set; }
        public int Limit { get; set; }

        public CommandArgs()
        {
            Api = null;
            InputData = null;
            Data = null;
            Limit = 100;
        }

        public override string ToString()
        {
            return $"Api: {Api.Name} Data: {Data} Limit: {Limit}";
        }
    }
}