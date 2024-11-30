﻿namespace Interlude.Features.Rulesets.Edit

open Percyqaz.Common
open Percyqaz.Flux.Windowing
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay.Rulesets
open Interlude.UI

type EditLampPage(ruleset: Setting<Ruleset>, id: int) =
    inherit Page()

    let lamp = ruleset.Value.Lamps.[id]
    let name = Setting.simple lamp.Name
    let color = Setting.simple lamp.Color
    let judgement_type = Setting.simple (match lamp.Requirement with LampRequirement.ComboBreaksAtMost _ -> -1 | LampRequirement.JudgementAtMost (j, _) -> j)
    let judgement_threshold = Setting.simple (match lamp.Requirement with LampRequirement.ComboBreaksAtMost n -> n | LampRequirement.JudgementAtMost (_, n) -> n)

    override this.Content() =
        page_container()
        |+ PageTextEntry(%"rulesets.lamp.name", name)
            .Pos(0)
        |+ PageSetting(%"rulesets.lamp.color", ColorPicker(color, false))
            .Pos(2, 3)
        |+ PageSetting(%"rulesets.lamp.requirement",
            NavigationContainer.Row()
            |+ SelectDropdown(
                seq {
                    for i, j in ruleset.Value.Judgements |> Array.indexed do
                        yield i, j.Name
                    yield -1, "Combo breaks"
                } |> Array.ofSeq,
                judgement_type,
                Position = { Position.DEFAULT with Right = 0.5f %- 100.0f }
            )
            |+ Text("<=")
            |+ Selector(
                [|0, "0"; 1, "1"; 9, "9"; 99, "99"|],
                judgement_threshold,
                Position = { Position.DEFAULT with Left = 0.5f %+ 100.0f }
            )
        )
            .Pos(5)
        :> Widget

    override this.Title = lamp.Name
    override this.OnClose() =
        let new_lamps = ruleset.Value.Lamps |> Array.copy
        new_lamps.[id] <- 
            { 
                Name = name.Value.Trim()
                Color = color.Value
                Requirement =
                    if judgement_type.Value < 0 then 
                        LampRequirement.ComboBreaksAtMost judgement_threshold.Value
                    else
                        LampRequirement.JudgementAtMost (judgement_type.Value, judgement_threshold.Value)
            }
        ruleset.Set 
            { ruleset.Value with 
                Lamps = 
                    new_lamps 
                    |> Array.sortByDescending (fun l -> l.Requirement.SortKey)
            }

type EditLampsPage(ruleset: Setting<Ruleset>) =
    inherit Page()

    let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

    let rec lamp_controls (i: int, l: Lamp) =
        NavigationContainer.Row()
        |+ ColoredButton(l.Name, l.Color, (fun () -> EditLampPage(ruleset, i).Show()), Position = Position.ShrinkR PRETTYHEIGHT)
        |+ Button(
            Icons.TRASH,
            (fun () -> 
                ConfirmPage(
                    [l.Name] %> "rulesets.lamp.confirm_delete",
                    fun () -> delete_lamp i
                ).Show()
            ),
            Position = Position.SliceR PRETTYHEIGHT
        )

    and refresh() =
        container.Clear()
        for i, l in ruleset.Value.Lamps |> Seq.indexed |> Seq.rev do
            container.Add (lamp_controls (i, l))
        container.Add <| Button(sprintf "%s %s" Icons.PLUS_CIRCLE %"rulesets.lamp.add", add_lamp)

    and add_lamp() =
        let new_lamp =
            {
                Name = "???"
                Color = Color.White
                Requirement = LampRequirement.ComboBreaksAtMost System.Int32.MaxValue
            }
        ruleset.Set { ruleset.Value with Lamps = ruleset.Value.Lamps |> Array.append [| new_lamp |] }
        GameThread.defer refresh

    and delete_lamp(i: int) : unit =
        ruleset.Set { ruleset.Value with Lamps = ruleset.Value.Lamps |> Array.removeAt i }
        refresh()

    override this.Content() =
        refresh()
        ScrollContainer(container, Position = Position.Shrink(PRETTY_MARGIN_X, PRETTY_MARGIN_Y).SliceL(PRETTYWIDTH))

    override this.Title = %"rulesets.edit.lamps"
    override this.OnClose() = ()
    override this.OnReturnFromNestedPage() = refresh()