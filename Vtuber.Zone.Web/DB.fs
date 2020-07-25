module Vtuber.Zone.Web.DB

open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

let private getAllStreamsScript =
    "Script/GetAllStreams.lua"
    |> ConfigUtils.loadFile
    |> LuaScript.Prepare
    |> fun s -> s.Load(Server)

let private getAllStreams sortKey =
    printfn "getting streams!"
    let key = sprintf "vtuber.zone.all-streams.%s" sortKey
    let keyPattern = sprintf "vtuber.zone.streams.*.%s" sortKey
    let castToByteArrayArray : RedisResult -> byte array array = RedisResult.op_Explicit
    getAllStreamsScript.Evaluate
        (DB,
         {| key = key |> RedisKey
            key_pattern = keyPattern |})
    |> castToByteArrayArray
    |> Seq.map pickler.UnPickle<Stream>

let getAllStreamsByViewers () = getAllStreams "by-viewers"
let getAllStreamsByStartTime () = getAllStreams "by-start-time"
