﻿namespace Interlude.Features.Pacemaker

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay
open Interlude.Options
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Content

type PacemakerOptionsPage() =
    inherit Page()

    let ruleset_id = Rulesets.current_hash

    let existing =
        if options.Pacemaker.ContainsKey ruleset_id then
            options.Pacemaker.[ruleset_id]
        else
            PacemakerSettings.Default

    let mode = Setting.simple existing.Mode

    let accuracy = 
        existing.Accuracy
        |> Setting.simple
        |> Setting.bound 0.0 1.0
        |> Setting.round 3

    let lamp = Setting.simple existing.Lamp

    let use_personal_best = Setting.simple existing.UsePersonalBest

    override this.Content() = 
        let lamps =
            seq {
                let lamp_count = Rulesets.current.Grading.Lamps.Length
                for i in 0 .. lamp_count - 1 do
                    yield i, Rulesets.current.LampName i
                if lamp_count = 0 then
                    yield 0, Rulesets.current.LampName 0
            }
            |> Array.ofSeq

        page_container()
        |+ PageSetting(%"gameplay.pacemaker.saveunderpace", Checkbox options.SaveScoreIfUnderPace)
            .Tooltip(Tooltip.Info("gameplay.pacemaker.saveunderpace"))
            .Pos(0)
        |+ PageSetting(%"gameplay.pacemaker.onlysavenewrecords", Checkbox options.OnlySaveNewRecords)
            .Tooltip(Tooltip.Info("gameplay.pacemaker.onlysavenewrecords"))
            .Pos(2)
        |+ PageSetting(%"gameplay.pacemaker.type",
            SelectDropdown([| PacemakerMode.Accuracy, %"gameplay.pacemaker.accuracy"; PacemakerMode.Lamp, %"gameplay.pacemaker.lamp" |], mode)
        )
            .Pos(5)
        |+ PageSetting(%"gameplay.pacemaker.use_personal_best", Checkbox use_personal_best)
            .Tooltip(Tooltip.Info("gameplay.pacemaker.use_personal_best"))
            .Pos(7)
        |+ PageSetting(%"gameplay.pacemaker.accuracy", Slider.Percent(accuracy |> Setting.f32)) 
            .Pos(9)
            .Conditional(fun () -> mode.Value = PacemakerMode.Accuracy)
        |+ PageSetting(%"gameplay.pacemaker.lamp", SelectDropdown(lamps, lamp))
            .Pos(9)
            .Conditional(fun () -> mode.Value = PacemakerMode.Lamp)
        |>> Container
        |+ Text(
            %"gameplay.pacemaker.hint",
            Align = Alignment.CENTER,
            Position = Position.SliceBottom(100.0f).TrimBottom(40.0f)
        )
        :> Widget

    override this.Title = sprintf "%s %s" Icons.FLAG (%"gameplay.pacemaker")

    override this.OnClose() =
        options.Pacemaker.[ruleset_id] <-
            {
                Accuracy = accuracy.Value
                Lamp = lamp.Value
                Mode = mode.Value
                UsePersonalBest = use_personal_best.Value
            }