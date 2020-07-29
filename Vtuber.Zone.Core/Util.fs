module Vtuber.Zone.Core.Util

open System

// Turns a Nullable<'a> into 'a option
let toOption (nullable: Nullable<'a>) =
    if nullable.HasValue then Some nullable.Value else None

let combineNames : Vtuber seq -> string =
  Seq.map (fun v -> v.Name)
  >> String.concat " & "

let combineTags : Vtuber seq -> string list =
  Seq.collect (fun v -> v.Tags)
  >> Set.ofSeq
  >> Set.toList

let getChannelIds platform =
    Seq.collect (fun v -> v.Channels)
    >> Seq.filter (fun c -> c.Platform = platform)
    >> Seq.map (fun c -> c.Id)

let getChannelToVtuberMap platform vtubers =
    seq { 
        for vtuber in vtubers do
            for channel in vtuber.Channels do
                if channel.Platform = platform then
                    yield channel.Id, vtuber
    }
    |> Seq.fold
        (fun map (id, vtuber) ->
            let vtubers =
                match map |> Map.tryFind id with
                | Some vtubers -> vtuber :: vtubers
                | None -> [vtuber]
            map |> Map.add id vtubers)
        Map.empty

let defaultIcon = "/image/default-icon.png"
