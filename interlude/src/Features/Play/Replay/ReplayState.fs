﻿namespace Interlude.Features.Play.Replay

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Charts.Processing
open Prelude.Data.User
open Interlude.UI

[<RequireQualifiedAccess>]
type ReplayMode =
    | Auto of ColoredChart
    | Replay of score_info: ScoreInfo * with_colors: ColoredChart

[<AutoOpen>]
module private ReplayModeSettings =
    
    let show_input_overlay = Setting.simple false
    let show_hit_overlay = Setting.simple false
    let show_hit_overlay_labels = Setting.simple true
    let playfield_dim: Setting.Bounded<float32> = Setting.percentf 0.5f
    let fixed_scroll_speed = Setting.simple false

type private ReplayModeSettingsPage(on_close) =
    inherit Page()

    override this.Content() =
        page_container()
        |+ PageSetting(%"replay.input_overlay", Checkbox show_input_overlay).Pos(0)
        |+ PageSetting(%"replay.hit_overlay", Checkbox show_hit_overlay).Pos(2)
        |+ PageSetting(%"replay.hit_overlay_labels", Checkbox show_hit_overlay_labels)
            .Conditional(show_hit_overlay.Get)
            .Pos(4)
        |+ PageSetting(%"replay.playfield_dim", Slider.Percent playfield_dim)
            .Conditional(fun () -> show_input_overlay.Value || show_hit_overlay.Value)
            .Pos(6)
        |+ PageSetting(%"replay.fixed_scroll_speed", Checkbox fixed_scroll_speed)
            .Pos(9)
        :> Widget

    override this.Title = sprintf "%s %s" Icons.SETTINGS (%"replay.settings")
    override this.OnClose() = on_close ()