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

let getLiveStreams (req : HttpRequest) =
  let serialize = Seq.toArray >> JsonUtils.serialize >> OK
  match req.queryParam "by" with
  | Choice1Of2 "viewers" -> getAllStreamsByViewers() |> serialize
  | Choice1Of2 "start-time" -> getAllStreamsByStartTime() |> serialize
  | _ -> BAD_REQUEST "missing or unsupported sort parameter"

let routes =
  choose [
    GET >=> choose
      [ path "/streams/live" >=> request getLiveStreams ]
  ] >=> setMimeType "application/json; charset=utf-8"

[<EntryPoint>]
let main _ =
    startWebServer defaultConfig routes
    0 // return an integer exit code
