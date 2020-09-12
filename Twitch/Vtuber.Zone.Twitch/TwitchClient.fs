module Vtuber.Zone.Twitch.Client

open System
open TwitchLib.Api
open Vtuber.Zone.Core

type UserInfo =
    { UserName: string
      ProfileImageUrl: string }

type StreamInfo =
    { UserName: string
      ThumbnailUrl: string
      Title: string
      Viewers: uint64
      StartTime: DateTimeOffset }

type ITwitchClient =
    abstract GetUsers: string seq -> Async<Result<UserInfo seq, exn>>
    abstract GetStreams: string seq -> Async<Result<StreamInfo seq, exn>>

let private getUsers (twitchApi: TwitchAPI) (userNames: string seq) =
    async {
        try
            let! res =
                twitchApi.Helix.Users.GetUsersAsync(logins = Collections.Generic.List(userNames))
                |> Async.AwaitTask

            return res.Users
                   |> Seq.map (fun u ->
                       { UserName = u.Login
                         ProfileImageUrl = u.ProfileImageUrl })
                   |> Ok
        with err ->
            Log.exn err "caught exception"
            return Error err
    }

let private getStreams (twitchApi: TwitchAPI) (userNames: string seq) =
    async {
        try
            let! res =
                twitchApi.Helix.Streams.GetStreamsAsync(first = 100, userLogins = Collections.Generic.List(userNames))
                |> Async.AwaitTask

            return res.Streams
                   |> Seq.map (fun s ->
                       { UserName = s.UserName
                         ThumbnailUrl = s.ThumbnailUrl
                         Title = s.Title
                         Viewers = s.ViewerCount |> uint64
                         StartTime = s.StartedAt |> DateTimeOffset })
                   |> Ok
        with err ->
            Log.exn err "Error fetching streams: %s" (userNames |> String.concat ", ")
            return Error err
    }

let getTwitchClient (secrets: TwitchSecrets) =
    let twitchApi = TwitchAPI()
    twitchApi.Settings.ClientId <- secrets.ClientId
    twitchApi.Settings.Secret <- secrets.ClientSecret

    { new ITwitchClient with
        member __.GetUsers userNames = getUsers twitchApi userNames
        member __.GetStreams userNames = getStreams twitchApi userNames }
