﻿open Percyqaz.Shell
open Percyqaz.Shell.Shell
open YAVSRG.CLI

let ctx = ShellContext.Empty |> Commands.register

[<EntryPoint>]
let main argv =
    let io = IOContext.Console

    if argv.Length > 0 then
        ctx.Evaluate io (String.concat " " argv)
    else
        printfn "== YAVSRG CLI Tools =="
        repl io ctx

    0
