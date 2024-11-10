﻿namespace Interlude.Features.LevelSelect

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude
open Prelude.Charts.Processing.Difficulty
open Prelude.Gameplay.Mods
open Interlude.UI
open Interlude.Features.Gameplay

type ChartDetails() =
    inherit Container(NodeType.None)

    let mutable rating = 0.0
    let mutable notecounts = ""
    let mutable last_played = "Never played"

    let refresh(info: LoadedChartInfo) =
        rating <- info.Rating.Physical
        notecounts <- info.NotecountsString
        last_played <- 
            if info.SaveData.LastPlayed <= 0L then
                "Never played"
            else
                sprintf "Last played %s ago" (Timestamp.since info.SaveData.LastPlayed |> format_timespan)

    static member HEIGHT = 230.0f

    override this.Init(parent) =
        SelectedChart.on_chart_change_finished.Add refresh
        SelectedChart.on_chart_update_finished.Add refresh
        SelectedChart.if_loaded refresh

        this |* PersonalBests(Position = Position.SliceT(100.0f))

        base.Init parent

    override this.Draw() =
        
        let play_info = this.Bounds.SliceT(40.0f).TranslateY(90.0f).ShrinkX(15.0f)

        Text.fill_b (Style.font, Mods.format (SelectedChart.rate.Value, SelectedChart.selected_mods.Value, SelectedChart.autoplay), play_info, Colors.text, Alignment.LEFT)
        Text.fill_b (Style.font, last_played, play_info, Colors.text, Alignment.RIGHT)

        let chart_info = this.Bounds.SliceT(30.0f).TranslateY(130.0f).ShrinkX(15.0f)
        Text.fill_b (Style.font, (match SelectedChart.CACHE_DATA with Some cc -> cc.DifficultyName | None -> ""), chart_info.SlicePercentL 0.5f, Colors.text_subheading, Alignment.LEFT)
        Text.fill_b (Style.font, notecounts, chart_info, Colors.text_subheading, Alignment.RIGHT)

        let three_icon_infos = this.Bounds.SliceT(70.0f).TranslateY(155.0f).ShrinkX(15.0f)
        Text.fill_b (Style.font, sprintf "%s %.2f" Icons.STAR rating, three_icon_infos, (Colors.white, DifficultyRating.physical_color rating), Alignment.LEFT)
        Text.fill_b (Style.font, SelectedChart.FMT_BPM, three_icon_infos, Colors.text, Alignment.CENTER)
        Text.fill_b (Style.font, SelectedChart.FMT_DURATION, three_icon_infos, Colors.text, Alignment.RIGHT)

        base.Draw()