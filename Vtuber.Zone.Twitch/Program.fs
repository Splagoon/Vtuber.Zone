open System
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
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

    let getStream (stream : Helix.Models.Streams.Stream) =
        { Channel = { Platform = Platform.Twitch 
                      Id = stream.UserName }
          Url = sprintf "https://www.twitch.tv/%s" stream.UserName
          ThumbnailUrl = stream.ThumbnailUrl
          Title = stream.Title
          Viewers = stream.ViewerCount |> uint64 |> Some
          StartTime = stream.StartedAt |> DateTimeOffset }

    let rec loop () =
        async {
            let! streams =
                twitch.Helix.Streams.GetStreamsAsync(
                    first = 100,
                    userLogins = twitchChannels)
                |> Async.AwaitTask

            streams.Streams
            |> Seq.map getStream
            |> putPlatformStreams Platform.Twitch
            do! Async.Sleep <| 60 * 1000
        } |> Async.RunSynchronously
        loop()
    loop()
    0 // return an integer exit code
