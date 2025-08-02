using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace YunPlugin.api.qq
{
    public class Preview
    {
        public int trybegin { get; set; }
        public int tryend { get; set; }
        public int trysize { get; set; }
    }

    public class SingerData
    {
        public int id { get; set; }
        public string mid { get; set; }
        public string name { get; set; }
        public string name_hilight { get; set; }
    }

    public class SearchMusic
    {
        public int albumid { get; set; }
        public string albummid { get; set; }
        public string albumname { get; set; }
        public string albumname_hilight { get; set; }
        public int alertid { get; set; }
        public int belongCD { get; set; }
        public int cdIdx { get; set; }
        public int chinesesinger { get; set; }
        public string docid { get; set; }
        public List<string> grp { get; set; }
        public int interval { get; set; }
        public int isonly { get; set; }
        public string lyric { get; set; }
        public string lyric_hilight { get; set; }
        public string media_mid { get; set; }
        public int msgid { get; set; }
        public int newStatus { get; set; }
        public long nt { get; set; }
        public Preview preview { get; set; }
        public int pubtime { get; set; }
        public int pure { get; set; }
        public List<SingerData> singer { get; set; }
        public int size128 { get; set; }
        public int size320 { get; set; }
        public int sizeape { get; set; }
        public int sizeflac { get; set; }
        public int sizeogg { get; set; }
        public int songid { get; set; }
        public string songmid { get; set; }
        public string songname { get; set; }
        public string songname_hilight { get; set; }
        public string strMediaMid { get; set; }
        public int stream { get; set; }
        public int @switch { get; set; }
        public int t { get; set; }
        public int tag { get; set; }
        public int type { get; set; }
        public int ver { get; set; }
        public string vid { get; set; }
    }

    public class SearchData<T>
    {
        public List<T> list { get; set; }
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public int total { get; set; }
        public string key { get; set; }
        public string type { get; set; }
    }

    public class AlbumData
    {
        public List<Track_info> list { get; set; }
        public int total { get; set; }
        public string albummid { get; set; }
    }

    public class Headpiclist
    {
        public string picurl { get; set; }
    }

    public class Ar
    {
        public string mid { get; set; }
        public string name { get; set; }
        public string id { get; set; }
    }

    public class AlbumInfo
    {
        public List<string> buyright { get; set; }
        public string commnum { get; set; }
        public string companyname { get; set; }
        public string desc { get; set; }
        public string dis_end { get; set; }
        public int dis_price { get; set; }
        public string dis_start { get; set; }
        public List<Headpiclist> headpiclist { get; set; }
        public int price { get; set; }
        public string score { get; set; }
        public string soldamt { get; set; }
        public string soldcount { get; set; }
        public string uin { get; set; }
        public List<Ar> ar { get; set; }
        public string name { get; set; }
        public string mid { get; set; }
        public string id { get; set; }
        public string publishTime { get; set; }
    }

    public class Content
    {
        public int id { get; set; }
        public string value { get; set; }
        public string mid { get; set; }
        public int type { get; set; }
        public int show_type { get; set; }
        public int is_parent { get; set; }
        public string picurl { get; set; }
        public int read_cnt { get; set; }
        public string author { get; set; }
        public string jumpurl { get; set; }
        public string ori_picurl { get; set; }
    }

    public class MoreData
    {
        public string title { get; set; }
        public string type { get; set; }
        public List<Content> content { get; set; }
        public int pos { get; set; }
        public int more { get; set; }
        public string selected { get; set; }
        public int use_platform { get; set; }
    }

    public class Info
    {
        public MoreData company { get; set; }
        public MoreData genre { get; set; }
        public MoreData intro { get; set; }
        public MoreData lan { get; set; }
        public MoreData pub_time { get; set; }
    }

    public class Extras
    {
        public string name { get; set; }
        public string transname { get; set; }
        public string subtitle { get; set; }
        public string @from { get; set; }
        public string wikiurl { get; set; }
    }

    public class Album
    {
        public int id { get; set; }
        public string mid { get; set; }
        public string name { get; set; }
        public string title { get; set; }
        public string subtitle { get; set; }
        public string time_public { get; set; }
        public string pmid { get; set; }
    }

    public class Track_info
    {
        public int id { get; set; }
        public int type { get; set; }
        public string mid { get; set; }
        public string name { get; set; }
        public string title { get; set; }
        public string subtitle { get; set; }
        public List<SingerData> singer { get; set; }
        public Album album { get; set; }
        public int interval { get; set; }
        public int isonly { get; set; }
        public int language { get; set; }
        public int genre { get; set; }
        public int index_cd { get; set; }
        public int index_album { get; set; }
        public string time_public { get; set; }
        public int status { get; set; }
        public int fnote { get; set; }
        public string label { get; set; }
        public string url { get; set; }
        public int bpm { get; set; }
        public int version { get; set; }
        public string trace { get; set; }
        public int data_type { get; set; }
        public int modify_stamp { get; set; }
        public string pingpong { get; set; }
        public string ppurl { get; set; }
        public int tid { get; set; }
        public int ov { get; set; }
        public int sa { get; set; }
        public string es { get; set; }
        public List<string> vs { get; set; }
        public List<int> vi { get; set; }
        public string ktag { get; set; }
        public List<double> vf { get; set; }
    }

    public class SongInfo
    {
        public Info info { get; set; }
        public Extras extras { get; set; }
        public Track_info track_info { get; set; }
    }

    public class Songlist
    {
        public string albumdesc { get; set; }
        public int albumid { get; set; }
        public string albummid { get; set; }
        public string albumname { get; set; }
        public int alertid { get; set; }
        public int belongCD { get; set; }
        public int cdIdx { get; set; }
        public int interval { get; set; }
        public int isonly { get; set; }
        public string label { get; set; }
        public int msgid { get; set; }
        public Preview preview { get; set; }
        public int rate { get; set; }
        public List<SingerData> singer { get; set; }
        public int size128 { get; set; }
        public int size320 { get; set; }
        public int size5_1 { get; set; }
        public int sizeape { get; set; }
        public int sizeflac { get; set; }
        public int sizeogg { get; set; }
        public int songid { get; set; }
        public string songmid { get; set; }
        public string songname { get; set; }
        public string songorig { get; set; }
        public int songtype { get; set; }
        public string strMediaMid { get; set; }
        public int stream { get; set; }
        public int @switch { get; set; }
        public int type { get; set; }
        public string vid { get; set; }
    }

    public class QQMusicPlayListTag
    {
    public int id { get; set; }
    public string name { get; set; }
    public int pid { get; set; }
    }

    public class PlaylistInfo
        {
            public string disstid { get; set; }
            public int dir_show { get; set; }
            public int owndir { get; set; }
            public int dirid { get; set; }
            public string coveradurl { get; set; }
            public int dissid { get; set; }
            public string login { get; set; }
            public string uin { get; set; }
            public string encrypt_uin { get; set; }
            public string dissname { get; set; }
            public string logo { get; set; }
            public string pic_mid { get; set; }
            public string album_pic_mid { get; set; }
            public int pic_dpi { get; set; }
            public int isAd { get; set; }
            public string desc { get; set; }
            public int ctime { get; set; }
            public int mtime { get; set; }
            public string headurl { get; set; }
            public string ifpicurl { get; set; }
            public string nick { get; set; }
            public string nickname { get; set; }
            public int type { get; set; }
            public int singerid { get; set; }
            public string singermid { get; set; }
            public int isvip { get; set; }
            public int isdj { get; set; }
            public List<QQMusicPlayListTag> tags { get; set; }
            public int songnum { get; set; }
            public string songids { get; set; }
            public string songtypes { get; set; }
            public int disstype { get; set; }
            public string dir_pic_url2 { get; set; }
            public int song_update_time { get; set; }
            public int song_update_num { get; set; }
            public int total_song_num { get; set; }
            public int song_begin { get; set; }
            public int cur_song_num { get; set; }
            public List<Songlist> songlist { get; set; }
            public int visitnum { get; set; }
            public int cmtnum { get; set; }
            public int buynum { get; set; }
            public string scoreavage { get; set; }
            public int scoreusercount { get; set; }
        }

    public class Creator
    {
        public string avatarUrl { get; set; }
        public string creator_uin { get; set; }
        public string encrypt_uin { get; set; }
        public int followflag { get; set; }
        public int isVip { get; set; }
        public string name { get; set; }
        public int qq { get; set; }
        public int singerid { get; set; }
        public string singermid { get; set; }
        public int type { get; set; }
    }

    public class SearchPlaylist
    {
        public int copyrightnum { get; set; }
        public string createtime { get; set; }
        public Creator creator { get; set; }
        public int diss_status { get; set; }
        public string dissid { get; set; }
        public string dissname { get; set; }
        public int docid { get; set; }
        public string imgurl { get; set; }
        public string introduction { get; set; }
        public int listennum { get; set; }
        public int score { get; set; }
        public int song_count { get; set; }
    }

    public class SearchAlbum
    {
        public int albumID { get; set; }
        public string albumMID { get; set; }
        public string albumName { get; set; }
        public string albumName_hilight { get; set; }
        public string albumPic { get; set; }
        public string catch_song { get; set; }
        public string docid { get; set; }
        public string publicTime { get; set; }
        public int singerID { get; set; }
        public string singerMID { get; set; }
        public string singerName { get; set; }
        public string singerName_hilight { get; set; }
        public string singerTransName { get; set; }
        public string singerTransName_hilight { get; set; }
        public List<SingerData> singer_list { get; set; }
        public int song_count { get; set; }
        public int type { get; set; }
    }

    public class MyCreator
    {
        public string nick { get; set; }
        public string encrypt_uin { get; set; }
    }

    public class MyInfo
    {
        public MyCreator creator { get; set; }
    }


    public class Result<T>
    {
        public int result { get; set; }
        public T data { get; set; }
        public string errMsg { get; set; }
        public string message { get; set; }
    }

}
