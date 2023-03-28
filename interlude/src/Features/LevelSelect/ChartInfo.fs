﻿namespace Interlude.Features.LevelSelect

open System
open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Data.Charts.Caching
open Prelude.Gameplay.Mods
open Prelude.Gameplay.Difficulty
open Prelude.Charts.Formats.Interlude
open Interlude.Features
open Interlude.Options
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Components
open Interlude.UI.Menu
open Interlude.Features.Gameplay

type ChartInfo() as this =
    inherit StaticContainer(NodeType.None)

    let scores = Scoreboard(Position = Position.TrimBottom 120.0f)
    let mutable length = ""
    let mutable bpm = ""
    let mutable notecounts = ""

    do
        this
        |+ scores

        |+ Text(
            fun () -> sprintf "%s %.2f" Icons.star (match Chart.rating with None -> 0.0 | Some d -> d.Physical)
            ,
            Color = (fun () -> Color.White, match Chart.rating with None -> Color.Black | Some d -> physicalColor d.Physical),
            Align = Alignment.LEFT,
            Position = { Left = 0.0f %+ 10.0f; Top = 1.0f %- 170.0f; Right = 0.33f %- 10.0f; Bottom = 1.0f %- 100.0f })

        |+ Text(
            (fun () -> bpm),
            Align = Alignment.CENTER,
            Position = { Left = 0.33f %+ 10.0f; Top = 1.0f %- 170.0f; Right = 0.66f %- 10.0f; Bottom = 1.0f %- 100.0f })

        |+ Text(
            (fun () -> length),
            Align = Alignment.RIGHT,
            Position = { Left = 0.66f %+ 10.0f; Top = 1.0f %- 170.0f; Right = 1.0f %- 10.0f; Bottom = 1.0f %- 100.0f })

        |+ Text(
            (fun () -> notecounts),
            Align = Alignment.RIGHT,
            Position = { Left = 0.0f %+ 10.0f; Top = 1.0f %- 100.0f; Right = 1.0f %- 17.0f; Bottom = 1.0f %- 60.0f })

        |+ Text(
            (fun () -> getModString(rate.Value, selectedMods.Value, autoplay)),
            Align = Alignment.LEFT,
            Position = { Left = 0.0f %+ 10.0f; Top = 1.0f %- 100.0f; Right = 1.0f %- 10.0f; Bottom = 1.0f %- 60.0f })
            
        |+ StylishButton(
            (fun () -> match Chart.current with Some c -> Preview(c).Show() | None -> ()),
            K (Icons.preview + " " + L"levelselect.preview.name"),
            Style.main 100,
            Hotkey = "preview",
            TiltLeft = false,
            Position = { Left = 0.0f %+ 0.0f; Top = 1.0f %- 50.0f; Right = 0.33f %- 25.0f; Bottom = 1.0f %- 0.0f })
            .Tooltip(Tooltip.Info("levelselect.preview", "preview"))

        |+ ModSelect(scores.Refresh,
             Position = { Left = 0.33f %+ 0.0f; Top = 1.0f %- 50.0f; Right = 0.66f %- 25.0f; Bottom = 1.0f %- 0.0f })
            .Tooltip(Tooltip.Info("levelselect.mods", "mods"))
        
        |* Rulesets.QuickSwitcher(
            options.SelectedRuleset |> Setting.trigger (fun _ -> LevelSelect.refresh <- true),
            Position = { Left = 0.66f %+ 0.0f; Top = 1.0f %- 50.0f; Right = 1.0f %- 0.0f; Bottom = 1.0f %- 0.0f })
            .Tooltip(Tooltip.Info("levelselect.rulesets", "ruleset_switch").Hotkey(L"levelselect.rulesets.picker_hint", "ruleset_picker"))

    member this.Refresh() =
        length <- Chart.format_duration()
        bpm <- Chart.format_bpm()
        notecounts <- Chart.format_notecounts()
        scores.Refresh()