module Vtuber.Zone.WebAPI.DB

open Vtuber.Zone.Core
open Vtuber.Zone.Core.Redis
open StackExchange.Redis

let private getAllStreamsScript =
    "Script/GetAllStreams.lua"
    |> ConfigUtils.loadFile
    |> LuaScript.Prepare
    |> fun s -> s.Load(Server)

let getAllStreams () =
    Log.info "getting streams!"
    let key = "vtuber.zone.all-streams" |> RedisKey
    let keyPattern = "vtuber.zone.streams.*"
    let castToByteArrayArray : RedisResult -> byte array array = RedisResult.op_Explicit
    getAllStreamsScript.Evaluate
        (DB,
         {| key = key
            key_pattern = keyPattern |})
    |> castToByteArrayArray
    |> Seq.map pickler.UnPickle<Stream>
