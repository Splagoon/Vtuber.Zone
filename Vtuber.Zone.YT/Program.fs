open System
open Google.Apis.Services
open Google.Apis.YouTube.v3
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Core.Redis

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Youtube
    let yt =
        new YouTubeService(
            BaseClientService.Initializer(
                ApiKey = secrets.ApiKey,
                ApplicationName = "vtubers-yt-connector"))

    let getStream (channel : Channel) =
        let searchReq = yt.Search.List(~~"snippet")
        searchReq.ChannelId <- channel.Id
        searchReq.Type <- ~~"video"
        searchReq.EventType <- ~~SearchResource.ListRequest.EventTypeEnum.Live
        match searchReq.Execute().Items |> Seq.toList with
        | stream :: _ ->
            Some { Channel = channel
                   Url = sprintf "https://youtube.com/watch?v=%s" stream.Id.VideoId
                   ThumbnailUrl = stream.Snippet.Thumbnails.Default__.Url
                   Title = stream.Snippet.Title
                   Viewers = 0
                   StartTime = stream.Snippet.PublishedAt |> DateTimeOffset.Parse }
        | _ -> None

    config.Vtubers
    |> Seq.collect (fun x -> x.Channels)
    |> Seq.filter (fun x -> x.Platform = Platform.Youtube)
    |> Seq.map getStream
    |> Seq.choose id
    |> putPlatformStreams Platform.Youtube

    0 // return an integer exit code
