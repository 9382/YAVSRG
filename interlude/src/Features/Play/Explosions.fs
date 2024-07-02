﻿namespace Interlude.Features.Play

open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Charts.Processing
open Prelude.Gameplay
open Prelude.Skinning.Noteskins
open Interlude.Options
open Interlude.Content
open Interlude.Features.Gameplay
open Interlude.Features.Play

[<Struct>]
type private Explosion =
    {
        Column: int
        Color: int
        IsRelease: bool
        Time: Time
    }
    static member Nothing =
        {
            Column = 0
            Color = 0
            IsRelease = false
            Time = -Time.infinity
        }

type Explosions(keys, ns: NoteskinConfig, state: PlayState) =
    inherit StaticWidget(NodeType.None)

    let mutable last_time : Time = -Time.infinity

    let hold_colors: int array = Array.zeroCreate keys
    let holding: bool array = Array.create keys false
    let holding_since: Time array = Array.create keys 0.0f<ms>

    let note_explosion = Content.Texture "noteexplosion"
    let hold_explosion = Content.Texture "holdexplosion"

    let release_explosion =
        if ns.HoldExplosionSettings.UseReleaseExplosion then
            Content.Texture "releaseexplosion"
        else
            hold_explosion

    let note_duration =
        if ns.NoteExplosionSettings.UseBuiltInAnimation then
            float32 ns.NoteExplosionSettings.Duration * 1.0f<ms> * SelectedChart.rate.Value
        else
            float32 ns.NoteExplosionSettings.AnimationFrameTime
            * float32 note_explosion.Columns
            * 1.0f<ms>
            * SelectedChart.rate.Value

    let release_duration =
        if ns.HoldExplosionSettings.UseBuiltInAnimation then
            float32 ns.HoldExplosionSettings.Duration * 1.0f<ms> * SelectedChart.rate.Value
        else
            float32 ns.HoldExplosionSettings.AnimationFrameTime
            * float32 release_explosion.Columns
            * 1.0f<ms>
            * SelectedChart.rate.Value

    let EXPLOSION_POOL_SIZE = 10

    let explosion_pool: Explosion array =
        Array.init
            EXPLOSION_POOL_SIZE
            (fun _ -> Explosion.Nothing)

    let mutable explosion_pool_pointer = 0

    let add_note_explosion (column: int, color: int) =
        explosion_pool.[explosion_pool_pointer] <-
            {
                Column = column
                Color = color
                IsRelease = false
                Time = state.CurrentChartTime()
            }

        explosion_pool_pointer <- (explosion_pool_pointer + 1) % EXPLOSION_POOL_SIZE

    let add_release_explosion (column: int, color: int) =
        explosion_pool.[explosion_pool_pointer] <-
            {
                Column = column
                Color = color
                IsRelease = true
                Time = state.CurrentChartTime()
            }

        explosion_pool_pointer <- (explosion_pool_pointer + 1) % EXPLOSION_POOL_SIZE

    let rotation = Skins.note_rotation keys

    let column_width = ns.ColumnWidth

    let column_positions =
        let column_spacing = ns.KeymodeColumnSpacing keys
        let mutable x = 0.0f

        Array.init
            keys
            (fun i ->
                let v = x

                if i + 1 < keys then
                    x <- x + column_width + column_spacing.[i]

                v
            )

    let handle_event (ev: HitEvent<HitEventGuts>) =
        match ev.Guts with
        | Hit e when (e.IsHold && not e.Missed) ->
            hold_colors.[ev.Column] <-
                match ns.HoldExplosionSettings.Colors with
                | ExplosionColors.Note -> int state.WithColors.Colors.[ev.Index].Data.[ev.Column]
                | ExplosionColors.Judgements -> e.Judgement |> Option.defaultValue -1

            holding.[ev.Column] <- true
            holding_since.[ev.Column] <- state.CurrentChartTime()

        | Hit e when not e.Missed ->
            let color =
                match ns.NoteExplosionSettings.Colors with
                | ExplosionColors.Note -> int state.WithColors.Colors.[ev.Index].Data.[ev.Column]
                | ExplosionColors.Judgements -> e.Judgement |> Option.defaultValue -1

            add_note_explosion (ev.Column, color)

        | Release e when holding.[ev.Column] ->
            let color =
                match ns.HoldExplosionSettings.Colors with
                | ExplosionColors.Note -> hold_colors.[ev.Column]
                | ExplosionColors.Judgements -> e.Judgement |> Option.defaultValue -1

            add_release_explosion (ev.Column, color)
            holding.[ev.Column] <- false
        | _ -> ()

    do
        state.SubscribeToHits handle_event

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        for k = 0 to (keys - 1) do
            if holding.[k] && state.Scoring.KeyState |> Bitmask.has_key k |> not then
                let color =
                    match ns.HoldExplosionSettings.Colors with
                    | ExplosionColors.Note -> hold_colors.[k]
                    | ExplosionColors.Judgements -> -1

                add_release_explosion (k, color)
                holding.[k] <- false

    override this.Draw() =
        let now = state.CurrentChartTime()

        if now < last_time then
            for i = 0 to EXPLOSION_POOL_SIZE - 1 do
                explosion_pool.[i] <- Explosion.Nothing
            for k = 0 to keys - 1 do
                holding.[k] <- false

        last_time <- now

        // hold animations
        for k = 0 to keys - 1 do
            if not holding.[k] then
                ()
            else

                let frame =
                    float32 (now - holding_since.[k])
                    / SelectedChart.rate.Value
                    / float32 ns.HoldExplosionSettings.AnimationFrameTime
                    |> floor
                    |> int

                let bounds =
                    (if options.Upscroll.Value then
                         Rect
                             .Box(this.Bounds.Left + column_positions.[k], this.Bounds.Top + options.HitPosition.Value, column_width, column_width)
                             .Translate(0.0f, column_width * ns.HoldExplosionSettings.Offset)
                     else
                         Rect
                             .Box(
                                 this.Bounds.Left + column_positions.[k],
                                 this.Bounds.Bottom - column_width - options.HitPosition.Value,
                                 column_width,
                                 column_width
                             )
                             .Translate(0.0f, -column_width * ns.HoldExplosionSettings.Offset))
                        .Expand((ns.HoldExplosionSettings.Scale - 1.0f) * column_width * 0.5f)

                Draw.quad
                    (bounds.AsQuad |> rotation k)
                    Color.White.AsQuad
                    (Sprite.pick_texture (frame, hold_colors.[k]) hold_explosion)

        for i = 0 to EXPLOSION_POOL_SIZE - 1 do

            let ex = explosion_pool.[i]

            // release animations
            if ex.IsRelease then

                let percent_remaining = (1.0f - (now - ex.Time) / release_duration) |> min 1.0f

                if percent_remaining <= 0.0f then
                    ()
                else

                    let frame =
                        float32 (now - ex.Time)
                        / SelectedChart.rate.Value
                        / float32 ns.HoldExplosionSettings.AnimationFrameTime
                        |> floor
                        |> int

                    let expand =
                        if ns.HoldExplosionSettings.UseBuiltInAnimation then
                            1.0f - percent_remaining
                        else
                            0.0f

                    let alpha =
                        if ns.HoldExplosionSettings.UseBuiltInAnimation then
                            255.0f * percent_remaining |> int |> max 0 |> min 255
                        else
                            255

                    let bounds =
                        (if options.Upscroll.Value then
                             Rect
                                 .Box(
                                     this.Bounds.Left + column_positions.[ex.Column],
                                     this.Bounds.Top + options.HitPosition.Value,
                                     column_width,
                                     column_width
                                 )
                                 .Translate(0.0f, column_width * ns.HoldExplosionSettings.Offset)
                         else
                             Rect
                                 .Box(
                                     this.Bounds.Left + column_positions.[ex.Column],
                                     this.Bounds.Bottom - column_width - options.HitPosition.Value,
                                     column_width,
                                     column_width
                                 )
                                 .Translate(0.0f, -column_width * ns.HoldExplosionSettings.Offset))
                            .Expand((ns.HoldExplosionSettings.Scale - 1.0f) * column_width * 0.5f)
                            .Expand(ns.HoldExplosionSettings.ExpandAmount * expand * column_width)

                    Draw.quad
                        (bounds.AsQuad |> rotation ex.Column)
                        (Color.White.O4a alpha).AsQuad
                        (Sprite.pick_texture (frame, ex.Color) release_explosion)

            // tap animations
            else

                let percent_remaining = (1.0f - (now - ex.Time) / note_duration) |> min 1.0f

                if percent_remaining < 0.0f then
                    ()
                else

                    let frame =
                        float32 (now - ex.Time)
                        / SelectedChart.rate.Value
                        / float32 ns.NoteExplosionSettings.AnimationFrameTime
                        |> floor
                        |> int

                    let expand =
                        if ns.NoteExplosionSettings.UseBuiltInAnimation then
                            1.0f - percent_remaining
                        else
                            0.0f

                    let alpha =
                        if ns.NoteExplosionSettings.UseBuiltInAnimation then
                            255.0f * percent_remaining |> int |> max 0 |> min 255
                        else
                            255

                    let bounds =
                        (if options.Upscroll.Value then
                             Rect
                                 .Box(
                                     this.Bounds.Left + column_positions.[ex.Column],
                                     this.Bounds.Top + options.HitPosition.Value,
                                     column_width,
                                     column_width
                                 )
                                 .Translate(0.0f, column_width * ns.NoteExplosionSettings.Offset)
                         else
                             Rect
                                 .Box(
                                     this.Bounds.Left + column_positions.[ex.Column],
                                     this.Bounds.Bottom - column_width - options.HitPosition.Value,
                                     column_width,
                                     column_width
                                 )
                                 .Translate(0.0f, -column_width * ns.NoteExplosionSettings.Offset))
                            .Expand((ns.NoteExplosionSettings.Scale - 1.0f) * column_width * 0.5f)
                            .Expand(ns.NoteExplosionSettings.ExpandAmount * expand * column_width)

                    Draw.quad
                        (bounds.AsQuad |> rotation ex.Column)
                        (Color.White.O4a alpha).AsQuad
                        (Sprite.pick_texture (frame, ex.Color) note_explosion)