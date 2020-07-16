namespace Vtuber.Zone.Core

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
      Title: string }

type Vtuber =
    { Name: string
      Channels: Channel list
      TwitterHandle: string option
      Tags: string list
      Languages: string list }

type Config =
    { Vtubers: Vtuber list }
    static member Load() =
        let jsonConfig =
            JsonConfig.create (jsonFieldNaming = Json.snakeCase, enumValue = EnumMode.Name)

        File.ReadAllText("../settings.json")
        |> Json.deserializeEx<Config> jsonConfig
