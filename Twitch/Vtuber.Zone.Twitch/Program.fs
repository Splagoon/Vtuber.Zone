module Vtuber.Zone.Twitch.Main

open System
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Twitch.Client

type TwitchChannel =
    { Channel: FullChannel
      IconUrl: string }

let getStream (channelMap: Map<string, TwitchChannel>) (thumbnailWidth: int, thumbnailHeight: int) (stream: StreamInfo) =
    channelMap
    |> Map.tryFind (stream.UserName.ToLower())
    |> Option.map (fun channel ->
        { Platform = Platform.Twitch
          VtuberIconUrl = channel.IconUrl
          VtuberName = channel.Channel.Name
          Url = sprintf "https://www.twitch.tv/%s" stream.UserName
          ThumbnailUrl =
              stream.ThumbnailUrl.Replace("{width}", thumbnailWidth |> string)
                    .Replace("{height}", thumbnailHeight |> string)
          Title = stream.Title
          Viewers = stream.Viewers |> Some
          StartTime = stream.StartTime
          Tags = channel.Channel.Tags
          Languages = channel.Channel.Languages })

let getStreams (channelMap: Map<string, TwitchChannel>) thumbnailSize (twitchClient: ITwitchClient) =
    let twitchUserNames = channelMap |> Map.toSeq |> Seq.map fst
    async {
        let! res = twitchClient.GetStreams(twitchUserNames)

        return res
               |> Result.map (Seq.choose (getStream channelMap thumbnailSize))
    }

let getChannelMap (twitchClient: ITwitchClient) (vtubers: Vtuber seq) =
    let fullChannelMap =
        vtubers
        |> getFullChannelMap Platform.Twitch
        |> Map.toSeq
        |> Seq.map (fun (k, v) -> k.ToLower(), v)
        |> Map.ofSeq

    let userNames =
        fullChannelMap |> Map.toSeq |> Seq.map fst

    async {
        let! res = twitchClient.GetUsers(userNames)

        return res
               |> Result.map
                   (Seq.map (fun user ->
                       let userName = user.UserName.ToLower()
                       userName,
                       { Channel = fullChannelMap.[userName]
                         IconUrl = user.ProfileImageUrl })
                    >> Map.ofSeq)
    }

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitch

    let twitchClient = getTwitchClient secrets

    let thumbnailSize =
        config.Twitch.ThumbnailSize.Width, config.Twitch.ThumbnailSize.Height

    let mutable channelMap = Map.empty

    let twitchUserNames =
        config.Vtubers
        |> Seq.collect (fun v -> v.Channels)
        |> Seq.choose (fun c -> if c.Platform = Platform.Twitch then Some <| c.Id.ToLower() else None)

    let logMissingChannels () =
        let missingIds =
            getMissingKeys twitchUserNames channelMap

        if not <| Seq.isEmpty missingIds
        then Log.warn "Channel(s) not found: %s" (missingIds |> String.concat ", ")

    let rec streamLoop () =
        async {
            Log.info "Grabbing streams..."
            match! twitchClient
                   |> getStreams channelMap thumbnailSize with
            | Ok streams ->
                Log.info "Got %d streams" (streams |> Seq.length)
                try
                    do! streams |> putPlatformStreams Platform.Twitch
                with err -> Log.exn err "Error storing streams"
            | Error _ -> ()

            do! Async.Sleep <| 60 * 1000
            return! streamLoop ()
        }

    let rec channelLoop () =
        async {
            do! Async.Sleep(TimeSpan.FromHours(12.).TotalMilliseconds |> int)
            Log.info "Grabbing channels"

            match! config.Vtubers |> getChannelMap twitchClient with
            | Ok map ->
                channelMap <- map
                logMissingChannels ()
            | Error _ -> Log.warn "Skipping channel update"
            return! channelLoop ()
        }

    // populate the channel map once before grabbing any streams
    while channelMap = Map.empty do
        match config.Vtubers
              |> getChannelMap twitchClient
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
