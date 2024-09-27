﻿namespace Interlude.Features.Toolbar

open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Audio
open Prelude
open Interlude.UI
open Interlude.Features.Gameplay
open Interlude.Features.LevelSelect

type Jukebox() =
    inherit Container(NodeType.None)

    let song_title_carousel = Animation.Counter(10000000.0)
    let CAROUSEL_SPEED = 0.05
    let CAROUSEL_GAP = 50.0f

    override this.Init(parent) =
        this 
        |+ Button(Icons.SKIP_BACK, 
            LevelSelect.History.back,
            Hotkey = "previous_random_chart",
            Disabled = (fun () -> Screen.current_type = Screen.Type.Lobby || not (LevelSelect.History.can_go_back())),
            Position = Position.Shrink(5.0f).SliceL(45.0f)
        )
        |+ Button(Icons.PAUSE, 
            (fun () -> if Song.playing() then Song.pause() else Song.resume()),
            Hotkey = "pause_music",
            Position = Position.Shrink(5.0f).SliceL(45.0f).Translate(45.0f, 0.0f)
        )
        |+ HotkeyAction("random_chart", (fun () -> if Screen.current_type <> Screen.Type.Lobby then LevelSelect.random_chart()))
        |* Button(Icons.SKIP_FORWARD,
            (fun () -> if LevelSelect.History.can_go_forward() then LevelSelect.History.forward() else LevelSelect.random_chart()),
            Disabled = (fun () -> Screen.current_type = Screen.Type.Lobby),
            Position = Position.Shrink(5.0f).SliceL(45.0f).Translate(90.0f, 0.0f)
        )
        base.Init parent

    override this.Draw() =
        Draw.rect this.Bounds (Colors.shadow_1.O2)

        let carousel_bounds = this.Bounds.ShrinkL(145.0f).Shrink(Style.PADDING)

        base.Draw()

        let song_title = 
            match SelectedChart.CACHE_DATA with 
            | Some cc -> cc.Artist + " - " + cc.Title
            | None -> %"jukebox.no_chart_selected"

        let song_title_width = Text.measure(Style.font, song_title) * 25.0f

        Stencil.start_stencilling false
        Draw.rect carousel_bounds Color.Transparent
        Stencil.start_drawing ()
        
        if song_title_width + CAROUSEL_GAP < carousel_bounds.Width then
            Text.draw_b(Style.font, song_title, 25.0f, carousel_bounds.Left + (carousel_bounds.Width - song_title_width) * 0.5f, this.Bounds.Top + 7.0f, Colors.text_subheading)
        else
            let x = float32 (song_title_carousel.Time * CAROUSEL_SPEED) % (song_title_width + CAROUSEL_GAP)
            Text.draw_b(Style.font, song_title, 25.0f, carousel_bounds.Left - x, this.Bounds.Top + 7.0f, Colors.text_subheading)
            Text.draw_b(Style.font, song_title, 25.0f, carousel_bounds.Left + song_title_width + CAROUSEL_GAP - x, this.Bounds.Top + 7.0f, Colors.text_subheading)

        Stencil.finish()


    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)
        song_title_carousel.Update elapsed_ms

