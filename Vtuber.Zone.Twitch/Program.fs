open System
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open Vtuber.Zone.Core.Util
open TwitchLib.Api

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitch
    let twitch = TwitchAPI()
    twitch.Settings.ClientId <- secrets.ClientId
    twitch.Settings.Secret <- secrets.ClientSecret

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

    let channelToVtuberMap =
        config.Vtubers
        |> getChannelToVtuberMap Platform.Twitch

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
                return Some Map.empty
            else
                let! res =
                    twitch.Helix.Users.GetUsersAsync(
                        logins = Collections.Generic.List(channelIds))
                    |> Async.AwaitTask
                    |> Async.Catch
                
                match res with
                | Choice1Of2 data ->
                    return data.Users
                    |> Seq.map (fun u -> u.Login.ToLower(), u.ProfileImageUrl)
                    |> Map.ofSeq
                    |> logMissingChannels channelIds
                    |> Some
                | Choice2Of2 err ->
                    Log.exn err "Error fetching channels"
                    return None
        }

    let getStream (stream : Helix.Models.Streams.Stream) =
        let vtubers = channelToVtuberMap.[stream.UserName.ToLower()]
        { Platform = Platform.Twitch
          VtuberIconUrl = stream.UserName |> getIcon
          VtuberName = vtubers |> combineNames
          Url = sprintf "https://www.twitch.tv/%s" stream.UserName
          ThumbnailUrl = stream.ThumbnailUrl
            .Replace("{width}", config.Twitch.ThumbnailSize.Width |> string)
            .Replace("{height}", config.Twitch.ThumbnailSize.Height |> string)
          Title = stream.Title
          Viewers = stream.ViewerCount |> uint64 |> Some
          StartTime = stream.StartedAt |> DateTimeOffset
          Tags = vtubers |> combineTags
          Languages = vtubers |> combineLanguages }

    let rec streamLoop () =
        async {
            Log.info "Grabbing streams..."
            let! res =
                twitch.Helix.Streams.GetStreamsAsync(
                    first = 100,
                    userLogins = twitchChannels)
                |> Async.AwaitTask
                |> Async.Catch

            match res with
            | Choice1Of2 data ->
                Log.info "Got %d streams" data.Streams.Length
                try
                    do! data.Streams
                        |> Seq.map getStream
                        |> putPlatformStreams Platform.Twitch
                with
                    err -> Log.exn err "Error storing streams"
            | Choice2Of2 err ->
                Log.exn err "Error fetching streams"

            do! Async.Sleep <| 60 * 1000
            return! streamLoop ()
        }

    let channelIds = config.Vtubers |> getChannelIds Platform.Twitch
    let rec channelLoop () =
        async {
            do! Async.Sleep (TimeSpan.FromHours(12.).TotalMilliseconds |> int)
            Log.info "Grabbing channels"

            match! channelIds |> getChannelToIconMap with
            | Some iconMap -> channelToIconMap <- iconMap
            | None -> Log.warn "Skipping channel icon update"
            return! channelLoop ()
        }
    
    // populate the icon map once before grabbing any streams
    while channelToIconMap = Map.empty do
        match channelIds
              |> getChannelToIconMap
              |> Async.RunSynchronously with
        | Some map -> channelToIconMap <- map
        | None ->
            Log.warn "Error populating channel icons, retrying in 1s..."
            Async.Sleep 1000 |> Async.RunSynchronously

    [streamLoop (); channelLoop ()]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    0 // return an integer exit code
