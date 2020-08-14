﻿open System
open System.Text.RegularExpressions
open Tweetinvi
open Tweetinvi.Models
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitter
    Auth.SetUserCredentials(
        secrets.ConsumerKey,
        secrets.ConsumerSecret,
        secrets.UserAccessToken,
        secrets.UserAccessSecret) |> ignore

    // matches youtu.be/xyz and youtube.com/watch?v=xyz links
    let youtubeRegex = Regex(@"^(?:https?:\/\/)?(?:www\.)?youtu(?:\.be\/|be\.com\/watch\?v=)([\w-]+)", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let readTweet vtuber (tweet : ITweet) =
        if not tweet.IsRetweet then
            let videoIds =
                seq { for url in tweet.Urls -> youtubeRegex.Match(url.ExpandedURL, 0) }
                |> Seq.filter (fun m -> m.Success && m.Groups.[1].Success)
                |> Seq.map (fun m -> m.Groups.[1].Value)
            if not << Seq.isEmpty <| videoIds then
                Log.info "Found YouTube video(s) for %s: %A" vtuber.Name videoIds
                let key = sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id |> RedisKey
                let timestamp = tweet.CreatedAt |> DateTimeOffset |> fun x -> x.ToUnixTimeSeconds() |> float
                let values : SortedSetEntry array =
                    videoIds
                    |> Seq.map (fun id -> SortedSetEntry(id |> RedisValue, timestamp))
                    |> Seq.toArray
                DB.SortedSetAdd(key, values) |> ignore
                // Only keep 5 most recently observed videos
                DB.SortedSetRemoveRangeByRank(key, 0L, -6L) |> ignore

    let stream = Stream.CreateFilteredStream()

    let twitterHandles =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle)
    let handleLookup =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle.ToLower(), v)
        |> Map.ofSeq

    for user in User.GetUsersFromScreenNames(twitterHandles) do
        let vtuber = handleLookup.[user.ScreenName.ToLower()]
        stream.AddFollow(Nullable user.Id, readTweet vtuber)
        Log.info "Following %s (@%s): %d" vtuber.Name user.ScreenName user.Id

    Log.info "Now listening for tweets..."
    let rec loop () =
        async {
            do! stream.StartStreamMatchingAnyConditionAsync()
                |> Async.AwaitTask
            Log.warn "Tweet stream stopped, restarting..."
            return! loop ()
        }
    loop () |> Async.RunSynchronously // does not return
    0 // return an integer exit code
