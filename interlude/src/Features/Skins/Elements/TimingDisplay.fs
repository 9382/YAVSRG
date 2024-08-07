﻿namespace Interlude.Features.Play.HUD

open System
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay
open Prelude.Skins.HudLayouts
open Interlude.Features.Gameplay
open Interlude.Features.Play

[<Struct>]
type private TimingDisplayHit =
    {
        Time: Time
        Position: float32
        IsRelease: bool
        Judgement: JudgementId option
    }

type TimingDisplay(config: HudConfig, state: PlayState) =
    inherit StaticWidget(NodeType.None)
    let hits = ResizeArray<TimingDisplayHit>()
    let mutable w = 0.0f

    let mutable last_seen_time = -Time.infinity

    let ln_mult =
        if config.TimingDisplayHalfScaleReleases then
            0.5f
        else
            1.0f

    let animation_time = config.TimingDisplayFadeTime * SelectedChart.rate.Value
    let moving_average = Animation.Fade(0.0f)

    do
        if config.TimingDisplayMovingAverageType <> TimingDisplayMovingAverageType.None then
            state.SubscribeToHits(fun ev ->
                match ev.Guts with
                | Hit e ->
                    if not e.Missed then
                        moving_average.Target <- 
                            Percyqaz.Flux.Utils.lerp 
                                config.TimingDisplayMovingAverageSensitivity
                                moving_average.Target 
                                (e.Delta / state.Scoring.MissWindow * w * 0.5f)
                | Release e ->
                    if not e.Missed then
                        moving_average.Target <- 
                            Percyqaz.Flux.Utils.lerp 
                                config.TimingDisplayMovingAverageSensitivity
                                moving_average.Target 
                                (e.Delta / state.Scoring.MissWindow * w * ln_mult)
            )
        if config.TimingDisplayMovingAverageType <> TimingDisplayMovingAverageType.ReplaceBars then
            state.SubscribeToHits(fun ev ->
                match ev.Guts with
                | Hit e ->
                    hits.Add
                        {
                            Time = ev.Time
                            Position = e.Delta / state.Scoring.MissWindow * w * 0.5f
                            IsRelease = false
                            Judgement = e.Judgement
                        }
                | Release e ->
                    hits.Add
                        {
                            Time = ev.Time
                            Position = e.Delta / state.Scoring.MissWindow * w * ln_mult
                            IsRelease = true
                            Judgement = e.Judgement
                        }
            )

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        moving_average.Update elapsed_ms

        if w = 0.0f || moved then
            w <- this.Bounds.Width

        let now = state.CurrentChartTime()

        if now < last_seen_time then
            hits.Clear()

        last_seen_time <- now

        while hits.Count > 0 && hits.[0].Time + animation_time * 1.0f<ms> < now do
            hits.RemoveAt(0)

    override this.Draw() =
        let centre = this.Bounds.CenterX

        if config.TimingDisplayShowGuide then
            Draw.rect
                (Rect.Create(
                    centre - config.TimingDisplayThickness * config.TimingDisplayGuideThickness,
                    this.Bounds.Top,
                    centre + config.TimingDisplayThickness * config.TimingDisplayGuideThickness,
                    this.Bounds.Bottom
                ))
                Color.White

        let now = state.CurrentChartTime()

        match config.TimingDisplayMovingAverageType with
        | TimingDisplayMovingAverageType.ReplaceBars ->
            let r = 
                Rect.Create(
                    centre + moving_average.Value - config.TimingDisplayThickness,
                    this.Bounds.Top,
                    centre + moving_average.Value + config.TimingDisplayThickness,
                    this.Bounds.Bottom
                )
            Draw.rect r config.TimingDisplayMovingAverageColor
        | TimingDisplayMovingAverageType.Arrow ->
            let arrow_height = this.Bounds.Height * 0.5f
            Draw.untextured_quad 
                (
                    Quad.createv 
                        (centre + moving_average.Value, this.Bounds.Top - 10.0f)
                        (centre + moving_average.Value - arrow_height, this.Bounds.Top - 10.0f - arrow_height)
                        (centre + moving_average.Value + arrow_height, this.Bounds.Top - 10.0f - arrow_height)
                        (centre + moving_average.Value, this.Bounds.Top - 10.0f)
                )
                config.TimingDisplayMovingAverageColor.AsQuad
        | _ -> ()

        for hit in hits do
            let r =
                Rect.Create(
                    centre + hit.Position - config.TimingDisplayThickness,
                    this.Bounds.Top,
                    centre + hit.Position + config.TimingDisplayThickness,
                    this.Bounds.Bottom
                )

            let c =
                match hit.Judgement with
                | None ->
                    Color.FromArgb(
                        Math.Clamp(127 - int (127.0f * (now - hit.Time) / animation_time), 0, 127),
                        Color.Silver
                    )
                | Some j ->
                    Color.FromArgb(
                        Math.Clamp(255 - int (255.0f * (now - hit.Time) / animation_time), 0, 255),
                        state.Ruleset.JudgementColor j
                    )

            if config.TimingDisplayShowNonJudgements || hit.Judgement.IsSome then
                Draw.rect
                    (if hit.IsRelease then
                         r.Expand(0.0f, config.TimingDisplayReleasesExtraHeight)
                     else
                         r)
                    c