module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.CustomMatchers
open Vtuber.Zone.Core
open Vtuber.Zone.Core.Util

let emptyVtuber =
    { Name = ""
      Channels = List.empty
      TwitterHandle = ""
      Tags = List.empty
      Languages = List.empty }

[<Fact>]
let ``getMissingKeys should return missing keys`` () =
    let expectedIds = [1; 2; 3]
    let map = [1, 1; 3, 3] |> Map.ofList

    getMissingKeys expectedIds map
    |> Seq.toList
    |> should matchList [2]

[<Fact>]
let ``getMissingKeys should ignore extra keys`` () =
    let expectedIds = [1; 2; 3]
    let map = [1, 1; 2, 2; 3, 3; 4, 4] |> Map.ofList

    getMissingKeys expectedIds map
    |> should be Empty

[<Fact>]
let ``getFullChannelMap should include Vtuber information`` () =
    let vtubers =
        [ { Name = "John Henry"
            Channels =
                [ { Platform = Platform.Youtube
                    Id = "abc123"
                    Name = None } ]
            TwitterHandle = ""
            Tags = [ "a"; "b" ]
            Languages = [ "en"; "ja" ] } ]

    let channelMap = getFullChannelMap Platform.Youtube vtubers
    let channel = channelMap.["abc123"]
    channel.Id |> should equal "abc123"
    channel.Name |> should equal "John Henry"
    channel.Platform |> should equal Platform.Youtube
    channel.Tags |> should matchList [ "a"; "b" ]
    channel.Languages |> should matchList [ "en"; "ja" ]

[<Fact>]
let ``getFullChannelMap should combine Vtuber information for shared channels`` () =
    let vtubers =
        [ { Name = "Tweedledee"
            Channels =
                [ { Platform = Platform.Youtube
                    Id = "tweedlech"
                    Name = None } ]
            TwitterHandle = ""
            Tags = [ "tag1"; "tag2" ]
            Languages = [ "de"; "en" ] }
          { Name = "Tweedledum"
            Channels =
                [ { Platform = Platform.Youtube
                    Id = "tweedlech"
                    Name = None } ]
            TwitterHandle = ""
            Tags = [ "tag1"; "tag3" ]
            Languages = [ "ja"; "en" ] } ]

    let channelMap = getFullChannelMap Platform.Youtube vtubers
    let channel = channelMap.["tweedlech"]
    channel.Id |> should equal "tweedlech"
    channel.Name |> should equal "Tweedledee & Tweedledum"
    channel.Platform |> should equal Platform.Youtube
    channel.Tags |> should matchList [ "tag1"; "tag2"; "tag3" ]
    channel.Languages |> should matchList [ "en" ]
