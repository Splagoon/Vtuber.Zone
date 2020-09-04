module Vtuber.Zone.Core.Util

open System

// Turns a Nullable<'a> into 'a option
let toOption (nullable: Nullable<'a>) =
    if nullable.HasValue then Some nullable.Value else None

let combineNames: Vtuber seq -> string =
    Seq.map (fun v -> v.Name) >> String.concat " & "

let combineTags: Vtuber seq -> string list =
    Seq.collect (fun v -> v.Tags)
    >> Set.ofSeq
    >> Set.toList

let combineLanguages: Vtuber seq -> string list =
    // Unlike tags, which are a union, languages are an intersection
    // (i.e. the vtubers will likely only speak in langauges they all know)
    Seq.map (fun v -> v.Languages |> Set.ofSeq)
    >> Set.intersectMany
    >> Set.toList

let channelName (channel: PartialChannel) vtubers =
    match channel.Name with
    | Some name -> name
    | None -> combineNames vtubers

let getChannelIds platform =
    Seq.collect (fun v -> v.Channels)
    >> Seq.choose (fun c -> if c.Platform = platform then Some c.Id else None)

let getFullChannelMap platform vtubers =
    seq {
        for vtuber in vtubers do
            for channel in vtuber.Channels do
                if channel.Platform = platform then yield channel, vtuber
    }
    |> Seq.groupBy fst
    |> Seq.map (fun (channel, tuples) ->
        let vtubers = tuples |> Seq.map snd

        let name =
            match channel.Name with
            | Some name -> name
            | None -> vtubers |> combineNames

        channel.Id,
        { Platform = channel.Platform
          Id = channel.Id
          Name = name
          Tags = vtubers |> combineTags
          Languages = vtubers |> combineLanguages })
    |> Map.ofSeq

let getMissingKeys expectedKeys map =
    if Map.count map < Seq.length expectedKeys then
        seq {
            for key in expectedKeys do
                if map |> Map.tryFind key |> Option.isNone
                then yield key
        }
    else
        Seq.empty

let defaultIcon = "/image/default-icon.png"
