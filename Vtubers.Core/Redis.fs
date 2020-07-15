module Vtubers.Core.Redis

open StackExchange.Redis
open MBrace.FsPickler
open Vtubers.Core.Util

let redis = ConnectionMultiplexer.Connect("127.0.0.1:6379")
let pickler = FsPickler.CreateBinarySerializer()

let DB = redis.GetDatabase()

module public Stream =
    let key : RedisKey = ~~"vtubers.streams"
    let put streams =
        let value : RedisValue array =
            streams
            |> Seq.map (pickler.Pickle >> (~~))
            |> Seq.toArray
        DB.SetAdd(key, value) |> ignore
