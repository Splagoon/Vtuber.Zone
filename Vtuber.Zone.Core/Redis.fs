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
    DB.KeyDelete [| AllStreamsKey |]
    |> ignore

let putPlatformStreams platform (streams: Stream seq) =
    let key =
        match platform with
        | Platform.Youtube -> "youtube"
        | Platform.Twitch -> "twitch"
        | _ -> failwithf "unknown stream provider %A" platform
        |> sprintf "vtuber.zone.streams.%s"
        |> RedisKey

    DB.KeyDelete[| key |]
    |> ignore

    let redisValues =
        streams
        |> Seq.map (pickler.Pickle >> RedisValue.op_Implicit)
        |> Seq.toArray

    DB.SetAdd(key, redisValues)
    |> ignore
    invalidateStreamIndex ()
