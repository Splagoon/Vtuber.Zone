module Vtuber.Zone.Twitter.Client

open System
open CoreTweet
open CoreTweet.Streaming
open Vtuber.Zone.Core

type UserInfo = { Handle: string; Id: int64 }

type TweetInfo =
    { AuthorHandle: string
      Urls: string seq
      Timestamp: DateTimeOffset }

type ITwitterClient =
    abstract GetUsers: string seq -> Async<Result<UserInfo seq, exn>>
    abstract GetTweets: int64 seq -> TweetInfo seq

let private mergeBatchedResults batchedRes =
    let users, errs =
        batchedRes
        |> Seq.fold (fun (data, errs) res ->
            match res with
            | Ok d -> Seq.append data d, errs
            | Error e -> data, e :: errs) (Seq.empty, List.empty)

    if Seq.isEmpty errs then Ok users else Result.Error(AggregateException(errs) :> exn)

let private getUsers (tokens: Tokens) (batchSize: int) (handles: string seq) =
    async {
        let! batchedRes =
            handles
            |> Seq.chunkBySize batchSize
            |> Seq.mapi (fun batchIdx batchHandles ->
                async {
                    try
                        Log.info "User batch %d: searching for %d handle(s)" (batchIdx + 1) batchHandles.Length
                        let! users =
                            tokens.Users.LookupAsync(batchHandles)
                            |> Async.AwaitTask
                        Log.info "User batch %d: got %d result(s)" (batchIdx + 1) users.Count

                        return users
                               |> Seq.map (fun u ->
                                   { Handle = u.ScreenName
                                     Id = u.Id.Value })
                               |> Ok
                    with err ->
                        Log.exn err "User batch %d: got error" (batchIdx + 1)
                        return Result.Error err
                })
            |> Async.Sequential // CoreTweet cannot handle parallel requests

        return batchedRes |> mergeBatchedResults
    }

let private getTweets (tokens: Tokens) userIds =
    tokens.Streaming.Filter(follow = userIds)
    |> Seq.choose (fun msg ->
        match msg with
        | :? StatusMessage as statusMsg when
                isNull statusMsg.Status.RetweetedStatus
                && not statusMsg.Status.InReplyToStatusId.HasValue
                && not statusMsg.Status.InReplyToUserId.HasValue ->
            Some
                { AuthorHandle = statusMsg.Status.User.ScreenName
                  Urls =
                      statusMsg.Status.Entities.Urls
                      |> Seq.map (fun u -> u.ExpandedUrl)
                  Timestamp = statusMsg.Status.CreatedAt }
        | _ -> None)

let getTwitterClient (secrets: TwitterSecrets) (batchSize: int) =
    let tokens =
        Tokens
            (ConsumerKey = secrets.ConsumerKey,
             ConsumerSecret = secrets.ConsumerSecret,
             AccessToken = secrets.UserAccessToken,
             AccessTokenSecret = secrets.UserAccessSecret)

    { new ITwitterClient with
        member __.GetUsers handles = getUsers tokens batchSize handles
        member __.GetTweets userIds = getTweets tokens userIds }
