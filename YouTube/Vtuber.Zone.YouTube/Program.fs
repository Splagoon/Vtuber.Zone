module Vtuber.Zone.YouTube.Main

open System
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Core.Redis
open Vtuber.Zone.YouTube.Client
open Vtuber.Zone.YouTube.Redis

type YouTubeChannel =
    { Channel: FullChannel
      ThumbnailUrl: string }

let getStream (channelMap: Map<string, YouTubeChannel>) (stream: StreamInfo) =
    channelMap
    |> Map.tryFind stream.ChannelId
    |> Option.map (fun channel ->
        { Platform = Platform.Youtube
          VtuberIconUrl = channel.ThumbnailUrl
          VtuberName = channel.Channel.Name
          Url = sprintf "https://www.youtube.com/watch?v=%s" stream.Id
          ThumbnailUrl = stream.ThumbnailUrl
          Title = stream.Title
          Viewers = stream.Viewers
          StartTime = stream.StartTime
          Tags = channel.Channel.Tags
          Languages = channel.Channel.Languages })

let getStreams (channelMap: Map<string, YouTubeChannel>) (youtubeClient: IYouTubeClient) (redisClient: IRedisClient) =
    async {
        match! redisClient.GetFoundVideoIds() with
        | Ok videoIds ->
            let! res = youtubeClient.GetStreams videoIds

            return res
                   |> Result.map
                       (Seq.map (function
                           | LiveStream streamInfo -> streamInfo |> getStream channelMap, None
                           | UpcomingStream -> None, None
                           | NotStream videoId -> None, Some videoId)
                        >> (fun streams -> streams |> Seq.choose fst, streams |> Seq.choose snd))
        | Error err -> return Error err
    }

let getChannelMap (youtubeClient: IYouTubeClient) (vtubers: Vtuber seq) =
    let fullChannelMap =
        vtubers |> getFullChannelMap Platform.Youtube

    let channelIds =
        fullChannelMap |> Map.toSeq |> Seq.map fst

    async {
        let! res = youtubeClient.GetChannels(channelIds)

        return res
               |> Result.map
                   (Seq.map (fun channel ->
                       let channelId = channel.Id
                       channelId,
                       { Channel = fullChannelMap.[channelId]
                         ThumbnailUrl = channel.ProfileImageUrl })
                    >> Map.ofSeq)
    }

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load()

    let youtubeClient =
        getYouTubeClient secrets.Youtube config.Youtube.BatchSize

    let redisClient =
        getRedisClient secrets.Redis
        |> Async.RunSynchronously
        |> function
        | Ok client -> client
        | Error err ->
            Log.exn err "Error instantiating Redis client"
            exit 1

    let channelIds =
        config.Vtubers
        |> getChannelIds Platform.Youtube
        |> Seq.distinct

    let mutable channelMap = Map.empty

    let logMissingChannels () =
        let missingIds = getMissingKeys channelIds channelMap

        if not <| Seq.isEmpty missingIds
        then Log.warn "Channel(s) not found: %s" (missingIds |> String.concat ", ")

    let rec streamLoop () =
        async {
            Log.info "Grabbing streams..."
            match! getStreams channelMap youtubeClient redisClient with
            | Ok (streams, badIds) ->
                match! redisClient.PutBadIds badIds with
                | Error err -> Log.exn err "Error putting bad video IDs in Redis"
                | _ -> ()
                do! putPlatformStreams Platform.Youtube streams
            | Error _ -> ()
            do! Async.Sleep(TimeSpan.FromMinutes(1.).TotalMilliseconds |> int)
            return! streamLoop ()
        }

    let rec channelLoop () =
        async {
            do! Async.Sleep(TimeSpan.FromHours(12.).TotalMilliseconds |> int)
            Log.info "Grabbing channels"

            match! config.Vtubers |> getChannelMap youtubeClient with
            | Ok map ->
                channelMap <- map
                logMissingChannels ()
            | Error _ -> Log.warn "Skipping channel update"
            return! channelLoop ()
        }

    // populate the channel map once before grabbing any streams
    while channelMap = Map.empty do
        match config.Vtubers
              |> getChannelMap youtubeClient
              |> Async.RunSynchronously with
        | Ok map ->
            channelMap <- map
            logMissingChannels ()
        | Error _ ->
            Log.warn "Error populating channel map, retrying in 1s..."
            Async.Sleep 1000 |> Async.RunSynchronously

    [ streamLoop (); channelLoop () ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    0 // return an integer exit code
