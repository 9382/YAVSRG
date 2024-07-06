﻿namespace Interlude.Features.OptionsMenu.SystemSettings

open Percyqaz.Common
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Windowing
open Percyqaz.Flux.UI
open Prelude
open Interlude.Options
open Interlude.UI
open Interlude.UI.Menu

type AudioPage() =
    inherit Page()

    override this.Content() =
        page_container()
        |+ PageSetting(
            %"system.audiovolume",
            Slider.Percent(
                options.AudioVolume
                |> Setting.trigger (fun v -> Devices.change_volume (v, v))
                |> Setting.f32
            )
        )
            .Tooltip(Tooltip.Info("system.audiovolume"))
            .Pos(0)
        |+ PageSetting(
            %"system.audiodevice",
            SelectDropdown(Array.ofSeq (Devices.list ()), Setting.trigger Devices.change config.AudioDevice)
        )
            .Tooltip(Tooltip.Info("system.audiodevice"))
            .Pos(2)
        |+ PageSetting(
            %"system.audio_pitch_rates",
            Checkbox(
                options.AudioPitchRates
                |> Setting.trigger (fun v -> Song.set_pitch_rates_enabled v)
            )
        )
            .Tooltip(Tooltip.Info("system.audio_pitch_rates"))
            .Pos(4)
        |+ PageSetting(
            %"system.audiooffset",
            { new Slider(options.AudioOffset, Step = 1f) with
                override this.OnDeselected(by_mouse: bool) =
                    base.OnDeselected by_mouse
                    Song.set_global_offset (options.AudioOffset.Value * 1.0f<ms>)
            }
        )
            .Tooltip(Tooltip.Info("system.audiooffset"))
            .Pos(6)
        |+ PageSetting(%"system.automatic_offset", Checkbox options.AutoCalibrateOffset)
            .Tooltip(Tooltip.Info("system.automatic_offset"))
            .Pos(8)
        :> Widget

    override this.OnClose() = ()

    override this.Title = Icons.SPEAKER + " " + %"system.audio"