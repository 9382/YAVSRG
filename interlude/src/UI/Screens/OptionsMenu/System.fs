﻿namespace Interlude.UI.OptionsMenu

open OpenTK
open Percyqaz.Common
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Windowing
open Prelude.Common
open Interlude.Options
open Interlude.UI.Components.Selection
open Interlude.UI.Components.Selection.Controls
open Interlude.UI.Components.Selection.Menu

module System =

    type SystemPage(m) as this =
        inherit Page(m)

        do
            this.Content(
                column()
                |+ PrettySetting("system.visualoffset", Percyqaz.Flux.UI.Slider<float>(options.VisualOffset, 0.01f)).Pos(200.0f)
                |+ PrettySetting("system.audiooffset",
                        { new Percyqaz.Flux.UI.Slider<float>(options.AudioOffset, 0.01f)
                            with override this.OnDeselected() = base.OnDeselected(); Song.changeGlobalOffset (float32 options.AudioOffset.Value * 1.0f<ms>) }
                    ).Pos(280.0f)

                |+ PrettySetting("system.audiovolume",
                        Percyqaz.Flux.UI.Slider<_>.Percent(options.AudioVolume |> Setting.trigger Devices.changeVolume, 0.01f)
                    ).Pos(380.0f)
                |+ PrettySetting("system.audiodevice", Percyqaz.Flux.UI.Selector(Array.ofSeq(Devices.list()), Setting.trigger Devices.change config.AudioDevice)).Pos(460.0f, 1700.0f)

                // todo: way to edit resolution settings?
                |+ PrettySetting("system.windowmode", Percyqaz.Flux.UI.Selector.FromEnum config.WindowMode).Pos(560.0f)
                |+ PrettySetting("system.framelimit", Percyqaz.Flux.UI.Selector.FromEnum config.FrameLimit).Pos(640.0f)
                |+ PrettySetting("system.monitor", Percyqaz.Flux.UI.Selector(Window.monitors, config.Display)).Pos(720.0f)
            )

        override this.OnClose() = Window.apply_config <- Some config
        override this.Title = N"system"