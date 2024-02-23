﻿namespace Interlude.Features.Printerlude

open System
open OpenTK.Windowing.GraphicsLibraryFramework
open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude.Common
open Interlude.Options
open Interlude.UI

module Terminal =

    let private LOCK_OBJ = obj ()

    let mutable exec_command = fun (c: string) -> ()

    module Log =

        let mutable private pos = 0
        let mutable private log: ResizeArray<string> = ResizeArray()

        let mutable visible: string seq = []

        let LINEWIDTH = 113

        let add (s: string) =
            lock
                LOCK_OBJ
                (fun () ->
                    try
                        for line in s.Split('\n', StringSplitOptions.RemoveEmptyEntries) do
                            let mutable line = line

                            while line.Length > LINEWIDTH do
                                let split =
                                    match line.Substring(0, LINEWIDTH).LastIndexOf(' ') with
                                    | -1 -> LINEWIDTH
                                    | 0 -> LINEWIDTH
                                    | n -> n

                                log.Insert(0, line.Substring(0, split))
                                line <- line.Substring(split)

                            log.Insert(0, line)

                        visible <- Seq.skip pos log
                    with x ->
                        printfn "%O" x
                )

        let up () =
            if log.Count - 15 > pos then
                pos <- pos + 5
                visible <- Seq.skip pos log

        let down () =
            if pos - 5 >= 0 then
                pos <- pos - 5
                visible <- Seq.skip pos log

        let home () =
            pos <- 0
            visible <- log

        let clear () =
            log.Clear()
            home ()

    let add_message (s: string) = Log.add s

    let private current_line = Setting.simple ""

    let private log_up_key = Bind.mk Keys.PageUp
    let private log_down_key = Bind.mk Keys.PageDown
    let private log_home_key = Bind.mk Keys.End

    let private cmd_up_key = Bind.mk Keys.Up
    let private cmd_down_key = Bind.mk Keys.Down
    let private cmd_send_key = Bind.mk Keys.Enter

    module private History =
        let mutable private pos = -1
        let mutable private history: string list = []

        let up () =
            if history.Length - 1 > pos then
                pos <- pos + 1
                current_line.Value <- history.[pos]

        let down () =
            if pos > 0 then
                pos <- pos - 1
                current_line.Value <- history.[pos]

        let add (l) =
            history <- l :: history
            pos <- -1

    let mutable shown = false

    let private hide () =
        shown <- false
        Input.remove_listener ()

    let private show () =
        shown <- true

        let rec add_input () =
            Input.listen_to_text (
                current_line,
                false,
                fun () ->
                    if shown then
                        sync add_input
            )

        add_input ()

    let drop_file (path: string) =
        let path = path.Replace("""\""", """\\""")
        let v = current_line.Value

        if v.Length = 0 || Char.IsWhiteSpace(v.[v.Length - 1]) then
            current_line.Set(sprintf "%s\"%s\"" v path)
        else
            current_line.Set(sprintf "%s \"%s\"" v path)

    let font =
        lazy
            (Fonts.create "Courier Prime Sans"
             |> fun x ->
                 x.SpaceWidth <- 0.75f
                 x)

    let draw () =
        if not shown then
            ()
        else

        let bounds = Viewport.bounds.Shrink(100.0f)

        Draw.rect (bounds.Expand 5.0f) (Colors.white.O2)
        Draw.rect (bounds.TrimBottom 70.0f) (Colors.shadow_1.O3)
        Draw.rect (bounds.SliceBottom 65.0f) (Colors.shadow_2.O3)

        Text.draw_b (
            font.Value,
            "> " + current_line.Value,
            30.0f,
            bounds.Left + 20.0f,
            bounds.Bottom - 50.0f,
            Colors.text
        )

        lock
            LOCK_OBJ
            (fun () ->
                for i, line in Seq.indexed Log.visible do
                    if i < 19 then
                        Text.draw_b (
                            font.Value,
                            line,
                            20.0f,
                            bounds.Left + 20.0f,
                            bounds.Bottom - 60.0f - 60.0f - 40f * float32 i,
                            Colors.text
                        )
            )

    let update () =
        if shown && (%%"exit").Tapped() then
            hide ()

        if
            options.EnableConsole.Value
            && not shown
            && Screen.current_type <> Screen.Type.Play
            && (%%"console").Tapped()
        then
            show ()

        if not shown then
            ()
        else

        lock
            LOCK_OBJ
            (fun () ->
                if cmd_send_key.Tapped() && current_line.Value <> "" then
                    add_message ("> " + current_line.Value)
                    exec_command current_line.Value
                    History.add current_line.Value
                    Log.home ()
                    current_line.Value <- ""
                elif cmd_up_key.Tapped() then
                    History.up ()
                elif cmd_down_key.Tapped() then
                    History.down ()
                elif log_up_key.Tapped() then
                    Log.up ()
                elif log_down_key.Tapped() then
                    Log.down ()
                elif log_home_key.Tapped() then
                    Log.home ()
            )
