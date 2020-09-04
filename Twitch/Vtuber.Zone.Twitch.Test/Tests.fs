module Tests

open System
open Vtuber.Zone.Core
open Vtuber.Zone.Twitch.Client
open Vtuber.Zone.Twitch.Main
open Xunit
open FsUnit.Xunit

type MockTwitchClient =
    { Users: UserInfo seq
      Streams: StreamInfo seq }
    interface ITwitchClient with
        member this.GetUsers _ = async { return Ok this.Users }
        member this.GetStreams _ = async { return Ok this.Streams }

type ErrorTwitchClient =
    { Error: exn }
    interface ITwitchClient with
        member this.GetUsers _ = async { return Error this.Error }
        member this.GetStreams _ = async { return Error this.Error }

let emptyStream =
    { UserName = ""
      ThumbnailUrl = ""
      Title = ""
      Viewers = 0uL
      StartTime = DateTimeOffset.UnixEpoch }

let emptyVtuber =
    { Name = ""
      Channels = List.empty
      TwitterHandle = ""
      Tags = List.empty
      Languages = List.empty }

[<Fact>]
let ``Stream should use Vtuber's channel icon`` () =
    let twitchClient =
        { Users =
              [ { UserName = "coolstreamer"
                  ProfileImageUrl = "coolimage" } ]
          Streams =
              [ { emptyStream with
                      UserName = "coolstreamer" } ] }

    let vtubers =
        [ { emptyVtuber with
                Channels =
                    [ { Platform = Platform.Twitch
                        Id = "coolstreamer"
                        Name = None } ] } ]

    async {
        match! vtubers |> getChannelMap twitchClient with
        | Ok channelMap ->
            match! twitchClient |> getStreams channelMap (0, 0) with
            | Ok streams ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberIconUrl |> should equal "coolimage"
            | Error err -> raise err
        | Error err -> raise err
    }
    |> Async.RunSynchronously
