namespace Vtuber.Zone.Core

open System
open System.IO
open FSharp.Json

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
      Viewers: int
      StartTime: DateTimeOffset }

type Vtuber =
    { Name: string
      Channels: Channel list
      TwitterHandle: string
      Tags: string list
      Languages: string list }
    member this.Id = this.Name.ToLower().Replace(" ", "-")

type Config =
    { Vtubers: Vtuber list }
    static member Load() =
        let jsonConfig =
            JsonConfig.create (jsonFieldNaming = Json.snakeCase, enumValue = EnumMode.Name)

        File.ReadAllText("../settings.json")
        |> Json.deserializeEx<Config> jsonConfig

type Secrets =
    { Twitter:
        {| ConsumerKey: string
           ConsumerSecret: string
           UserAccessToken: string
           UserAccessSecret: string |}
      Youtube:
        {| ApiKey: string |}
      Redis:
        {| Url: string |}}
    static member Load() =
        let jsonConfig =
            JsonConfig.create (jsonFieldNaming = Json.snakeCase, enumValue = EnumMode.Name)
        
        File.ReadAllText("../secrets.json")
        |> Json.deserializeEx<Secrets> jsonConfig
