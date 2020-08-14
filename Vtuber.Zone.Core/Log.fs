module Log

open System.Reflection
open Printf
open NLog

let private currentProject =
    Assembly.GetEntryAssembly().GetName().Name
let private logger = LogManager.GetLogger(currentProject)

let debug<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Info(str)) fmt

let error<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Error(str)) fmt

let fatal<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Fatal(str)) fmt

let info<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Info(str)) fmt

let trace<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Info(str)) fmt

let warn<'a> (fmt : StringFormat<'a, unit>) =
    ksprintf (fun str -> logger.Warn(str)) fmt
