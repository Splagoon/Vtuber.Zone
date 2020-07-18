module Vtuber.Zone.Core.Redis

open StackExchange.Redis
open MBrace.FsPickler
open Vtuber.Zone.Core.Util
open Vtuber.Zone.Core

let secrets = Secrets.Load().Redis
let redis =
    ConnectionMultiplexer.Connect(secrets.Url)

let pickler = FsPickler.CreateBinarySerializer()

let DB = redis.GetDatabase()

let AllStreamsByViewersKey: RedisKey = ~~ "vtuber.zone.all-streams.by-viewers"

let AllStreamsByStartTimeKey: RedisKey =
    ~~ "vtuber.zone.all-streams.by-start-time"

let invalidateStreamIndexes () =
    DB.KeyDelete
        ([| AllStreamsByViewersKey
            AllStreamsByStartTimeKey |])
    |> ignore

let putPlatformStreams platform (streams: Stream seq) =
    let viewersKey, startTimeKey: RedisKey * RedisKey =
        match platform with
        | Platform.Youtube -> "youtube"
        | Platform.Twitch -> "twitch"
        | _ -> failwithf "unknown stream provider %A" platform
        |> fun p ->
            p
            |> sprintf "vtuber.zone.streams.%s.by-viewers"
            |> (~~),
            p
            |> sprintf "vtuber.zone.streams.%s.by-start-time"
            |> (~~)

    DB.KeyDelete([| viewersKey; startTimeKey |])
    |> ignore

    let streamsByViewers, streamsByStartTime =
        seq {
            for stream in streams ->
                let value: RedisValue = stream |> pickler.Pickle |> (~~)
                SortedSetEntry(value, stream.Viewers |> float),
                SortedSetEntry(value, stream.StartTime.ToUnixTimeSeconds() |> float)
        }
        |> Seq.toArray
        |> Array.unzip

    DB.SortedSetAdd(viewersKey, streamsByViewers)
    |> ignore
    DB.SortedSetAdd(startTimeKey, streamsByStartTime)
    |> ignore
