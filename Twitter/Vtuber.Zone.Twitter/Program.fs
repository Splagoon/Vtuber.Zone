module Vtuber.Zone.Twitter.Main

open System
open System.Text.RegularExpressions
open Tweetinvi
open Tweetinvi.Models
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

let lowercase (str: string) = str.ToLowerInvariant()

// matches youtu.be/xyz and youtube.com/watch?v=xyz links
let private youtubeRegex =
    Regex
        (@"^(?:https?:\/\/)?(?:www\.)?youtu(?:\.be\/|be\.com\/watch\?(?:[^=]+=[^&]+&)*v=)([\w-]+)",
         RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

let parseYouTubeUrl =
    youtubeRegex.Match
    >> function
    | m when m.Success && m.Groups.[1].Success -> Some m.Groups.[1].Value
    | _ -> None

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitter
    Auth.SetUserCredentials
        (secrets.ConsumerKey, secrets.ConsumerSecret, secrets.UserAccessToken, secrets.UserAccessSecret)
    |> ignore

    let readTweet vtuber (tweet: ITweet) =
        if not tweet.IsRetweet then
            let videoIds =
                tweet.Urls
                |> Seq.map (fun u -> u.ExpandedURL)
                |> Seq.choose parseYouTubeUrl

            if not << Seq.isEmpty <| videoIds then
                Log.info "Found YouTube video(s) for %s: %A" vtuber.Name videoIds

                let key =
                    sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id
                    |> RedisKey

                let timestamp =
                    tweet.CreatedAt
                    |> DateTimeOffset
                    |> fun x -> x.ToUnixTimeSeconds() |> float

                let values: SortedSetEntry array =
                    videoIds
                    |> Seq.map (fun id -> SortedSetEntry(id |> RedisValue, timestamp))
                    |> Seq.toArray

                DB.SortedSetAdd(key, values) |> ignore
                // Only keep 5 most recently observed videos
                DB.SortedSetRemoveRangeByRank(key, 0L, -6L)
                |> ignore

    let stream = Stream.CreateFilteredStream()

    let twitterHandles =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle)

    let handleLookup =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle.ToLower(), v)
        |> Map.ofSeq

    let users =
        User.GetUsersFromScreenNames(twitterHandles)
        |> List.ofSeq

    Log.info "Found %d handles" users.Length
    for user in users do
        let vtuber = handleLookup.[user.ScreenName.ToLower()]
        stream.AddFollow(Nullable user.Id, readTweet vtuber)

    let foundHandles =
        users |> Seq.map (fun u -> u.ScreenName.ToLower())

    let missingHandles =
        Set.difference
            (twitterHandles |> Seq.map lowercase |> Set.ofSeq)
            (foundHandles |> Seq.map lowercase |> Set.ofSeq)

    if missingHandles |> Set.isEmpty |> not
    then Log.warn "Handle(s) not found: %s" (missingHandles |> String.concat ", ")

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
