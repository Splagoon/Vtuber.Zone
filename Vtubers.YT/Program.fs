open Google.Apis.Services
open Google.Apis.YouTube.v3
open Vtubers.Core
open Vtubers.Core.Util
open Vtubers.Core.Redis

[<EntryPoint>]
let main _ =
    let yt =
        new YouTubeService(
            BaseClientService.Initializer(
                ApiKey = "--snip--",
                ApplicationName = "vtubers-yt-connector"))
    let config = Config.Load()

    let getStream (channel : Channel) =
        let searchReq = yt.Search.List(~~"snippet")
        searchReq.ChannelId <- channel.Id
        searchReq.Type <- ~~"video"
        searchReq.EventType <- ~~SearchResource.ListRequest.EventTypeEnum.Live
        match searchReq.Execute().Items |> Seq.toList with
        | stream :: _ -> Some stream.Id.VideoId
        | _ -> None

    config.Vtubers
    |> Seq.collect (fun x -> x.Channels)
    |> Seq.filter (fun x -> x.Platform = Platform.Youtube)
    |> Seq.map getStream
    |> Seq.choose id
    |> Stream.put

    0 // return an integer exit code
