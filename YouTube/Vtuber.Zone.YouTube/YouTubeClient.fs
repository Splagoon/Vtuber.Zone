module Vtuber.Zone.YouTube.Client

open System
open Google.Apis.Services
open Google.Apis.YouTube.v3
open Google.Apis.Util
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util

type ChannelInfo = { Id: string; ProfileImageUrl: string }

type StreamInfo =
    { Id: string
      ChannelId: string
      ThumbnailUrl: string
      Title: String
      Viewers: uint64 option
      StartTime: DateTimeOffset }

type StreamResult =
    | LiveStream of StreamInfo
    | UpcomingStream
    | NotStream of string

type IYouTubeClient =
    abstract GetChannels: string seq -> Async<Result<ChannelInfo seq, exn>>
    abstract GetStreams: string seq -> Async<Result<StreamResult seq, exn>>

let private mergeBatchedResults batchedRes =
    let channels, errs =
        batchedRes
        |> Seq.fold (fun (data, errs) res ->
            match res with
            | Ok d -> Seq.append data d, errs
            | Error e -> data, e :: errs) (Seq.empty, List.empty)

    if Seq.isEmpty errs then Ok channels else Error(AggregateException(errs) :> exn)

let private getChannels (youtubeService: YouTubeService) (batchSize: int) (channelIds: string seq) =
    async {
        let! batchedRes =
            channelIds
            |> Seq.chunkBySize batchSize
            |> Seq.mapi (fun batchIdx batchIds ->
                async {
                    try
                        let req =
                            youtubeService.Channels.List([ "snippet" ] |> Repeatable)

                        req.Id <- batchIds |> Repeatable
                        Log.info "Channel batch %d: searching for %d video(s)" (batchIdx + 1) batchIds.Length
                        let! res = req.ExecuteAsync() |> Async.AwaitTask

                        Log.info "Channel batch %d: got %d result(s)" (batchIdx + 1) res.Items.Count

                        return res.Items
                               |> Seq.map (fun channel ->
                                   { Id = channel.Id
                                     ProfileImageUrl = channel.Snippet.Thumbnails.Default__.Url })
                               |> Ok
                    with err ->
                        Log.exn err "Channel batch %d: got error" (batchIdx + 1)
                        return Error err
                })
            |> Async.Parallel

        return batchedRes |> mergeBatchedResults
    }

let private getStreams (youtubeService: YouTubeService) (batchSize: int) (streamIds: string seq) =
    async {
        let! batchedRes =
            streamIds
            |> Seq.chunkBySize batchSize
            |> Seq.mapi (fun batchIdx batchIds ->
                async {
                    try
                        let req =
                            youtubeService.Videos.List
                                ([ "snippet"; "liveStreamingDetails" ]
                                 |> Repeatable)

                        req.Id <- batchIds |> Repeatable
                        Log.info "Video batch %d: searching for %d video(s)" (batchIdx + 1) batchIds.Length
                        let! res = req.ExecuteAsync() |> Async.AwaitTask

                        Log.info "Video batch %d: got %d result(s)" (batchIdx + 1) res.Items.Count

                        return res.Items
                               |> Seq.map (fun stream ->
                                   match stream.Snippet.LiveBroadcastContent with
                                   | "live" ->
                                       LiveStream
                                           { Id = stream.Id
                                             ChannelId = stream.Snippet.ChannelId
                                             ThumbnailUrl = stream.Snippet.Thumbnails.Standard.Url
                                             Title = stream.Snippet.Title
                                             Viewers =
                                                 stream.LiveStreamingDetails.ConcurrentViewers
                                                 |> toOption
                                             StartTime =
                                                 stream.LiveStreamingDetails.ActualStartTime
                                                 |> DateTimeOffset.Parse }
                                   | "upcoming" -> UpcomingStream
                                   | _ -> NotStream stream.Id)
                               |> Ok
                    with err ->
                        Log.exn err "Video batch %d: got error" (batchIdx + 1)
                        return Error err
                })
            |> Async.Parallel

        return batchedRes |> mergeBatchedResults
    }

let getYouTubeClient (secrets: YouTubeSecrets) (batchSize: int) =
    let youtubeService =
        new YouTubeService(BaseClientService.Initializer
                               (ApiKey = secrets.ApiKey, ApplicationName = "vtubers-yt-connector"))

    { new IYouTubeClient with
        member __.GetChannels channelIds =
            getChannels youtubeService batchSize channelIds

        member __.GetStreams streamIds =
            getStreams youtubeService batchSize streamIds }
