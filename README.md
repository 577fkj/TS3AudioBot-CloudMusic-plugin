# TS3AudioBot-CloudMusic-plugin

> Teamspeak 3 流媒体音乐插件

## 支持平台

- 网易云音乐
- QQ 音乐

## 命令

### 基本命令

命令格式 `!yun [命令] {平台} ...`

平台支持缩写和别名

- `[]` 是必选命令
- `{}` 是可选命令

| 命令    | 命令格式                                                    | 注释         |
| ------- | ---------------------------------------------------------- | ------------ |
| login   | `!yun login [平台] [登录方式]`                              | 登录         |
| play    | `!yun play {平台} [歌单/专辑/歌曲/歌名/歌曲ID]`              | 播放         |
| add     | `!yun add {平台} [歌单/专辑/歌曲/歌名/歌曲ID]`               | 下一首播放   |
| next    | `!yun next`                                                | 下一首       |
| gedan   | `!yun gedan {平台} [歌单/歌单名/歌单ID] {长度(max/1-9999)}`  | 歌单         |
| zhuanji | `!yun gedan {平台} [专辑/专辑名/专辑ID] {长度(max/1-9999)}`  | 专辑         |
| mode    | `!yun mode [播放模式]`                                      | 播放模式     |
| list    | `!yun list`                                                 | 查看播放列表 |
| clear   | `!yun clear`                                                | 清空播放列表 |
| stop    | `!yun stop`                                                 | 停止播放     |
| start   | `!yun start`                                                | 开始播放     |
| status  | `!yun status`                                               | 查看登录状态 |
| reload  | `!yun reload`                                               | 重载插件配置 |

播放模式

| 模式 | 注释     |
| ---- | ------- |
| `0`  | 顺序播放 |
| `1`  | 顺序循环 |
| `2`  | 随机播放 |
| `3`  | 随机循环 |

### 网易云音乐

二维码登录：(输入指令后扫描机器人头像二维码登录)  

- `!yun login netease qr`

验证码登录：

- `!yun login netease sms [手机号] {验证码}`

- 先使用 `!yun login netease sms [手机号]` 获取验证码
- 在使用 `!yun login netease sms [手机号] [验证码]` 登录

Cookie登录:

- `!yun login netease cookie [Cookie]`

### QQ 音乐

Cookie登录:

- `!yun login qq set [Cookie]`

云端 Cookie:

- `!yun login qq get [uin]`

### 扩展命令

- `!here {密码}`: 让机器人前往当前频道，需要在服务器聊天框发送

### TS 频道描述(复制代码到频道描述)

```
[COLOR=#ff5500][B]正在播放的歌单的图片和名称可以点机器人看它的头像和描述[/B][/COLOR]

[COLOR=#0055ff]双击机器人, 目前有以下指令([I]把[xxx]替换成对应信息, 包括中括号[/I])[/COLOR]
1.立即播放音乐
[COLOR=#00aa00]!yun play [歌单/专辑/歌曲/歌名/歌曲ID][/COLOR]
2.添加音乐到下一首
[COLOR=#00aa00]!yun add [歌单/专辑/歌曲/歌名/歌曲ID][/COLOR]
3.播放歌单
[COLOR=#00aa00]!yun gedan [歌单名称/歌单网址/歌单ID] {长度(max/1-9999)}[/COLOR]
4.播放专辑
[COLOR=#00aa00]!yun zhuanji [歌单名称/歌单网址/歌单ID] {长度(max/1-9999)}[/COLOR]
5.播放列表中的下一首
[COLOR=#00aa00]!yun next[/COLOR]
6.播放模式选择【0=顺序播放 1=顺序循环 2=随机 3=随机循环】
[COLOR=#00aa00]!yun mode[/COLOR]
7.查看播放列表
[COLOR=#00aa00]!yun list[/COLOR]
8.查看状态
[COLOR=#00aa00]!yun status[/COLOR]
需要注意的是如果歌单歌曲过多需要时间加载, 期间[B]一定一定不要[/B]输入其他指令
```

## 配置文件

```json
{
  "Version": 1, // 配置版本 请勿修改
  "PlayMode": "RandomPlay", // 播放模式
  "AutoPause": true, // 无人自动暂停
  "DefaultApi": "Netease", // 默认API
  "Apis": [ // API 配置
    {
      "Enable": true, // 是否启用API
      "Type": "Netease", // API名称，请勿更改
      "Alias": [ // API 别名
        "n",
        "wy",
        "wyy"
      ],
      "Config": { // API 内部配置
        "RefreshCookie": false, // 是否刷新 Cookie，二维码登录不支持刷新
        "CookieUpdateIntervalMin": 30, // 刷新间隔
        "Header": { // 请求头，需要其他可自行添加
          "Cookie": "",
          "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0"
        },
        "ApiServerUrl": "http://127.0.0.1:3000" // API 地址
      }
    },
    {
      "Enable": true, // 是否启用API
      "Type": "QQMusic", // API名称，请勿更改
      "Alias": [ // API 别名
        "q",
        "qq"
      ],
      "Config": { // API 内部配置
        "RefreshCookie": false, // 是否刷新 Cookie，QQ 登录需要刷新
        "CookieUpdateIntervalMin": 30, // 刷新间隔
        "Header": { // 请求头，需要其他可自行添加
          "Cookie": "",
          "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0"
        },
        "Uin": "12345", // 唯一标识符，QQ 登录就是 QQ 号，微信登录就是 wxuin
        "ApiServerUrl": "http://127.0.0.1:3001" // API 地址
      }
    }
  ]
}
```

## 使用

Github action 默认编译 Docker Hub 最新版本支持和 Master Nightly 支持

需要其他版本请自行编译！

- [Docker Hub](https://registry.hub.docker.com/r/ancieque/ts3audiobot/)
- [Master Nightly](https://splamy.de/api/nightly/projects/ts3ab/master/download)

## 权限

- rights.toml

```toml
"+" = [
	....
	
	"cmd.yun.*"
]
```

## 下载

- [Github action](https://github.com/577fkj/TS3AudioBot-CloudMusic-plugin/actions)
- [Release](https://github.com/577fkj/TS3AudioBot-CloudMusic-plugin/releases)

## 音乐 API 文档

- [NeteaseMusicApi](https://docs-neteasecloudmusicapi.vercel.app/docs/)
- [QQMusicApi](https://jsososo.github.io/QQMusicApi)

## 感谢

- [TS3AudioBot](https://github.com/Splamy/TS3AudioBot)
- [TS3AudioBot-NetEaseCloudmusic-plugin](https://github.com/ZHANGTIANYAO1/TS3AudioBot-NetEaseCloudmusic-plugin)
- [NeteaseMusicApi](https://gitlab.com/Binaryify/neteasecloudmusicapi)
- [QQMusicApi](https://github.com/yuanter/QQMusicApi)
- [QQMusicApi原项目](https://github.com/jsososo/QQMusicApi)
