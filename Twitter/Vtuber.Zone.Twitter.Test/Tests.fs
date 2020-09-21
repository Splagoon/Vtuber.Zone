module Tests

open System
open Xunit
open FsUnit.Xunit
open Vtuber.Zone.Twitter.Main

[<Fact>]
let ``parseYouTubeUrl should parse long URLs`` () =
    parseYouTubeUrl "youtube.com/watch?v=abc"
    |> should equal (Some "abc")
    parseYouTubeUrl "http://youtube.com/watch?v=defghi&other=stuff"
    |> should equal (Some "defghi")
    parseYouTubeUrl "https://youtube.com/watch?v=jk"
    |> should equal (Some "jk")
    parseYouTubeUrl "http://www.youtube.com/watch?other=stuff&v=lmnop"
    |> should equal (Some "lmnop")
    parseYouTubeUrl "https://www.youtube.com/watch?this=that&beginning=end&v=qrs&other=stuff"
    |> should equal (Some "qrs")

[<Fact>]
let ``parseYouTubeUrl should parse short URLs`` () =
    parseYouTubeUrl "youtu.be/abc"
    |> should equal (Some "abc")
    parseYouTubeUrl "http://youtu.be/defghi&other=stuff"
    |> should equal (Some "defghi")
    parseYouTubeUrl "https://youtu.be/jk"
    |> should equal (Some "jk")
    parseYouTubeUrl "http://www.youtu.be/lmnop&other=stuff"
    |> should equal (Some "lmnop")
    parseYouTubeUrl "https://www.youtu.be/qrs&this=that&beginning=end&other=stuff"
    |> should equal (Some "qrs")

[<Fact>]
let ``parseYouTubeUrl should not parse other URLs`` () =
    parseYouTubeUrl "other.website"
    |> should equal None
    parseYouTubeUrl "example.com/watch?v=abc"
    |> should equal None
    parseYouTubeUrl "https://www.youtube.com/channel/abcdef"
    |> should equal None
