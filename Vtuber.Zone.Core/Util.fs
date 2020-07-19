module Vtuber.Zone.Core.Util

open System

// Explicit operator to invoke implicit conversion operator
let inline public (~~) (x: ^a): ^b =
    ((^a or ^b): (static member op_Implicit: ^a -> ^b) x)

// Turns a Nullable<'a> into 'a option
let toOption (nullable: Nullable<'a>) =
    if nullable.HasValue then Some nullable.Value else None
