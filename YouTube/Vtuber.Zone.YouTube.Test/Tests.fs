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

type MockRedisClient =
    { FoundVideoIds: string array }
    interface IRedisClient with
        member this.GetFoundVideoIds() = async { return Ok this.FoundVideoIds }

        member __.PutBadIds _ =
            async { return Ok() }

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
              [ LiveStream
                  { emptyStream with
                        ChannelId = "abc123" } ] }

    let redisClient =
        { FoundVideoIds = Array.empty }

    let vtubers =
        [ { emptyVtuber with
                Channels = [ youtubeChannel "abc123" ] } ]

    async {
        match! vtubers |> getChannelMap youtubeClient with
        | Ok channelMap ->
            match! getStreams channelMap youtubeClient redisClient with
            | Ok (streams, _) ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberIconUrl |> should equal "coolimage"
            | Error err -> raise err
        | Error err -> raise err
    }
    |> Async.RunSynchronously

[<Fact>]
let ``getChannelMap should ignore missing channels`` () =
    let youtubeClient =
        { Channels =
              [ { Id = "streamer2"
                  ProfileImageUrl = "image2" } ]
          Streams = Seq.empty }

    let vtubers =
        [ { emptyVtuber with
                Channels = [ youtubeChannel "streamer1" ] }
          { emptyVtuber with
                Channels = [ youtubeChannel "streamer2" ] } ]

    async {
        match! vtubers |> getChannelMap youtubeClient with
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
    let youtubeClient =
        { Channels =
              [ { Id = "userB"
                  ProfileImageUrl = "imageB" } ]
          Streams =
              [ LiveStream
                  { emptyStream with
                        Id = "streamA"
                        ChannelId = "userA" }
                LiveStream
                    { emptyStream with
                          Id = "streamB"
                          ChannelId = "userB" } ] }

    let redisClient =
        { FoundVideoIds = [| "streamA"; "streamB" |] }

    let vtubers =
        [ { emptyVtuber with
                Name = "Vtuber A"
                Channels = [ youtubeChannel "userA" ] }
          { emptyVtuber with
                Name = "Vtuber B"
                Channels = [ youtubeChannel "userB" ] } ]

    async {
        match! vtubers |> getChannelMap youtubeClient with
        | Ok channelMap ->
            match! getStreams channelMap youtubeClient redisClient with
            | Ok (streams, _) ->
                let stream = streams |> Seq.exactlyOne
                stream.VtuberName |> should equal "Vtuber B"
            | Error err -> raise err
        | Error err -> raise err
    }
    |> Async.RunSynchronously

[<Fact>]
let ``Non-live streams should be added to bad ID set`` () =
    let youtubeClient =
        { Channels =
              [ { Id = "cooltuber"
                  ProfileImageUrl = "coolimage" } ]
          Streams = [ NotStream "vod" ] }

    let redisClient =
        { FoundVideoIds = [| "vod" |] }

    let vtubers =
        [ { emptyVtuber with
                Channels = [ youtubeChannel "cooltuber" ] } ]

    async {
        match! vtubers |> getChannelMap youtubeClient with
        | Ok channelMap ->
            match! getStreams channelMap youtubeClient redisClient with
            | Ok (streams, badIds) ->
                streams |> should be Empty
                let badId = badIds |> Seq.exactlyOne
                badId |> should equal "vod"
            | Error err -> raise err
        | Error err -> raise err
    }
    |> Async.RunSynchronously
