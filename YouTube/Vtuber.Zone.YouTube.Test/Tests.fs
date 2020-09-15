module Tests

open System
open Vtuber.Zone.Core
open Vtuber.Zone.YouTube.Client
open Vtuber.Zone.YouTube.Redis
open Vtuber.Zone.YouTube.Main
open Xunit
open FsUnit.Xunit

type MockYouTubeClient =
    { Channels: ChannelInfo seq
      Streams: StreamResult seq }
    interface IYouTubeClient with
        member this.GetChannels _ = async { return Ok this.Channels }
        member this.GetStreams _ = async { return Ok this.Streams }

let emptyStream =
    { Id = ""
      ChannelId = ""
      ThumbnailUrl = ""
      Title = ""
      Viewers = None
      StartTime = DateTimeOffset.UnixEpoch }

let emptyVtuber =
    { Name = ""
      Channels = List.empty
      TwitterHandle = ""
      Tags = List.empty
      Languages = List.empty }

let youtubeChannel id =
    { Platform = Platform.Youtube
      Id = id
      Name = None }

[<Fact>]
let ``Stream should use Vtuber's channel icon`` () =
    let youtubeClient =
        { Channels =
            [ { Id = "abc123"
                ProfileImageUrl = "coolimage" } ]
          Streams =
            [ LiveStream { emptyStream with
                            ChannelId = "abc123" } ] }

    let vtubers =
        [ { emptyVtuber with
                Channels = [ youtubeChannel "abc123" ] } ]

    async {
        match! vtubers |> getChannelMap youtubeClient with
        | Ok channelMap ->
            match! youtubeClient |> getStreams channelMap with
            | Ok (streams, _) ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberIconUrl |> should equal "coolimage"
            | Error err -> raise err
        | Error err -> raise err
    }
    |> Async.RunSynchronously
