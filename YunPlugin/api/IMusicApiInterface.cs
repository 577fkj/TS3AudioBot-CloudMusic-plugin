using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot;
using TSLib.Full.Book;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using YunPlugin.api.netease;
using YunPlugin.api.qq;

namespace YunPlugin.api
{
    public enum MusicApiType
    {
        None = 0,
        Netease,
        QQMusic
    }

    public enum MusicUrlType
    {
        None = 0,
        Music,
        PlayList,
        Album,
        Number
    }

    public class UserInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Extra { get; set; }
    }

    public static class MusicApiRegister
    {
        public static Dictionary<MusicApiType, Type> ApiInterface = new Dictionary<MusicApiType, Type>
        {
            { MusicApiType.Netease, typeof(NeteaseMusic) },
            { MusicApiType.QQMusic, typeof(QQMusic) }
        };
    }

    public class MusicApiConfigConverter : JsonConverter<ApiContainer>
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override ApiContainer ReadJson(JsonReader reader, Type objectType, [AllowNull] ApiContainer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            if (!jo.TryGetValue("Type", out JToken types))
            {
                throw new KeyNotFoundException("No Type field in ApiContainer");
            }
            if (!Enum.TryParse(types.ToString(), out MusicApiType type))
            {
                throw new KeyNotFoundException($"Unsupported MusicApiType: {types}");
            }
            ApiContainer container = new ApiContainer();
            container.Type = type;
            if (!jo.TryGetValue("Config", out JToken config))
            {
                throw new KeyNotFoundException("No Config field in ApiContainer");
            }

            if (!MusicApiRegister.ApiInterface.TryGetValue(type, out Type iface))
            {
                throw new KeyNotFoundException($"No MusicApiInterface in MusicApiRegister: {type}");
            }
            var tTypes = iface.BaseType.GetGenericArguments();
            if (tTypes.Length != 1)
            {
                throw new KeyNotFoundException($"No GenericArguments in MusicApiInterface: {iface}, {tTypes}");
            }
            var tType = tTypes[0];
            if (tType == null)
            {
                throw new KeyNotFoundException($"No Config Type in MusicApiRegister: {type}");
            }
            if (tType.IsAssignableFrom(typeof(MusicApiConfig)))
            {
                throw new KeyNotFoundException($"Config Type is not MusicApiConfig: {tType}");
            }
            container.Config = (MusicApiConfig)config.ToObject(tType);
            if (jo.TryGetValue("Alias", out JToken alias))
            {
                container.Alias = alias.ToObject<string[]>();
            }
            else
            {
                container.Alias = new string[0];
            }
            return container;
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] ApiContainer value, JsonSerializer serializer) { }
    }

    public abstract class MusicApiConfig
    {
        public string ApiServerUrl { get; set; }

        [JsonIgnore]
        private Action save;

        public void SetSaveAction(Action save)
        {
            this.save = save;
        }

        public void Save()
        {
            save();
        }
    }

    public class MusicApiInputData
    {
        public MusicUrlType Type { get; set; }
        public string Id { get; set; }
        public string Url { get; set; }

        public MusicApiInputData()
        {
            Type = MusicUrlType.None;
            Id = null;
            Url = null;
        }

        public override string ToString()
        {
            return $"Type: {Type}, Id: {Id}, Url: {Url}";
        }
    }

    public abstract class MusicApi<T> : IMusicApiInterface where T : MusicApiConfig
    {
        public PlayManager playManager;
        public Ts3Client ts3Client;
        public Connection serverView;

        public T Config;

        private readonly NLog.Logger Log;

        public MusicApi(PlayManager playManager, Ts3Client ts3Client, Connection serverView, T config)
        {
            this.playManager = playManager;
            this.ts3Client = ts3Client;
            this.serverView = serverView;
            Config = config;

            Log = YunPlugin.GetLogger(GetType().Name);
            LogInfo($"Init {GetType().Name}");
        }

        public void LogInfo(string msg)
        {
            Log.Info($"[{Name}] {msg}");
        }

        public void LogError(string msg)
        {
            Log.Error($"[{Name}] {msg}");
        }

        public void LogDebug(string msg) {
            Log.Debug($"[{Name}] {msg}");
        }

        public void LogWarning(string msg)
        {
            Log.Warn($"[{Name}] {msg}");
        }

        public void LogInfo(Exception e, string msg)
        {
            Log.Info(e, $"[{Name}] {msg}");
        }

        public void LogError(Exception e, string msg)
        {
            Log.Error(e, $"[{Name}] {msg}");
        }

        public void LogDebug(Exception e, string msg)
        {
            Log.Debug(e, $"[{Name}] {msg}");
        }

        public void LogWarning(Exception e, string msg)
        {
            Log.Warn(e, $"[{Name}] {msg}");
        }

        public abstract string Name { get; }
        public abstract MusicApiType Key { get; }
        public abstract string[] DefaultAlias { get; }
        public abstract string[] KeyInUrl { get; }

        public abstract void Refresh(T config);
        public abstract void Dispose();

        public abstract Task<PlayListMeta> GetAlbums(string id, int limit);
        public abstract Task<MusicInfo> GetMusicInfo(string id);
        public abstract Task<PlayListMeta> GetPlayList(string id, int limit);
        public abstract MusicApiInputData GetInputData(string url);
        public abstract Task<UserInfo> GetUserInfo();
        public abstract Task<string> Login(string[] args);
        public abstract Task<List<PlayListMeta>> SearchAlbum(string keyword, int limit = 10, int offset = 0);
        public abstract Task<List<MusicInfo>> SearchMusic(string keyword, int limit = 10, int offset = 0);
        public abstract Task<List<PlayListMeta>> SearchPlaylist(string keyword, int limit = 10, int offset = 0);

        public string GetApiServerUrl()
        {
            return Config.ApiServerUrl;
        }

        public void RefreshInterface(object config)
        {
            Refresh((T)config);
        }
    }

    public interface IMusicApiInterface
    {
        public string Name { get; }
        public MusicApiType Key { get; }
        public string[] DefaultAlias { get; }
        public string[] KeyInUrl { get; }

        public Task<string> Login(string[] args);

        public MusicApiInputData GetInputData(string url);

        public Task<List<MusicInfo>> SearchMusic(string keyword, int limit = 10, int offset = 0);
        public Task<List<PlayListMeta>> SearchPlaylist(string keyword, int limit = 10, int offset = 0);
        public Task<List<PlayListMeta>> SearchAlbum(string keyword, int limit = 10, int offset = 0);

        public Task<PlayListMeta> GetPlayList(string id, int limit);
        public Task<PlayListMeta> GetAlbums(string id, int limit);

        public Task<MusicInfo> GetMusicInfo(string id);

        public Task<UserInfo?> GetUserInfo();

        public string GetApiServerUrl();

        public void RefreshInterface(object config);
        public void Dispose();
    }
}
