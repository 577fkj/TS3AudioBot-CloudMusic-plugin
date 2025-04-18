using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TS3AudioBot.ResourceFactories;

public enum Mode
{
    SeqPlay = 0,
    SeqLoopPlay = 1,
    RandomPlay = 2,
    RandomLoopPlay = 3,
}

public class PlayListMeta
{
    public string Id;
    public string Url;
    public string Name;
    public string Image;
    public List<MusicInfo>? MusicList;

    public PlayListMeta(string id, string name, string url, string image, List<MusicInfo>? musicInfos)
    {
        Id = id;
        Url = url;
        Name = name;
        Image = image;
        MusicList = musicInfos;
    }
}

public abstract class MusicInfo
{
    public abstract string ArtistUrl { get; }
    public readonly NLog.Logger Log;

    public string Id = "";
    public string Name = "";
    public string Image = "";
    public string DetailUrl = "";
    public bool InPlayList;
    public Dictionary<string, string?> Author = new Dictionary<string, string?>();

    public MusicInfo(string id, bool inPlayList = true)
    {
        Id = id;
        InPlayList = inPlayList;
        Log = YunPlugin.YunPlugin.GetLogger(GetType().Name);
    }

    public string GetAuthor()
    {
        return string.Join(" / ", Author.Keys);
    }

    public string GetFullName()
    {
        var author = GetAuthor();
        author = !string.IsNullOrEmpty(author) ? $" - {author}" : "";
        return Name + author;
    }

    public string GetFullNameBBCode()
    {
        var author = GetAuthorBBCode();
        author = !string.IsNullOrEmpty(author) ? $" - {author}" : "";
        return $"[URL={DetailUrl}]{Name}[/URL]{author}";
    }

    public string GetAuthorBBCode()
    {
        return string.Join(" / ", Author.Select(entry =>
        {
            string key = entry.Key;
            string? id = entry.Value;
            string authorName = id == null ? key : $"[URL={string.Format(ArtistUrl, id)}]{key}[/URL]";
            return authorName;
        }));
    }

    public AudioResource GetMusicInfo()
    {
        var ar = new AudioResource(DetailUrl, GetFullName(), "media")
                    .Add("PlayUri", Image);
        return ar;
    }

    public async Task<byte[]> GetImage()
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Image);
        request.Method = "GET";

        using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
        using (Stream stream = response.GetResponseStream())
        using (MemoryStream memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }

    public abstract Task InitMusicInfo();

    public abstract Task<string> GetMusicUrl();
}