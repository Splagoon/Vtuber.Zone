open System
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Twitch.Client

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitch
    let twitchClient = getTwitchClient secrets

    let getTwitchChannels vtuber =
        vtuber.Channels
        |> Seq.filter (fun c -> c.Platform = Platform.Twitch)
        |> Seq.map (fun channel -> vtuber, channel)

    let twitchChannels =
        config.Vtubers
        |> Seq.collect getTwitchChannels
        |> Seq.map
            (fun (v, c) ->
                Log.info "Following %s (%s)" v.Name c.Id
                c.Id)
        |> Collections.Generic.List

    let channelMap =
        config.Vtubers
        |> getFullChannelMap Platform.Twitch
        |> Map.toSeq
        |> Seq.map (fun (k, v) -> k.ToLower(), v)
        |> Map.ofSeq

    let mutable channelToIconMap = Map.empty
    let getIcon (channelId : string) =
        match channelToIconMap |> Map.tryFind (channelId.ToLower()) with
        | Some icon -> icon
        | None -> defaultIcon

    let logMissingChannels channelIds channelMap =
        let missingIds = getMissingKeys channelIds channelMap
        if not <| Seq.isEmpty missingIds then
            Log.warn "Channel(s) not found: %s" (missingIds |> String.concat ", ")
        channelMap

    let getChannelToIconMap (channelIds : string seq) =
        async {
            if Seq.isEmpty channelIds then
                return Ok Map.empty
            else
                let! res = twitchClient.GetUsers(channelIds)
                
                return res
                |> Result.map (
                    Seq.map (fun u -> u.UserName.ToLower(), u.ProfileImageUrl)
                    >> Map.ofSeq
                    >> logMissingChannels channelIds)
        }

    let getStream (stream : StreamInfo) =
        match channelMap |> Map.tryFind (stream.UserName.ToLower()) with
        | Some channel ->
            Some { Platform = Platform.Twitch
                   VtuberIconUrl = stream.UserName |> getIcon
                   VtuberName = channel.Name
                   Url = sprintf "https://www.twitch.tv/%s" stream.UserName
                   ThumbnailUrl = stream.ThumbnailUrl
                     .Replace("{width}", config.Twitch.ThumbnailSize.Width |> string)
                     .Replace("{height}", config.Twitch.ThumbnailSize.Height |> string)
                   Title = stream.Title
                   Viewers = stream.Viewers |> Some
                   StartTime = stream.StartTime
                   Tags = channel.Tags
                   Languages = channel.Languages }
        | None -> None

    let rec streamLoop () =
        async {
            Log.info "Grabbing streams..."
            match!
                twitchClient.GetStreams(twitchChannels)
                with
            | Ok streams ->
                Log.info "Got %d streams" (streams |> Seq.length)
                try
                    do! streams
                        |> Seq.choose getStream
                        |> putPlatformStreams Platform.Twitch
                with
                    err -> Log.exn err "Error storing streams"
            | Error _ -> ()

            do! Async.Sleep <| 60 * 1000
            return! streamLoop ()
        }

    let channelIds = config.Vtubers |> getChannelIds Platform.Twitch
    let rec channelLoop () =
        async {
            do! Async.Sleep (TimeSpan.FromHours(12.).TotalMilliseconds |> int)
            Log.info "Grabbing channels"

            match! channelIds |> getChannelToIconMap with
            | Ok iconMap -> channelToIconMap <- iconMap
            | Error _ -> Log.warn "Skipping channel icon update"
            return! channelLoop ()
        }
    
    // populate the icon map once before grabbing any streams
    while channelToIconMap = Map.empty do
        match channelIds
              |> getChannelToIconMap
              |> Async.RunSynchronously with
        | Ok map -> channelToIconMap <- map
        | Error _ ->
            Log.warn "Error populating channel icons, retrying in 1s..."
            Async.Sleep 1000 |> Async.RunSynchronously

    [streamLoop (); channelLoop ()]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    0 // return an integer exit code
