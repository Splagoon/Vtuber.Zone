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
                printfn "Following %s (%s)" v.Name c.Id
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

    let getChannelToIconMap (channelIds : string seq) =
        async {
            if Seq.isEmpty channelIds then
                return Map.empty
            else
                let! res =
                    twitch.Helix.Users.GetUsersAsync(
                        logins = Collections.Generic.List(channelIds))
                    |> Async.AwaitTask

                return res.Users
                |> Seq.map (fun u -> u.Login.ToLower(), u.ProfileImageUrl)
                |> Map.ofSeq
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
            printfn "Grabbing streams"
            let! streams =
                twitch.Helix.Streams.GetStreamsAsync(
                    first = 100,
                    userLogins = twitchChannels)
                |> Async.AwaitTask

            streams.Streams
            |> Seq.map getStream
            |> putPlatformStreams Platform.Twitch
            do! Async.Sleep <| 60 * 1000
            return! streamLoop ()
        }

    let channelIds = config.Vtubers |> getChannelIds Platform.Twitch
    let rec channelLoop () =
        async {
            do! Async.Sleep (TimeSpan.FromHours(12.).TotalMilliseconds |> int)
            printfn "Grabbing channels"
            let! iconMap = channelIds |> getChannelToIconMap
            channelToIconMap <- iconMap
            return! channelLoop ()
        }
    
    // populate the icon map once before grabbing any streams
    channelToIconMap <- channelIds
                        |> getChannelToIconMap
                        |> Async.RunSynchronously
    [streamLoop (); channelLoop ()]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    0 // return an integer exit code
