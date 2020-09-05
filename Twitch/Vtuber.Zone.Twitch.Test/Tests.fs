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

let twitchChannel userName =
    { Platform = Platform.Twitch
      Id = userName
      Name = None }

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
                Channels = [ twitchChannel "coolstreamer" ] } ]

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

[<Fact>]
let ``getChannelMap should ignore missing channels`` () =
    let twitchClient =
        { Users =
              [ { UserName = "streamer2"
                  ProfileImageUrl = "image2" } ]
          Streams = Seq.empty }

    let vtubers =
        [ { emptyVtuber with
                Channels = [ twitchChannel "streamer1" ] }
          { emptyVtuber with
                Channels = [ twitchChannel "streamer2" ] } ]

    async {
        match! vtubers |> getChannelMap twitchClient with
        | Ok channelMap ->
            channelMap
            |> Map.tryFind "streamer1"
            |> Option.isNone
            |> should be True
            channelMap
            |> Map.tryFind "streamer2"
            |> Option.isSome
            |> should be True
        | Error err -> raise err
    }
    |> Async.RunSynchronously

[<Fact>]
let ``getStream should ignore missing channels`` () =
    let twitchClient =
        { Users =
            [ { UserName = "userB"
                ProfileImageUrl = "imageB" } ]
          Streams =
            [ { emptyStream with UserName = "userA" }
              { emptyStream with UserName = "userB" } ] }

    let vtubers =
        [ { emptyVtuber with
                Name = "Vtuber A"
                Channels = [ twitchChannel "userA" ] }
          { emptyVtuber with
                Name = "Vtuber B"
                Channels = [ twitchChannel "userB" ] } ]

    async {
        match! vtubers |> getChannelMap twitchClient with
        | Ok channelMap ->
            match! twitchClient |> getStreams channelMap (0, 0) with
            | Ok streams ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberName |> should equal "Vtuber B"
            | Error err -> raise err
        | Error err -> raise err
    } |> Async.RunSynchronously

[<Fact>]
let ``Usernames should be case insensitive`` () =
    let twitchClient =
        { Users =
            [ { UserName = "vTuber"
                ProfileImageUrl = "image" } ]
          Streams =
            [ { emptyStream with UserName = "Vtuber" } ] }

    let vtubers =
        [ { emptyVtuber with
                Name = "The Vtuber"
                Channels = [ twitchChannel "vtuber" ] } ]

    async {
        match! vtubers |> getChannelMap twitchClient with
        | Ok channelMap ->
            match! twitchClient |> getStreams channelMap (0, 0) with
            | Ok streams ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberName |> should equal "The Vtuber"
                stream.VtuberIconUrl |> should equal "image"
            | Error err -> raise err
        | Error err -> raise err
    } |> Async.RunSynchronously
