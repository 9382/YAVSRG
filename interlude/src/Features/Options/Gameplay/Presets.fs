﻿namespace Interlude.Features.OptionsMenu.Gameplay

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude
open Interlude.Options
open Interlude.UI
open Interlude.UI.Menu

type private PresetKeymodeCheckbox(preset_id: int, keymode: int) as this =
    inherit Container(NodeType.Container(fun () -> Some this.Button))

    let old_value =
        match options.KeymodePreferredPresets.[keymode - 3] with
        | Some i when i = preset_id -> None
        | x -> x

    let button =
        Button(
            sprintf "%iK" keymode,
            fun () ->
                if options.KeymodePreferredPresets.[keymode - 3] = Some preset_id then
                    options.KeymodePreferredPresets.[keymode - 3] <- old_value
                else
                    options.KeymodePreferredPresets.[keymode - 3] <- Some preset_id
        )

    member private this.Button = button

    override this.Init(parent) =
        this |* button
        base.Init parent

    override this.Draw() =
        if this.Focused then
            Draw.rect (this.Bounds.Shrink(5.0f)) Colors.yellow_accent.O1

        base.Draw()

        if options.KeymodePreferredPresets.[keymode - 3] = Some preset_id then
            Draw.rect (this.Bounds.SliceBottom(5.0f)) Colors.yellow_accent

type private EditPresetPage(preset_id: int, setting: Setting<Preset option>) =
    inherit Page()

    let mutable delete = false

    let delete_button =
        PageButton(
            %"gameplay.preset.delete",
            fun () ->
                delete <- true
                Menu.Back()
        )

    let preset = setting.Value.Value
    let name = Setting.simple preset.Name

    let mode =
        Setting.simple preset.Mode
        |> Setting.trigger (fun mode -> delete_button.Enabled <- mode <> PresetMode.Locked)

    override this.Content() =
        let keymode_preference =
            FlowContainer.LeftToRight<PresetKeymodeCheckbox>(100.0f, Spacing = 10.0f)

        for keymode = 3 to 10 do
            keymode_preference.Add(PresetKeymodeCheckbox(preset_id, keymode))

        page_container()
        |+ PageTextEntry(%"gameplay.preset.name", name).Pos(0)
        |+ PageSetting(
            %"gameplay.preset.mode",
            SelectDropdown<PresetMode>(
                [|
                    PresetMode.Unlocked, %"gameplay.preset.mode.unlocked"
                    PresetMode.Locked, %"gameplay.preset.mode.locked"
                    PresetMode.Autosave, %"gameplay.preset.mode.autosave"
                |],
                mode
            )
        )
            .Tooltip(Tooltip.Info("gameplay.preset.mode"))
            .Pos(2)
        |+ PageSetting(%"gameplay.preset.keymode_preference", keymode_preference)
            .Tooltip(Tooltip.Info("gameplay.preset.keymode_preference"))
            .Pos(4, 2, PageWidth.Custom (PRETTYTEXTWIDTH + (keymode_preference :> IWidth).Width))
        |+ delete_button.Pos(6)
        :> Widget

    override this.Title = preset.Name

    override this.OnClose() =
        if delete then
            setting.Set None
        else
            setting.Set(
                Some
                    { preset with
                        Name = name.Value
                        Mode = mode.Value
                    }
            )

module private Presets =

    let preset_buttons (preset_id: int) (setting: Setting<Preset option>) (refresh_preview: unit -> unit) =
        Container(
            NodeType.None,
            Position = Position.Box(1.0f, 1.0f, -1200.0f + float32 preset_id * 300.0f, -90.0f, 290.0f, 80.0f)
        )
        |+ Conditional(
            (fun () ->
                options.SelectedPreset.Value = Some preset_id
                && match setting.Value with
                   | Some p -> p.Mode = PresetMode.Autosave
                   | None -> false
            ),
            Text(
                sprintf "%s %s" Icons.REFRESH_CW (%"gameplay.preset.autosaving"),
                Color = K Colors.text_green,
                Position = Position.SliceBottom(40.0f).Margin(10.0f, 0.0f)
            )
        )
        |+ Button(
            (fun () ->
                match setting.Value with
                | None -> sprintf "Preset %i (Empty)" preset_id
                | Some s -> Icons.EDIT_2 + " " + s.Name
            ),
            (fun () ->
                match setting.Value with
                | Some s ->
                    let needs_confirmation =
                        match options.SelectedPreset.Value with
                        | None -> true
                        | Some i when preset_id = i -> false
                        | Some i ->
                            match (Presets.get i).Value with
                            | Some p -> p.Mode <> PresetMode.Autosave
                            | None -> true

                    if needs_confirmation then
                        ConfirmPage(
                            [ s.Name ] %> "gameplay.preset.load.prompt",
                            fun () ->
                                Presets.load preset_id |> ignore
                                refresh_preview()
                                EditPresetPage(preset_id, setting).Show()
                        )
                            .Show()
                    else
                        Presets.load preset_id |> ignore
                        refresh_preview()
                        EditPresetPage(preset_id, setting).Show()
                | None -> ()
            ),
            Disabled = (fun () -> setting.Value.IsNone),
            Position = Position.SliceTop(40.0f)
        )

        |+ Conditional(
            (fun () ->
                options.SelectedPreset.Value <> Some preset_id
                || match setting.Value with
                   | Some p -> p.Mode <> PresetMode.Autosave
                   | None -> true
            ),
            Button(
                %"gameplay.preset.load",
                (fun () ->
                    match setting.Value with
                    | Some s ->
                        let needs_confirmation =
                            match options.SelectedPreset.Value with
                            | None -> true
                            | Some i ->
                                match (Presets.get i).Value with
                                | Some p -> p.Mode <> PresetMode.Autosave
                                | None -> true

                        if needs_confirmation then
                            ConfirmPage(
                                [ s.Name ] %> "gameplay.preset.load.prompt",
                                fun () ->
                                    Presets.load preset_id |> ignore
                                    refresh_preview()

                                    Notifications.action_feedback (
                                        Icons.ALERT_OCTAGON,
                                        %"notification.preset_loaded",
                                        s.Name
                                    )
                            )
                                .Show()
                        else
                            Presets.load preset_id |> ignore
                            refresh_preview()
                            Notifications.action_feedback (Icons.ALERT_OCTAGON, %"notification.preset_loaded", s.Name)
                    | None -> ()
                ),
                Disabled = (fun () -> setting.Value.IsNone),
                Position =
                    { Position.SliceBottom(40.0f) with
                        Right = 0.5f %+ 0.0f
                    }
                        .Margin(40.0f, 0.0f)
            )
        )
        |+ Conditional(
            (fun () ->
                options.SelectedPreset.Value <> Some preset_id
                || match setting.Value with
                   | Some p -> p.Mode <> PresetMode.Autosave
                   | None -> true
            ),
            Button(
                %"gameplay.preset.save",
                (fun () ->
                    match setting.Value with
                    | None ->
                        let name = sprintf "Preset %i" preset_id
                        setting.Value <- Presets.create (name) |> Some
                        Notifications.action_feedback (Icons.ALERT_OCTAGON, %"notification.preset_saved", name)
                    | Some existing ->
                        ConfirmPage(
                            [ existing.Name ] %> "gameplay.preset.save.prompt",
                            fun () ->
                                setting.Value <- Presets.save existing |> Some

                                Notifications.action_feedback (
                                    Icons.ALERT_OCTAGON,
                                    %"notification.preset_saved",
                                    existing.Name
                                )
                        )
                            .Show()
                ),
                Disabled =
                    (fun () ->
                        match setting.Value with
                        | Some s -> s.Mode <> PresetMode.Unlocked
                        | None -> false
                    ),
                Position =
                    { Position.SliceBottom(40.0f) with
                        Left = 0.5f %+ 0.0f
                    }
                        .Margin(40.0f, 0.0f)
            )
        )