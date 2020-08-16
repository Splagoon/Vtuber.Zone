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

let combineLanguages : Vtuber seq -> string list =
    // Unlike tags, which are a union, languages are an intersection
    // (i.e. the vtubers will likely only speak in langauges they all know)
    Seq.map (fun v -> v.Languages |> Set.ofSeq)
    >> Set.intersectMany
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

let getMissingKeys expectedKeys map =
    if Map.count map < Seq.length expectedKeys then
        seq {
            for key in expectedKeys do
                if map |> Map.tryFind key |> Option.isNone then
                    yield key
        }
    else
        Seq.empty

let defaultIcon = "/image/default-icon.png"
