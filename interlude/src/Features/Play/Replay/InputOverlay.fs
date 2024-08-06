﻿namespace Interlude.Features.Play.Replay

open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Audio
open Percyqaz.Common
open Prelude
open Prelude.Gameplay
open Interlude.Options
open Interlude.Features.Gameplay
open Interlude.Features.Play

type private InputOverlay(keys, replay_data: ReplayData, state: PlayState, playfield: Playfield, enable: Setting<bool>)
    =
    inherit StaticWidget(NodeType.None)

    let mutable seek = 0
    let keys_down = Array.zeroCreate keys
    let keys_times = Array.zeroCreate keys

    let scroll_direction_pos: float32 -> Rect -> Rect =
        if options.Upscroll.Value then
            fun _ -> id
        else
            fun bottom ->
                fun (r: Rect) ->
                    {
                        Left = r.Left
                        Top = bottom - r.Bottom
                        Right = r.Right
                        Bottom = bottom - r.Top
                    }

    override this.Init(parent) =
        state.ScoringChanged.Publish.Add(fun _ -> seek <- 0)
        base.Init parent

    override this.Draw() =

        if enable.Value then
            let draw_press (k, now: ChartTime, start: ChartTime, finish: ChartTime) =
                let y t =
                    float32 options.HitPosition.Value
                    + float32 (t - now) * (options.ScrollSpeed.Value / SelectedChart.rate.Value)
                    + playfield.ColumnWidth * 0.5f

                Rect
                    .Create(
                        playfield.Bounds.Left + playfield.ColumnPositions.[k],
                        y start,
                        playfield.Bounds.Left + playfield.ColumnPositions.[k] + playfield.ColumnWidth,
                        y finish
                    )
                    .Shrink(20.0f, 0.0f)
                |> scroll_direction_pos playfield.Bounds.Bottom
                |> fun a -> Draw.rect a Colors.grey_2.O2

            let now =
                state.CurrentChartTime()
                + (if Song.playing() then Performance.frame_compensation () else 0.0f<ms>)
                + options.VisualOffset.Value * 1.0f<ms> * SelectedChart.rate.Value

            while replay_data.Length - 1 > seek
                  && let struct (t, _) = replay_data.[seek + 1] in
                     t < now - 100.0f<ms> do
                seek <- seek + 1

            let until_time =
                now + 1080.0f<ms> / (options.ScrollSpeed.Value / SelectedChart.rate.Value)

            let mutable peek = seek
            let struct (t, b) = replay_data.[peek]

            for k = 0 to keys - 1 do
                if Bitmask.has_key k b then
                    keys_down.[k] <- true
                    keys_times.[k] <- t
                else
                    keys_down.[k] <- false

            while replay_data.Length - 1 > peek
                  && let struct (t, _) = replay_data.[peek] in
                     t < until_time do
                let struct (t, b) = replay_data.[peek]

                for k = 0 to keys - 1 do
                    if Bitmask.has_key k b then
                        if not keys_down.[k] then
                            keys_down.[k] <- true
                            keys_times.[k] <- t
                    else if keys_down.[k] then
                        keys_down.[k] <- false
                        draw_press (k, now, keys_times.[k], t)

                peek <- peek + 1

            for k = 0 to keys - 1 do
                if keys_down.[k] then
                    draw_press (k, now, keys_times.[k], until_time)