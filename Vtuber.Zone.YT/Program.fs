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

    let channelToVtuberMap =
        config.Vtubers
        |> getChannelToVtuberMap Platform.Youtube

    let mutable channelToIconMap = Map.empty
    let getIcon channelId =
        match channelToIconMap |> Map.tryFind channelId with
        | Some icon -> icon
        | None -> defaultIcon

    let videoToStream (video : Data.Video) =
        match video.Snippet.LiveBroadcastContent with
        | "live" ->
            match channelToVtuberMap |> Map.tryFind video.Snippet.ChannelId with
            | Some vtubers ->
                (Some { Platform = Platform.Youtube
                        VtuberIconUrl = video.Snippet.ChannelId |> getIcon
                        VtuberName = vtubers |> combineNames
                        Url = sprintf "https://www.youtube.com/watch?v=%s" video.Id
                        ThumbnailUrl = video.Snippet.Thumbnails.Standard.Url
                        Title = video.Snippet.Title
                        Viewers = video.LiveStreamingDetails.ConcurrentViewers |> toOption
                        StartTime = video.LiveStreamingDetails.ActualStartTime |> DateTimeOffset.Parse
                        Tags = vtubers |> combineTags
                        Languages = vtubers |> combineLanguages }, None)
            | None -> (None, Some video.Id)
        | "upcoming" -> (None, None) // TODO
        | _ -> (None, Some video.Id)

    let badIdsKey = "vtuber.zone.youtube-bad-ids" |> RedisKey
    let putBadVideoIds videoIds =
        let setSize =
            DB.SetAdd(
                badIdsKey,
                videoIds |> Seq.map RedisValue |> Seq.toArray)
        if setSize > 1000L then
            Log.info "Bad ID set grew to %d elements, popping" setSize
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
                    Log.info "got %d result(s)" results.Count
                    yield! results
                    |> Seq.map videoToStream
            } |> Seq.toList)
            unified |> Seq.map fst |> Seq.choose id, unified |> Seq.map snd |> Seq.choose id

    let getChannelToIconMap (channelIds : string seq) =
        if Seq.isEmpty channelIds
        then
            Map.empty
        else
            seq {
                for batch in channelIds |> Seq.chunkBySize config.Youtube.BatchSize do
                let req = yt.Channels.List(["snippet"] |> Repeatable)
                printf "Searching for %d channelId(s)..." batch.Length
                req.Id <- batch |> Repeatable
                let results = req.Execute().Items
                Log.info "got %d result(s)" results.Count
                yield! results
                |> Seq.map (fun c -> c.Id, c.Snippet.Thumbnails.Medium.Url)
            } |> Map.ofSeq

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

    let rec videoLoop () = 
        async {
            Log.info "Grabbing videos"
            let streams, badIds =
                config.Vtubers
                |> Seq.collect getFoundVideos
                |> filterBadIds
                |> getStreams
            
            putBadVideoIds badIds
            putPlatformStreams Platform.Youtube streams
            do! Async.Sleep (TimeSpan.FromMinutes(1.).TotalMilliseconds |> int)
            return! videoLoop ()
        }

    let channelIds =
        config.Vtubers
        |> getChannelIds Platform.Youtube
        |> Seq.distinct

    let rec channelLoop () =
        async {
            do! Async.Sleep (TimeSpan.FromHours(12.).TotalMilliseconds |> int) 
            Log.info "Grabbing channels"
            channelToIconMap <- channelIds |> getChannelToIconMap
            return! channelLoop ()
        }

    channelToIconMap <- channelIds |> getChannelToIconMap // populate the channel map once before grabbing videos
    if Map.count channelToIconMap < Seq.length channelIds then
        seq {
            for id in channelIds do
                if channelToIconMap |> Map.tryFind id |> Option.isNone then
                    yield id
        } |> Log.info "Channel(s) not found: %A"

    [videoLoop (); channelLoop ()] |> Async.Parallel |> Async.RunSynchronously |> ignore
    0 // return an integer exit code
