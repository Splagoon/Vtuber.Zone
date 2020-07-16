module Vtuber.Zone.Core.Util

// Explicit operator to invoke implicit conversion operator
let inline public (~~) (x: ^a): ^b =
    ((^a or ^b): (static member op_Implicit: ^a -> ^b) x)
