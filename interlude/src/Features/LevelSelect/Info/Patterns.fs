﻿namespace Interlude.Features.LevelSelect

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Charts.Processing.Patterns
open Prelude.Charts.Processing.Difficulty
open Interlude.UI
open Interlude.Features.Gameplay

[<RequireQualifiedAccess>]
type Display =
    | Local
    | Online
    | Patterns

type Patterns(display: Setting<Display>) =
    inherit Container(NodeType.None)

    let mutable patterns: PatternSummary.PatternBreakdown list = []
    let mutable category: PatternSummary.ChartCategorisation = PatternSummary.ChartCategorisation.Default

    override this.Init(parent: Widget) =
        base.Init parent

        this
        |* StylishButton(
            (fun () -> display.Set Display.Local),
            K <| %"levelselect.info.details",
            !%Palette.MAIN_100,
            Hotkey = "scoreboard_storage",
            TiltLeft = false,
            TiltRight = false,
            Position =
                {
                    Left = 0.0f %+ 0.0f
                    Top = 0.0f %+ 0.0f
                    Right = 1.0f %- 0.0f
                    Bottom = 0.0f %+ 50.0f
                }
        )
            .Help(Help.Info("levelselect.info.mode", "scoreboard_storage"))

    override this.Draw() =
        base.Draw()

        let mutable b =
            this.Bounds.SliceT(60.0f).Shrink(20.0f, 0.0f).Translate(0.0f, 60.0f)

        let TEXT_WIDTH = 240.0f
        let BAR_L = b.Left + TEXT_WIDTH + 5.0f
        let BAR_R = b.Right - 5.0f
        let BAR_WIDTH = BAR_R - BAR_L

        for entry in patterns do
            Text.fill_b (
                Style.font,
                (sprintf "%O" entry.Pattern),
                b.ShrinkB(25.0f).SliceL(TEXT_WIDTH),
                Colors.text,
                Alignment.LEFT
            )

            let feels_like_bpm = entry.Pattern.DensityToBPM * entry.Density50 |> int

            Text.fill_b (
                Style.font,
                (if entry.Mixed then
                     sprintf "Mixed, ~%i BPM (feels like %i)" entry.BPM feels_like_bpm
                 else
                     sprintf "%i BPM (feels like %i)" entry.BPM feels_like_bpm),
                b.SliceB(30.0f).SliceL(TEXT_WIDTH),
                Colors.text_subheading,
                Alignment.LEFT
            )

            Text.fill_b (
                Style.font,
                String.concat ", " (entry.Specifics |> Seq.map fst),
                b.SliceB(30.0f).ShrinkL(TEXT_WIDTH),
                Colors.text_subheading,
                Alignment.LEFT
            )

            Draw.rect (b.SliceL(5.0f).SliceT(20.0f).Translate(TEXT_WIDTH, 10.0f)) Colors.white
            Draw.rect (b.SliceR(5.0f).SliceT(20.0f).Translate(0.0f, 10.0f)) Colors.white

            let density_color (nps: float32) =
                nps * 2.0f |> float |> DifficultyRating.physical_color

            let bar_scale = min 1.0f (entry.Amount / 1000.0f<ms / rate> / 100.0f)

            let bar (lo_pc, lo_val, hi_pc, hi_val) =
                Draw.untextured_quad
                    (Rect
                        .Create(
                            BAR_L + lo_pc * BAR_WIDTH * bar_scale,
                            b.Top + 12.5f,
                            BAR_L + hi_pc * BAR_WIDTH * bar_scale,
                            b.Top + 27.5f
                        )
                        .AsQuad)
                    (Quad.gradient_left_to_right (density_color lo_val) (density_color hi_val))

            bar (0.0f, entry.Density10, 0.1f, entry.Density10)
            bar (0.1f, entry.Density10, 0.25f, entry.Density25)
            bar (0.25f, entry.Density25, 0.5f, entry.Density50)
            bar (0.5f, entry.Density50, 0.75f, entry.Density75)
            bar (0.75f, entry.Density75, 0.9f, entry.Density90)
            bar (0.9f, entry.Density90, 1.0f, entry.Density90)

            b <- b.Translate(0.0f, 60.0f)
        
        Text.fill_b (
            Style.font,
            String.concat ", " category.MinorFeatures,
            this.Bounds.SliceB(30.0f).Shrink(20.0f, 0.0f),
            Colors.text_greyout,
            Alignment.LEFT
        )

    member this.OnChartUpdated(info: LoadedChartInfo) =
        patterns <- info.Patterns.Patterns |> List.truncate 6
        category <- info.Patterns.Category
        printfn "%.2f - %.2f - %.2f - %.2f - %.2f" 
            info.Patterns.Density10
            info.Patterns.Density25
            info.Patterns.Density50
            info.Patterns.Density75
            info.Patterns.Density90
