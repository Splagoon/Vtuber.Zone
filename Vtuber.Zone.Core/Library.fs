namespace Vtuber.Zone.Core

open System
open System.IO
open FSharp.Json

module ConfigUtils =
    let basePath = AppDomain.CurrentDomain.BaseDirectory

    let jsonConfig =
        JsonConfig.create (jsonFieldNaming = Json.snakeCase, enumValue = EnumMode.Name)

    let loadFile<'a> filePath =
        Path.Combine(basePath, filePath)
        |> File.ReadAllText
        |> Json.deserializeEx<'a> jsonConfig

type Platform =
    | Unknown = 0
    | Youtube = 1
    | Twitch = 2

type Channel = { Platform: Platform; Id: string }

type ChannelInfo =
    { Name: string
      Url: string
      IconUrl: string }

type Stream =
    { Channel: Channel
      Url: string
      ThumbnailUrl: string
      Title: string
      Viewers: uint64 option
      StartTime: DateTimeOffset }

type Vtuber =
    { Name: string
      Channels: Channel list
      TwitterHandle: string
      Tags: string list
      Languages: string list }
    member this.Id = this.Name.ToLower().Replace(" ", "-")

type Config =
    { Youtube: {| BatchSize: int |}
      Vtubers: Vtuber list }
    static member Load() =
        ConfigUtils.loadFile<Config> "settings.json"
        |> fun c ->
            printfn "Loaded %d vtubers" c.Vtubers.Length
            c

type Secrets =
    { Twitter: {| ConsumerKey: string
                  ConsumerSecret: string
                  UserAccessToken: string
                  UserAccessSecret: string |}
      Youtube: {| ApiKey: string |}
      Redis: {| Url: string |} }
    static member Load() = ConfigUtils.loadFile<Secrets> "secrets.json"
