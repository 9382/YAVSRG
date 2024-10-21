﻿namespace Interlude.Features.Rulesets.Edit

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay
open Prelude.Gameplay.Rulesets
open Interlude.UI

type EditGradePage(ruleset: Setting<Ruleset>, id: int) =
    inherit Page()

    let grade = ruleset.Value.Grades.[id]
    let name = Setting.simple grade.Name
    let color = Setting.simple grade.Color
    let acc_required = float32 grade.Accuracy |> Setting.bounded (0.0f, 1.0f) |> Setting.roundf 6

    override this.Content() =
        page_container()
        |+ PageTextEntry(%"rulesets.grade.name", name)
            .Pos(0)
        |+ PageSetting(%"rulesets.grade.color", ColorPicker(color, false))
            .Pos(2, 3)
        |+ PageSetting(%"rulesets.grade.accuracy", 
            Slider(acc_required, Format = (fun v -> sprintf "%.4f%%" (v * 100.0f)), Step = 0.001f)
        )
            .Pos(5)
        :> Widget

    override this.Title = grade.Name
    override this.OnClose() =
        let new_grades = ruleset.Value.Grades |> Array.copy
        new_grades.[id] <- { Name = name.Value.Trim(); Color = color.Value; Accuracy = System.Math.Round(float acc_required.Value, 6) }
        ruleset.Set { ruleset.Value with Grades = new_grades }

type EditGradesPage(ruleset: Setting<Ruleset>) =
    inherit Page()

    let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

    let rec grade_controls (i: int, g: Grade) =
        NavigationContainer.Row()
        |+ ColoredButton(g.Name, g.Color, (fun () -> EditGradePage(ruleset, i).Show()), Position = Position.ShrinkR PRETTYHEIGHT)
        |+ Button(
            Icons.TRASH,
            (fun () -> 
                ConfirmPage(
                    [g.Name] %> "rulesets.grade.confirm_delete",
                    fun () -> delete_grade i
                ).Show()
            ),
            Position = Position.SliceR PRETTYHEIGHT
        )

    and refresh() =
        container.Clear()
        for i, g in ruleset.Value.Grades |> Seq.indexed |> Seq.rev do
            container.Add (grade_controls (i, g))
        container.Add <| Button(sprintf "%s %s" Icons.PLUS_CIRCLE %"rulesets.grade.add", add_grade)

    and add_grade() =
        let new_grade =
            {
                Name = "???"
                Color = Color.White
                Accuracy = 0.0
            }
        ruleset.Set { ruleset.Value with Grades = ruleset.Value.Grades |> Array.append [| new_grade |] }
        defer refresh

    and delete_grade(i: int) : unit =
        ruleset.Set { ruleset.Value with Grades = ruleset.Value.Grades |> Array.removeAt i }
        refresh()

    override this.Content() =
        refresh()
        ScrollContainer(container, Position = Position.Shrink(PRETTY_MARGIN_X, PRETTY_MARGIN_Y).SliceL(PRETTYWIDTH))

    override this.Title = %"rulesets.edit.grades"
    override this.OnClose() = ()
    override this.OnReturnFromNestedPage() = refresh()