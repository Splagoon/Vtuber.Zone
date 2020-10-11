module Vtuber.Zone.Twitter.Client

open System
open CoreTweet
open CoreTweet.V2
open FSharp.Control
open Vtuber.Zone.Core

type UserInfo = { Handle: string; Id: int64 }

type TweetInfo =
    { AuthorHandle: string
      Urls: string seq
      Timestamp: DateTimeOffset }

type ITwitterClient =
    abstract GetUsers: string seq -> Async<Result<UserInfo seq, exn>>
    abstract GetTweets: string seq -> Async<Result<AsyncSeq<TweetInfo>, exn>>

let private mergeBatchedResults batchedRes =
    let users, errs =
        batchedRes
        |> Seq.fold (fun (data, errs) res ->
            match res with
            | Ok d -> Seq.append data d, errs
            | Error e -> data, e :: errs) (Seq.empty, List.empty)

    if Seq.isEmpty errs
    then Ok users
    else Result.Error(AggregateException(errs) :> exn)

let private getUsers (tokens: OAuth2Token) (batchSize: int) (handles: string seq) =
    async {
        let! batchedRes =
            handles
            |> Seq.chunkBySize batchSize
            |> Seq.mapi (fun batchIdx batchHandles ->
                async {
                    try
                        Log.info "User batch %d: searching for %d handle(s)" (batchIdx + 1) batchHandles.Length
                        let! users =
                            tokens.V2.UserLookupApi.GetUsersByUsernamesAsync
                                (batchHandles, user_fields = Nullable(UserFields.Id ||| UserFields.Username))
                            |> Async.AwaitTask

                        Log.info "User batch %d: got %d result(s)" (batchIdx + 1) users.Data.Length

                        return users.Data
                               |> Seq.map (fun u -> { Handle = u.Username; Id = u.Id })
                               |> Ok
                    with err ->
                        Log.exn err "User batch %d: got error" (batchIdx + 1)
                        return Result.Error err
                })
            |> Async.Sequential // CoreTweet cannot handle parallel requests

        return batchedRes |> mergeBatchedResults
    }

let private getTweets (tokens: OAuth2Token) handles =
    async {
        try
            // Clear out any existing rules
            let! existingFilters =
                tokens.V2.FilteredStreamApi.GetRulesAsync()
                |> Async.AwaitTask

            if existingFilters.Data.Length > 0 then
                Log.info "Deleting %d filter(s)..." existingFilters.Data.Length

                let filterIds =
                    existingFilters.Data
                    |> Array.map (fun it -> it.Id)

                do! tokens.V2.FilteredStreamApi.DeleteRulesAsync(ids = filterIds)
                    |> Async.AwaitTask
                    |> Async.Ignore

            // TODO: we can more efficiently pack usernames with Seq.takeWhile
            let filters =
                handles
                |> Seq.chunkBySize 15
                |> Seq.map
                    (Array.map (sprintf "from:%s")
                     >> String.concat " OR "
                     >> sprintf "has:links -is:retweet -is:quote -is:reply (%s)"
                     >> fun s -> FilterRule(Value = s))

            Log.info "Creating %d filter(s)..." (Seq.length filters)
            do! tokens.V2.FilteredStreamApi.CreateRulesAsync(add = filters)
                |> Async.AwaitTask
                |> Async.Ignore

            let stream =
                tokens.V2.FilteredStreamApi.Filter
                    (expansions = Nullable(TweetExpansions.AuthorId),
                     user_fields = Nullable(UserFields.Username),
                     tweet_fields =
                         Nullable
                             (TweetFields.AuthorId
                              ||| TweetFields.Entities
                              ||| TweetFields.ReferencedTweets
                              ||| TweetFields.CreatedAt))

            return stream.StreamAsAsyncEnumerable(Threading.CancellationToken.None)
                   |> AsyncSeq.ofAsyncEnum
                   |> AsyncSeq.choose (fun tweet ->
                       tweet.Includes.Users
                       |> Seq.tryFind (fun user -> user.Id = tweet.Data.AuthorId.Value)
                       |> Option.map (fun author ->
                           { AuthorHandle = author.Username
                             Urls =
                                 tweet.Data.Entities.Urls
                                 |> Seq.map (fun url -> url.ExpandedUrl)
                             Timestamp = tweet.Data.CreatedAt.Value }))
                   |> Ok
        with err ->
            Log.exn err "Error connecting to filtered Tweet stream"
            return Result.Error err
    }

let getTwitterClient (secrets: TwitterSecrets) (batchSize: int) =
    let token =
        OAuth2Token
            (ConsumerKey = secrets.ConsumerKey,
             ConsumerSecret = secrets.ConsumerSecret,
             BearerToken = secrets.BearerToken)

    { new ITwitterClient with
        member __.GetUsers handles = getUsers token batchSize handles
        member __.GetTweets handles = getTweets token handles }
