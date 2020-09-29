module Vtuber.Zone.Twitter.Redis

open System
open Vtuber.Zone.Core
open StackExchange.Redis

type IRedisClient =
    abstract PutFoundVideoIds: Vtuber -> string seq -> DateTimeOffset -> Async<Result<unit, exn>>

let private putFoundVideoIds (db: IDatabaseAsync)
                             (vtuber: Vtuber)
                             (foundVideoIds: string seq)
                             (timestamp: DateTimeOffset)
                             =
    let key =
        sprintf "vtuber.zone.twitter-yt-links.%s" vtuber.Id
        |> RedisKey

    let timestamp = timestamp.ToUnixTimeSeconds() |> float

    let values: SortedSetEntry array =
        foundVideoIds
        |> Seq.map (fun id -> SortedSetEntry(RedisValue id, timestamp))
        |> Seq.toArray

    async {
        try
            do! db.SortedSetAddAsync(key, values)
                |> Async.AwaitTask
                |> Async.Ignore
            // Only keep 5 most recently observed videos
            do! db.SortedSetRemoveRangeByRankAsync(key, 0L, -6L)
                |> Async.AwaitTask
                |> Async.Ignore
            return Ok()
        with err ->
            Log.exn err "Error adding found video IDs for %s" vtuber.Id
            return Error err
    }

let getRedisClient (secrets: RedisSecrets) =
    async {
        try
            let! conn =
                ConnectionMultiplexer.ConnectAsync(secrets.Url)
                |> Async.AwaitTask

            let db = conn.GetDatabase()

            return { new IRedisClient with
                         member __.PutFoundVideoIds vtuber foundVideoIds timestamp =
                             putFoundVideoIds db vtuber foundVideoIds timestamp }
                   |> Ok
        with err ->
            Log.exn err "Error connecting to Redis"
            return Error err
    }
