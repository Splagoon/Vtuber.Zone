open Suave
open Suave.Sockets.Control
open Suave.Logging
open Suave.Operators
open Suave.EventSource
open Suave.Filters
open Suave.Writers
open Suave.Files
open Suave.Successful
open Suave.State.CookieStateStore
open Suave.RequestErrors
open Vtuber.Zone.Core
open Vtuber.Zone.Web.DB

let getLiveStreams _ =
  getAllStreams ()
  |> Seq.toArray
  |> JsonUtils.serialize
  |> OK

let routes =
  choose [
    GET >=> choose
      [ path "/streams/live" >=> request getLiveStreams ]
  ] >=> setMimeType "application/json; charset=utf-8"
    >=> setHeader  "Access-Control-Allow-Origin" "*"

[<EntryPoint>]
let main _ =
    startWebServer defaultConfig routes
    0 // return an integer exit code
