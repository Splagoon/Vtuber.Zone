namespace Vtuber.Zone.Core

open System
open System.IO
open System.Text.RegularExpressions
open FSharp.Json

module JsonUtils =
    let jsonConfig =
        JsonConfig.create (jsonFieldNaming = Json.snakeCase, enumValue = EnumMode.Name, unformatted = true)

    let deserialize<'a> = Json.deserializeEx<'a> jsonConfig
    let serialize<'a> (obj: 'a) = obj |> Json.serializeEx jsonConfig

module ConfigUtils =
    let basePath = AppDomain.CurrentDomain.BaseDirectory

    let loadFile filePath =
        Path.Combine(basePath, filePath)
        |> File.ReadAllText

    let loadJson<'a> = loadFile >> JsonUtils.deserialize<'a>

type Platform =
    | Unknown = 0
    | Youtube = 1
    | Twitch = 2

type PartialChannel =
    { Platform: Platform
      Id: string
      Name: string option }

type FullChannel =
    { Platform: Platform
      Id: string
      Name: string
      Tags: string list
      Languages: string list }

type Stream =
    { Platform: Platform
      VtuberIconUrl: string
      VtuberName: string
      Url: string
      ThumbnailUrl: string
      Title: string
      Viewers: uint64 option
      StartTime: DateTimeOffset
      Tags: string list
      Languages: string list }

type Vtuber =
    { Name: string
      Channels: PartialChannel list
      TwitterHandle: string
      Tags: string list
      Languages: string list }
    member this.Id = Regex("\W").Replace(this.Name.ToLower(), "_")

type Config =
    { Youtube: {| BatchSize: int |}
      Twitch: {| ThumbnailSize: {| Width: int; Height: int |} |}
      Vtubers: Vtuber list }
    static member Load() =
        ConfigUtils.loadJson<Config> "settings.json"
        |> fun c ->
            Log.info "Loaded %d vtubers" c.Vtubers.Length
            c

type TwitchSecrets =
    { ClientId: string
      ClientSecret: string }

type YouTubeSecrets =
    { ApiKey: string }

type RedisSecrets =
    { Url: string }

type Secrets =
    { Twitter: {| ConsumerKey: string
                  ConsumerSecret: string
                  UserAccessToken: string
                  UserAccessSecret: string |}
      Youtube: YouTubeSecrets
      Twitch: TwitchSecrets
      Redis: RedisSecrets }
    static member Load() =
        ConfigUtils.loadJson<Secrets> "secrets.json"
