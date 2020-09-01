open System
open Google.Apis.Services
open Google.Apis.YouTube.v3
open Google.Apis.Util
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

type VideoResult =
    | GotStream of Stream
    | NotStream of string
    | AskAgainLater

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Youtube
    let yt =
        new YouTubeService(
            BaseClientService.Initializer(
                ApiKey = secrets.ApiKey,
                ApplicationName = "vtubers-yt-connector"))

    let channelMap =
        config.Vtubers
        |> getFullChannelMap Platform.Youtube

    let mutable channelToIconMap = Map.empty
    let getIcon channelId =
        match channelToIconMap |> Map.tryFind channelId with
        | Some icon -> icon
        | None -> defaultIcon

    let videoToStream (video : Data.Video) =
        match video.Snippet.LiveBroadcastContent with
        | "live" ->
            match channelMap |> Map.tryFind video.Snippet.ChannelId with
            | Some channel ->
                GotStream { Platform = Platform.Youtube
                            VtuberIconUrl = video.Snippet.ChannelId |> getIcon
                            VtuberName = channel.Name
                            Url = sprintf "https://www.youtube.com/watch?v=%s" video.Id
                            ThumbnailUrl = video.Snippet.Thumbnails.Standard.Url
                            Title = video.Snippet.Title
                            Viewers = video.LiveStreamingDetails.ConcurrentViewers |> toOption
                            StartTime = video.LiveStreamingDetails.ActualStartTime |> DateTimeOffset.Parse
                            Tags = channel.Tags
                            Languages = channel.Languages }
            | None -> NotStream video.Id
        | "upcoming" -> AskAgainLater // TODO
        | _ -> NotStream video.Id

    let badIdsKey = "vtuber.zone.youtube-bad-ids" |> RedisKey
    let putBadVideoIds videoIds =
        let setSize =
            DB.SetAdd(
                badIdsKey,
                videoIds |> Seq.map RedisValue |> Seq.toArray)
        if setSize > 1000L then
            Log.info "Bad ID set grew to %d elements, popping" setSize
            DB.SetPop(badIdsKey, 200L) |> ignore

    let getStreams =
        Seq.chunkBySize config.Youtube.BatchSize
        >> Seq.mapi (fun batch ids -> async {
            let req = yt.Videos.List(["snippet"; "liveStreamingDetails"] |> Repeatable)
            Log.info "Video batch %d: searching for %d video(s)" (batch+1) ids.Length
            req.Id <- ids |> Repeatable
            match!
                req.ExecuteAsync()
                |> Async.AwaitTask
                |> Async.Catch
                with
            | Choice1Of2 res ->
                    Log.info "Video batch %d: got %d result(s)" (batch+1) res.Items.Count
                    return res.Items
                    |> Seq.map videoToStream
            | Choice2Of2 err ->
                    Log.exn err "Video batch %d: got error" (batch+1)
                    return Seq.empty })
        >> Async.Parallel
        >> Async.RunSynchronously
        >> Seq.concat

    let getChannelToIconMap =
        Seq.chunkBySize config.Youtube.BatchSize
        >> Seq.mapi (fun batch ids -> async {
            let req = yt.Channels.List(["snippet"] |> Repeatable)
            Log.info "Channel batch %d: searching for %d channel(s)..." (batch+1) ids.Length
            req.Id <- ids |> Repeatable
            match!
                req.ExecuteAsync()
                |> Async.AwaitTask
                |> Async.Catch
                with
            | Choice1Of2 res ->
                Log.info "Channel batch %d: got %d result(s)" (batch+1) res.Items.Count
                return res.Items
                |> Seq.map (fun c -> c.Id, c.Snippet.Thumbnails.Medium.Url)
            | Choice2Of2 err ->
                Log.exn err "Channel batch %d: got error" (batch+1)
                return Seq.empty })
        >> Async.Parallel
        >> Async.RunSynchronously
        >> Seq.concat
        >> Map.ofSeq

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
            let results =
                config.Vtubers
                |> Seq.collect getFoundVideos
                |> filterBadIds
                |> getStreams
            let badIds =
                results
                |> Seq.choose
                    (function
                     | NotStream id -> Some id
                     | _ -> None)
            let streams =
                results
                |> Seq.choose
                    (function
                     | GotStream stream -> Some stream
                     | _ -> None)
            
            putBadVideoIds badIds
            do! putPlatformStreams Platform.Youtube streams
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
    let missingIds = getMissingKeys channelIds channelToIconMap
    if not <| Seq.isEmpty missingIds then
        Log.warn "Channel(s) not found: %s" (missingIds |> String.concat ", ")

    [videoLoop (); channelLoop ()] |> Async.Parallel |> Async.RunSynchronously |> ignore
    0 // return an integer exit code
