﻿namespace Interlude.Features.OptionsMenu.Noteskins

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Data.Content
open Interlude.Content
open Interlude.Utils
open Interlude.UI.Components
open Interlude.UI.Menu

type AnimationSettingsPage() as this =
    inherit Page()

    let data = Content.NoteskinConfig

    let note = Content.Texture "note"
    let noteexplosion = Content.Texture "noteexplosion"
    let holdexplosion = Content.Texture "holdexplosion"
    let releaseexplosion = Content.Texture "releaseexplosion"
    let receptor = Content.Texture "receptor"
    let columnlighting = Content.Texture "receptorlighting"
        
    let mutable holding = false
    let test_events = Animation.Counter 1000.0
    let f_note = Animation.Counter(data.AnimationFrameTime)

    let t_columnlight = Animation.Delay(data.HoldExplosionSettings.Duration)

    let f_note_ex = Animation.Counter(data.NoteExplosionSettings.AnimationFrameTime)
    let t_note_ex = Animation.Delay(data.NoteExplosionSettings.Duration)

    let f_hold_ex = Animation.Counter(data.HoldExplosionSettings.AnimationFrameTime)
    let t_hold_ex = Animation.Delay(data.HoldExplosionSettings.Duration)
    
    let f_release_ex = Animation.Counter(data.ReleaseExplosionSettings.AnimationFrameTime)
    let t_release_ex = Animation.Delay(data.ReleaseExplosionSettings.Duration)

    let note_animation_time = Setting.bounded data.AnimationFrameTime 10.0 1000.0 |> Setting.round 0 |> Setting.trigger f_note.set_Interval
    let enable_column_light = Setting.simple data.EnableColumnLight
    let column_light_duration = Setting.bounded data.ColumnLightDuration 0.0 1000.0 |> Setting.round 0 |> Setting.trigger t_columnlight.set_Interval

    let enable_explosions = Setting.simple data.UseExplosions

    let explosion_frame_time_note = Setting.bounded data.NoteExplosionSettings.AnimationFrameTime 10.0 1000.0 |> Setting.round 0 |> Setting.trigger f_note_ex.set_Interval
    let explosion_colors_note = Setting.simple data.NoteExplosionSettings.Colors
    let explosion_on_miss_note = Setting.simple data.NoteExplosionSettings.ExplodeOnMiss
    let explosion_builtin_note = Setting.simple data.NoteExplosionSettings.UseBuiltInAnimation
    let explosion_duration_note = Setting.bounded data.NoteExplosionSettings.Duration 50.0 1000 |> Setting.round 0 |> Setting.trigger t_note_ex.set_Interval
    let explosion_scale_note = Setting.bounded data.NoteExplosionSettings.Scale 0.5f 2.0f
    let explosion_expand_note = Setting.percentf data.NoteExplosionSettings.ExpandAmount

    let explosion_frame_time_hold = Setting.bounded data.HoldExplosionSettings.AnimationFrameTime 10.0 1000.0 |> Setting.round 0 |> Setting.trigger f_hold_ex.set_Interval
    let explosion_colors_hold = Setting.simple data.HoldExplosionSettings.Colors
    let explosion_on_miss_hold = Setting.simple data.HoldExplosionSettings.ExplodeOnMiss
    let explosion_builtin_hold = Setting.simple data.HoldExplosionSettings.UseBuiltInAnimation
    let explosion_duration_hold = Setting.bounded data.HoldExplosionSettings.Duration 50.0 1000 |> Setting.round 0 |> Setting.trigger t_hold_ex.set_Interval
    let explosion_scale_hold = Setting.bounded data.HoldExplosionSettings.Scale 0.5f 2.0f
    let explosion_expand_hold = Setting.percentf data.HoldExplosionSettings.ExpandAmount
    
    let explosion_frame_time_release = Setting.bounded data.ReleaseExplosionSettings.AnimationFrameTime 10.0 1000.0 |> Setting.round 0 |> Setting.trigger f_release_ex.set_Interval
    let explosion_colors_release = Setting.simple data.ReleaseExplosionSettings.Colors
    let explosion_on_miss_release = Setting.simple data.ReleaseExplosionSettings.ExplodeOnMiss
    let explosion_builtin_release = Setting.simple data.ReleaseExplosionSettings.UseBuiltInAnimation
    let explosion_duration_release = Setting.bounded data.ReleaseExplosionSettings.Duration 50.0 1000 |> Setting.round 0 |> Setting.trigger t_release_ex.set_Interval
    let explosion_scale_release = Setting.bounded data.ReleaseExplosionSettings.Scale 0.5f 2.0f
    let explosion_expand_release = Setting.percentf data.ReleaseExplosionSettings.ExpandAmount

    do
        let general_tab =
            let pos = menu_pos 3.0f
            NavigationContainer.Column<Widget>(WrapNavigation = false)
            |+ PageSetting("noteskins.animations.enablecolumnlight", Selector<_>.FromBool enable_column_light)
                .Tooltip(Tooltip.Info("noteskins.animations.enablecolumnlight"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.columnlighttime", Slider(column_light_duration |> Setting.f32, Step = 1f))
                .Tooltip(Tooltip.Info("noteskins.animations.columnlighttime"))
                .Pos(pos.Step 1.5f)
            |+ PageSetting(
                "noteskins.animations.animationtime",
                Slider(
                    note_animation_time
                    |> Setting.f32,
                    Step = 1f
                )
            )
                .Tooltip(Tooltip.Info("noteskins.animations.animationtime"))
                .Pos(pos.Step 1.5f)
            |+ PageSetting("noteskins.animations.enableexplosions", Selector<_>.FromBool enable_explosions)
                .Tooltip(Tooltip.Info("noteskins.animations.enableexplosions"))
                .Pos(pos.Step())

        let note_explosion_tab =
            let pos = menu_pos 3.0f
            NavigationContainer.Column<Widget>(WrapNavigation = false)
            |+ PageSetting(
                "noteskins.animations.explosionanimationtime",
                Slider(explosion_frame_time_note |> Setting.f32, Step = 1f)
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosionanimationtime"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.explodeonmiss", Selector<_>.FromBool(explosion_on_miss_note))
                .Tooltip(Tooltip.Info("noteskins.animations.explodeonmiss"))
                .Pos(pos.Step())
            |+ PageSetting(
                "noteskins.animations.explosioncolors",
                Selector(
                    [| ExplosionColors.Note, "Note"; ExplosionColors.Judgements, "Judgements" |],
                    explosion_colors_note
                )
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosioncolors"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.usebuiltinanimation", Selector<_>.FromBool explosion_builtin_note)
                .Tooltip(Tooltip.Info("noteskins.animations.usebuiltinanimation"))
                .Pos(pos.Step())
            |+ Conditional(explosion_builtin_note.Get, 
                PageSetting("noteskins.animations.explosionduration", Slider(explosion_duration_note |> Setting.f32, Step = 1f))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionduration"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_note.Get, 
                PageSetting("noteskins.animations.explosionscale", Slider.Percent(explosion_scale_note))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionscale"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_note.Get, 
                PageSetting("noteskins.animations.explosionexpand", Slider.Percent(explosion_expand_note))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionexpand"))
                    .Pos(pos.Step())
            )

        let hold_explosion_tab =
            let pos = menu_pos 3.0f
            NavigationContainer.Column<Widget>(WrapNavigation = false)
            |+ PageSetting(
                "noteskins.animations.explosionanimationtime",
                Slider(explosion_frame_time_hold |> Setting.f32, Step = 1f)
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosionanimationtime"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.explodeonmiss", Selector<_>.FromBool(explosion_on_miss_hold))
                .Tooltip(Tooltip.Info("noteskins.animations.explodeonmiss"))
                .Pos(pos.Step())
            |+ PageSetting(
                "noteskins.animations.explosioncolors",
                Selector(
                    [| ExplosionColors.Note, "Note"; ExplosionColors.Judgements, "Judgements" |],
                    explosion_colors_hold
                )
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosioncolors"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.usebuiltinanimation", Selector<_>.FromBool explosion_builtin_hold)
                .Tooltip(Tooltip.Info("noteskins.animations.usebuiltinanimation"))
                .Pos(pos.Step())
            |+ Conditional(explosion_builtin_hold.Get, 
                PageSetting("noteskins.animations.explosionduration", Slider(explosion_duration_hold |> Setting.f32, Step = 1f))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionduration"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_hold.Get, 
                PageSetting("noteskins.animations.explosionscale", Slider.Percent(explosion_scale_hold))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionscale"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_hold.Get, 
                PageSetting("noteskins.animations.explosionexpand", Slider.Percent(explosion_expand_hold))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionexpand"))
                    .Pos(pos.Step())
            )

        let release_explosion_tab =
            let pos = menu_pos 3.0f
            NavigationContainer.Column<Widget>(WrapNavigation = false)
            |+ PageSetting(
                "noteskins.animations.explosionanimationtime",
                Slider(explosion_frame_time_release |> Setting.f32, Step = 1f)
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosionanimationtime"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.explodeonmiss", Selector<_>.FromBool(explosion_on_miss_release))
                .Tooltip(Tooltip.Info("noteskins.animations.explodeonmiss"))
                .Pos(pos.Step())
            |+ PageSetting(
                "noteskins.animations.explosioncolors",
                Selector(
                    [| ExplosionColors.Note, "Note"; ExplosionColors.Judgements, "Judgements" |],
                    explosion_colors_release
                )
            )
                .Tooltip(Tooltip.Info("noteskins.animations.explosioncolors"))
                .Pos(pos.Step())
            |+ PageSetting("noteskins.animations.usebuiltinanimation", Selector<_>.FromBool explosion_builtin_release)
                .Tooltip(Tooltip.Info("noteskins.animations.usebuiltinanimation"))
                .Pos(pos.Step())
            |+ Conditional(explosion_builtin_release.Get, 
                PageSetting("noteskins.animations.explosionduration", Slider(explosion_duration_release |> Setting.f32, Step = 1f))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionduration"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_release.Get, 
                PageSetting("noteskins.animations.explosionscale", Slider.Percent(explosion_scale_release))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionscale"))
                    .Pos(pos.Step())
            )
            |+ Conditional(explosion_builtin_release.Get, 
                PageSetting("noteskins.animations.explosionexpand", Slider.Percent(explosion_expand_release))
                    .Tooltip(Tooltip.Info("noteskins.animations.explosionexpand"))
                    .Pos(pos.Step())
            )

        let tabs = SwapContainer(general_tab)
        let tab_buttons = 
            RadioButtons.create {
                Setting = Setting.make tabs.set_Current tabs.get_Current
                Options =
                    [|
                        general_tab, "General", K false
                        note_explosion_tab, "Note explosions", (fun () -> not enable_explosions.Value)
                        hold_explosion_tab, "Hold explosions", (fun () -> not enable_explosions.Value)
                        release_explosion_tab, "Release explosions", (fun () -> not enable_explosions.Value || explosion_builtin_hold.Value)
                    |]
                Height = 50.0f
            }
        tab_buttons.Position <- Position.Box(0.0f, 0.0f, 100.0f, 200.0f, PRETTYWIDTH, PRETTYHEIGHT)

        NavigationContainer.Column<Widget>()
        |+ tab_buttons
        |+ tabs
        |> this.Content

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        f_note.Update elapsed_ms
        f_note_ex.Update elapsed_ms
        f_hold_ex.Update elapsed_ms
        f_release_ex.Update elapsed_ms

        t_columnlight.Update elapsed_ms
        t_note_ex.Update elapsed_ms
        t_hold_ex.Update elapsed_ms
        t_release_ex.Update elapsed_ms
        t_columnlight.Update elapsed_ms

        test_events.Update elapsed_ms

        if holding <> (test_events.Loops % 2 = 0) then
            holding <- test_events.Loops % 2 = 0

            if holding then
                t_note_ex.Reset()
                f_note_ex.Reset()
                f_hold_ex.Reset()
            else
                t_columnlight.Reset()
                t_hold_ex.Reset()
                t_release_ex.Reset()
                f_hold_ex.Reset()
                f_release_ex.Reset()

    override this.Draw() =
        base.Draw()

        let COLUMN_WIDTH = 120.0f
        let mutable left = this.Bounds.Right - 50.0f - COLUMN_WIDTH * 2.0f
        let mutable bottom = this.Bounds.Bottom - 50.0f - COLUMN_WIDTH

        // draw note explosion example
        Draw.quad
            (Rect.Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH).AsQuad)
            (Quad.color Color.White)
            (Sprite.pick_texture (f_note.Loops, 0) receptor)

        if enable_explosions.Value then

            let percent_remaining =
                if explosion_builtin_note.Value then
                    1.0 - (t_note_ex.Elapsed / explosion_duration_note.Value)
                    |> min 1.0
                    |> max 0.0
                    |> float32
                else
                    if f_note_ex.Loops > noteexplosion.Columns then 0.0f else 1.0f

            let a = 255.0f * percent_remaining |> int

            Draw.quad
                (Rect
                    .Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH)
                    .Expand((explosion_scale_note.Value - 1.0f) * COLUMN_WIDTH * 0.5f)
                    .Expand(explosion_expand_note.Value * (1.0f - percent_remaining) * COLUMN_WIDTH)
                    .AsQuad)
                (Quad.color (Color.White.O4a a))
                (Sprite.pick_texture (f_note_ex.Loops, 0) noteexplosion)

        // draw hold explosion example
        bottom <- bottom - COLUMN_WIDTH * 2.0f

        Draw.quad
            (Rect.Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH).AsQuad)
            (Quad.color Color.White)
            (Sprite.pick_texture (f_note.Loops, if holding then 1 else 0) receptor)

        if enable_explosions.Value then

            if explosion_builtin_hold.Value || holding then

                let percent_remaining =
                    if holding then
                        1.0f 
                    else
                        1.0 - (t_hold_ex.Elapsed / explosion_duration_hold.Value)
                        |> min 1.0
                        |> max 0.0
                        |> float32

                let a = 255.0f * percent_remaining |> int

                Draw.quad
                    (Rect
                        .Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH)
                        .Expand((explosion_scale_hold.Value - 1.0f) * COLUMN_WIDTH * 0.5f)
                        .Expand(explosion_expand_hold.Value * (1.0f - percent_remaining) * COLUMN_WIDTH)
                        .AsQuad)
                    (Quad.color (Color.White.O4a a))
                    (Sprite.pick_texture (f_hold_ex.Loops, 0) holdexplosion)

            else
        
                let percent_remaining =
                    if explosion_builtin_release.Value then
                        1.0 - (t_release_ex.Elapsed / explosion_duration_release.Value)
                        |> min 1.0
                        |> max 0.0
                        |> float32
                    else
                        if f_release_ex.Loops > releaseexplosion.Columns then 0.0f else 1.0f

                let a = 255.0f * percent_remaining |> int

                Draw.quad
                    (Rect
                        .Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH)
                        .Expand((explosion_scale_release.Value - 1.0f) * COLUMN_WIDTH * 0.5f)
                        .Expand(explosion_expand_release.Value * (1.0f - percent_remaining) * COLUMN_WIDTH)
                        .AsQuad)
                    (Quad.color (Color.White.O4a a))
                    (Sprite.pick_texture (f_release_ex.Loops, 0) releaseexplosion)

        // draw note animation example
        bottom <- bottom - COLUMN_WIDTH * 2.0f

        Draw.quad
            (Rect.Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH).AsQuad)
            (Quad.color Color.White)
            (Sprite.pick_texture (f_note.Loops, 0) note)

        // draw column light example
        bottom <- bottom + COLUMN_WIDTH * 4.0f
        left <- left - COLUMN_WIDTH * 1.5f

        Draw.quad
            (Rect.Box(left, bottom - COLUMN_WIDTH, COLUMN_WIDTH, COLUMN_WIDTH).AsQuad)
            (Quad.color Color.White)
            (Sprite.pick_texture (f_note.Loops, if holding then 1 else 0) receptor)

        if enable_column_light.Value then

            let percent_remaining =
                if holding then 1.0f else
                    1.0 - (t_columnlight.Elapsed / t_columnlight.Interval)
                    |> min 1.0
                    |> max 0.0
                    |> float32

            let a = 255.0f * percent_remaining |> int |> min 255 |> max 0

            Draw.sprite
                (Sprite.aligned_box_x
                    (left + COLUMN_WIDTH * 0.5f, bottom, 0.5f, 1.0f, COLUMN_WIDTH * percent_remaining, 1.0f / percent_remaining)
                    columnlighting)
                (Color.White.O4a a)
                columnlighting

    override this.Title = %"noteskins.animations.name"

    override this.OnClose() =
        Noteskins.save_config
            { Content.NoteskinConfig with
                EnableColumnLight = enable_column_light.Value
                ColumnLightDuration = column_light_duration.Value
                AnimationFrameTime = note_animation_time.Value
                UseExplosions = enable_explosions.Value
                NoteExplosionSettings =
                    {
                        AnimationFrameTime = explosion_frame_time_note.Value
                        Scale = explosion_scale_note.Value
                        Colors = explosion_colors_note.Value
                        ExplodeOnMiss = explosion_on_miss_note.Value
                        UseBuiltInAnimation = explosion_builtin_note.Value
                        Duration = explosion_duration_note.Value
                        ExpandAmount = explosion_expand_note.Value
                    }
            }
