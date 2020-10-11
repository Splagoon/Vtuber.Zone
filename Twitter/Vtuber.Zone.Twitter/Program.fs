module Vtuber.Zone.Twitter.Main

open FSharp.Control
open System.Text.RegularExpressions
open Vtuber.Zone.Core
open Vtuber.Zone.Twitter.Client
open Vtuber.Zone.Twitter.Redis

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
    let secrets = Secrets.Load()

    let twitterClient =
        getTwitterClient secrets.Twitter config.Twitter.BatchSize

    let redisClient =
        getRedisClient secrets.Redis
        |> Async.RunSynchronously
        |> function
        | Ok client -> client
        | Error err ->
            Log.exn err "Error instantiating Redis client"
            exit 1

    let handleLookup =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle.ToLower(), v)
        |> Map.ofSeq

    let readTweet (tweet: TweetInfo) =
        match handleLookup
              |> Map.tryFind (tweet.AuthorHandle.ToLower()) with
        | Some vtuber ->
            let videoIds = tweet.Urls |> Seq.choose parseYouTubeUrl

            if not << Seq.isEmpty <| videoIds then
                Log.info "Found YouTube video(s) for %s: %A" vtuber.Name videoIds

                let res =
                    redisClient.PutFoundVideoIds vtuber videoIds tweet.Timestamp
                    |> Async.RunSynchronously

                match res with
                | Error err -> Log.exn err "Error storing found video IDs to Redis"
                | _ -> ()
        | _ -> Log.warn "Got Tweet from unknown account: %s" tweet.AuthorHandle

    let twitterHandles =
        config.Vtubers
        |> Seq.map (fun v -> v.TwitterHandle)

    let users =
        twitterClient.GetUsers twitterHandles
        |> Async.RunSynchronously
        |> function
        | Ok users -> List.ofSeq users
        | Error err ->
            Log.exn err "Error fetching user IDs"
            exit 2

    let foundHandles =
        users |> Seq.map (fun u -> u.Handle.ToLower())

    let missingHandles =
        Set.difference
            (twitterHandles |> Seq.map lowercase |> Set.ofSeq)
            (foundHandles |> Seq.map lowercase |> Set.ofSeq)

    if missingHandles |> Set.isEmpty |> not
    then Log.warn "Handle(s) not found: %s" (missingHandles |> String.concat ", ")

    let tweetStream =
        twitterClient.GetTweets twitterHandles
        |> Async.RunSynchronously
        |> function
        | Ok stream -> stream
        | Error _ -> exit 2

    Log.info "Now listening for tweets..."

    let rec loop () =
        async {
            try
                do! tweetStream |> AsyncSeq.iter readTweet
                Log.warn "Tweet stream stopped, restarting..."
            with err -> Log.exn err "Got error reading Tweet stream, continuing..."
            return! loop ()
        }

    loop () |> Async.RunSynchronously // does not return
    0 // return an integer exit code
