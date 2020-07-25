open System
open Google.Apis.Services
open Google.Apis.YouTube.v3
open Google.Apis.Util
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Youtube
    let yt =
        new YouTubeService(
            BaseClientService.Initializer(
                ApiKey = secrets.ApiKey,
                ApplicationName = "vtubers-yt-connector"))

    let videoToStream (video : Data.Video) =
        match video.Snippet.LiveBroadcastContent with
        | "live" ->
            Some { Channel = { Platform = Platform.Youtube
                               Id = video.Snippet.ChannelId }
                   Url = sprintf "https://youtube.com/watch?v=%s" video.Id
                   ThumbnailUrl = video.Snippet.Thumbnails.High.Url
                   Title = video.Snippet.Title
                   Viewers = video.LiveStreamingDetails.ConcurrentViewers |> toOption
                   StartTime = video.LiveStreamingDetails.ActualStartTime |> DateTimeOffset.Parse }
        | "upcoming" -> None // TODO
        | _ -> None

    let getStreams (videoIds : string seq) =
        if Seq.isEmpty videoIds
        then
            Seq.empty
        else
            let req = yt.Videos.List(["snippet"; "liveStreamingDetails"] |> Repeatable)
            printf "Searching for %d videoId(s)..." (videoIds |> Seq.length)
            req.Id <- videoIds |> Repeatable
            // TODO: batching?
            let results = req.Execute().Items
            printfn "got %d result(s)" results.Count
            results
            |> Seq.map videoToStream
            |> Seq.choose id

    let getFoundVideos (vtuber : Vtuber) =
        let redisKey = sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id |> RedisKey
        DB.SortedSetRangeByRank(redisKey, 0L, -1L, Order.Descending)
        |> Seq.map (fun x -> x.ToString())

    let rec loop _ =
        config.Vtubers
        |> Seq.collect getFoundVideos
        |> getStreams
        |> putPlatformStreams Platform.Youtube
        System.Threading.Thread.Sleep(TimeSpan.FromMinutes 1.)
        loop ()
    loop ()
    0 // return an integer exit code
