open Suave
open Suave.Operators
open Suave.Filters
open Suave.Writers
open Suave.Successful
open Vtuber.Zone.Core
open Vtuber.Zone.Web.DB

let config = Config.Load()

let getLiveStreams _ =
  getAllStreams ()
  |> Seq.toArray
  |> JsonUtils.serialize
  |> OK

let formatChannel (channel : Channel) =
  channel.Platform,
  match channel.Platform with
  | Platform.Youtube -> Some <| sprintf "https://www.youtube.com/channel/%s" channel.Id
  | Platform.Twitch -> Some <| sprintf "https://www.twitch.tv/%s" channel.Id
  | _ -> None

let vtubersPayload =
  config.Vtubers
  |> List.map
    (fun vtuber ->
      {| name = vtuber.Name
         channels = vtuber.Channels
                    |> List.map formatChannel
                    |> List.choose
                      (fun (platform, url) ->
                        match url with
                        | Some url -> Some {| platform = platform
                                              url = url |}
                        | None -> None)
         twitter = sprintf "https://twitter.com/%s" vtuber.TwitterHandle |})
  |> List.sortBy (fun vtuber -> vtuber.name |> String.toLowerInvariant)
  |> JsonUtils.serialize

let getVtubers _ =
  OK vtubersPayload

let routes =
  choose [
    GET >=> choose
      [ path "/streams/live" >=> request getLiveStreams
        path "/vtubers" >=> request getVtubers ]
  ] >=> setMimeType "application/json; charset=utf-8"
    >=> setHeader  "Access-Control-Allow-Origin" "*"

[<EntryPoint>]
let main _ =
    startWebServer defaultConfig routes
    0 // return an integer exit code
