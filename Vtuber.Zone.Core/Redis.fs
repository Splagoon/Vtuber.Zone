module Vtuber.Zone.Core.Redis

open StackExchange.Redis
open MBrace.FsPickler
open Vtuber.Zone.Core

let secrets = Secrets.Load().Redis
let redis =
    ConnectionMultiplexer.Connect(secrets.Url)

let pickler = FsPickler.CreateBinarySerializer()

let Server = redis.GetEndPoints() |> Seq.exactlyOne |> redis.GetServer
let DB = redis.GetDatabase()

let AllStreamsKey = 
    "vtuber.zone.all-streams" |> RedisKey

let invalidateStreamIndex () =
    async {
        do! DB.KeyDeleteAsync [| AllStreamsKey |]
            |> Async.AwaitTask
            |> Async.Ignore
    }

let putPlatformStreams platform (streams: Stream seq) =
    async {
        let key =
            match platform with
            | Platform.Youtube -> "youtube"
            | Platform.Twitch -> "twitch"
            | _ -> failwithf "unknown stream provider %A" platform
            |> sprintf "vtuber.zone.streams.%s"
            |> RedisKey

        do! DB.KeyDeleteAsync [| key |]
            |> Async.AwaitTask
            |> Async.Ignore

        let redisValues =
            streams
            |> Seq.map (pickler.Pickle >> RedisValue.op_Implicit)
            |> Seq.toArray

        do! DB.SetAddAsync(key, redisValues)
            |> Async.AwaitTask
            |> Async.Ignore

        do! invalidateStreamIndex ()
    }
