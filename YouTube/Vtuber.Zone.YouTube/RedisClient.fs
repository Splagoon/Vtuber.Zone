module Vtuber.Zone.YouTube.Redis

open Vtuber.Zone.Core
open StackExchange.Redis

type IRedisClient =
    abstract GetFoundVideoIds: unit -> Async<Result<string array, exn>>
    abstract PutBadIds: string seq -> Async<Result<unit, exn>>

let private badIdsKey =
    "vtuber.zone.youtube-bad-ids" |> RedisKey

let private getFoundVideoIds (db: IDatabaseAsync) (script: LoadedLuaScript) =
    let tmpKey =
        "vtuber.zone.yt-found-ids-tmp" |> RedisKey

    let foundIdsKeyPattern = "vtuber.zone.twitter-yt-links.*"
    let castToStringArray: RedisResult -> string array = RedisResult.op_Explicit

    async {
        try
            let! res =
                script.EvaluateAsync
                    (db,
                     {| tmp_key = tmpKey
                        bad_ids_key = badIdsKey
                        found_ids_key_pattern = foundIdsKeyPattern |})
                |> Async.AwaitTask

            return res |> castToStringArray |> Ok
        with err -> return Error err
    }

let private putBadIds (db: IDatabaseAsync) (videoIds: string seq) =
    async {
        try
            let! setSize =
                db.SetAddAsync(badIdsKey, videoIds |> Seq.map RedisValue |> Seq.toArray)
                |> Async.AwaitTask

            if setSize > 1000L then
                Log.info "Bad ID set grew to %d elements, popping" setSize
                do! db.SetPopAsync(badIdsKey, 200L)
                    |> Async.AwaitTask
                    |> Async.Ignore
                return Ok()
            else
                return Ok()
        with err ->
            Log.exn err "Error adding to bad ID set"
            return Error err
    }

let getRedisClient (secrets: RedisSecrets) =
    async {
        try
            let! conn =
                ConnectionMultiplexer.ConnectAsync(secrets.Url)
                |> Async.AwaitTask

            let db = conn.GetDatabase()

            let! script =
                ConfigUtils.loadFile "Script/GetFoundVideoIds.lua"
                |> LuaScript.Prepare
                |> fun s ->
                    s.LoadAsync
                        (conn.GetEndPoints()
                         |> Seq.exactlyOne
                         |> conn.GetServer)
                |> Async.AwaitTask

            return { new IRedisClient with
                         member __.GetFoundVideoIds() = getFoundVideoIds db script
                         member __.PutBadIds videoIds = putBadIds db videoIds }
                   |> Ok
        with err ->
            Log.exn err "Error connecting to Redis"
            return Error err
    }
