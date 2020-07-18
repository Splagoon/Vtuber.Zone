open System
open System.Text.RegularExpressions
open System.Threading
open Tweetinvi
open Tweetinvi.Models
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

[<EntryPoint>]
let main _ =
    let config = Config.Load()
    let secrets = Secrets.Load().Twitter
    let creds =
        Auth.SetUserCredentials(
            secrets.ConsumerKey,
            secrets.ConsumerSecret,
            secrets.UserAccessToken,
            secrets.UserAccessSecret)
    // matches youtu.be/xyz and youtube.com/watch?v=xyz links
    let youtubeRegex = Regex(@"^(?:https?:\/\/)?(?:www\.)?youtu(?:\.be\/|be\.com\/watch\?v=)(\w+)", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let readTweet vtuber (tweet : ITweet) =
        if not tweet.Retweeted then
            let videoIds =
                seq { for url in tweet.Urls -> youtubeRegex.Match(url.ExpandedURL, 0) }
                |> Seq.filter (fun m -> m.Success && m.Groups.[1].Success)
                |> Seq.map (fun m -> m.Groups.[1].Value)
            if not << Seq.isEmpty <| videoIds then
                printfn "Found YouTube videos for %s: %A" vtuber.Name videoIds
                let key = sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id |> RedisKey
                let timestamp = tweet.CreatedAt |> DateTimeOffset |> fun x -> x.ToUnixTimeSeconds() |> float
                let values : SortedSetEntry array =
                    videoIds
                    |> Seq.map (fun id -> SortedSetEntry(id |> RedisValue, timestamp))
                    |> Seq.toArray
                DB.SortedSetAdd(key, values) |> ignore
                // Only keep 10 most recently observed videos
                DB.SortedSetRemoveRangeByRank(key, -10L, 0L) |> ignore

    let stream = Stream.CreateFilteredStream(creds)

    for vtuber in config.Vtubers do
        printfn "Following %s (@%s)" vtuber.Name vtuber.TwitterHandle
        stream.AddFollow(vtuber.TwitterHandle |> UserIdentifier, readTweet vtuber)
    stream.StartStreamMatchingAnyCondition()

    printfn "Sleeping forever"
    Thread.Sleep(Timeout.Infinite)
    0 // return an integer exit code
