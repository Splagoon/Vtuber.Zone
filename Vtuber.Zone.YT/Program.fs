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
            (Some { Channel = { Platform = Platform.Youtube
                                Id = video.Snippet.ChannelId }
                    Url = sprintf "https://www.youtube.com/watch?v=%s" video.Id
                    ThumbnailUrl = video.Snippet.Thumbnails.High.Url
                    Title = video.Snippet.Title
                    Viewers = video.LiveStreamingDetails.ConcurrentViewers |> toOption
                    StartTime = video.LiveStreamingDetails.ActualStartTime |> DateTimeOffset.Parse }, None)
        | "upcoming" -> (None, None) // TODO
        | _ -> (None, Some video.Id)

    let badIdsKey = "vtuber.zone.youtube-bad-ids" |> RedisKey
    let putBadVideoIds videoIds =
        let setSize =
            DB.SetAdd(
                badIdsKey,
                videoIds |> Seq.map RedisValue |> Seq.toArray)
        if setSize > 1000L then
            printfn "Bad ID set grew to %d elements, popping" setSize
            DB.SetPop(badIdsKey, 200L) |> ignore

    let getStreams (videoIds : string seq) =
        if Seq.isEmpty videoIds
        then
            Seq.empty, Seq.empty
        else
            let unified = (seq {
                for batch in videoIds |> Seq.chunkBySize config.Youtube.BatchSize do
                    let req = yt.Videos.List(["snippet"; "liveStreamingDetails"] |> Repeatable)
                    printf "Searching for %d videoId(s)..." batch.Length
                    req.Id <- batch |> Repeatable
                    let results = req.Execute().Items
                    printfn "got %d result(s)" results.Count
                    yield! results
                    |> Seq.map videoToStream
            } |> Seq.toList)
            unified |> Seq.map fst |> Seq.choose id, unified |> Seq.map snd |> Seq.choose id

    let getFoundVideos (vtuber : Vtuber) =
        let redisKey = sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id |> RedisKey
        DB.SortedSetRangeByRank(redisKey, 0L, -1L, Order.Descending)
        |> Seq.map (fun x -> x.ToString())
    
    let filterBadIds (videos : string seq) =
        let tmpKey = "vtuber.zone.yt-found-ids-tmp" |> RedisKey
        DB.SetAdd(tmpKey, videos |> Seq.map RedisValue |> Seq.toArray) |> ignore
        let goodIds =
            DB.SetCombine(SetOperation.Difference, tmpKey, badIdsKey)
            |> Seq.map string
        DB.KeyDelete(tmpKey) |> ignore
        goodIds

    let rec loop () =
        let streams, badIds =
            config.Vtubers
            |> Seq.collect getFoundVideos
            |> filterBadIds
            |> getStreams
        
        putBadVideoIds badIds
        putPlatformStreams Platform.Youtube streams
        System.Threading.Thread.Sleep(TimeSpan.FromMinutes 1.)
        loop ()
    loop ()
    0 // return an integer exit code
