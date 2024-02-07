﻿namespace Interlude.Features.OptionsMenu.Gameplay

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Percyqaz.Flux.Audio
open Interlude.Options
open Interlude.UI.Menu
open Interlude.UI
open Interlude.Utils
open Interlude.Features

type GameplayKeybinder(keymode: Setting<Keymode>) as this =
    inherit StaticContainer(NodeType.Leaf)

    let mutable progress = 0

    let mutable text =
        options.GameplayBinds.[int keymode.Value - 3]
        |> Seq.map (sprintf "%O")
        |> String.concat ",  "

    let refresh_text () : unit =
        let binds = options.GameplayBinds.[int keymode.Value - 3]

        if not this.Selected then
            text <- binds |> Seq.map (sprintf "%O") |> String.concat ",  "
        else
            text <- ""

            for i = 0 to progress - 1 do
                text <- text + binds.[i].ToString() + ",  "

            text <- text + "..."

    let rec input_callback (b) =
        let binds = options.GameplayBinds.[int keymode.Value - 3]

        match b with
        | Key(k, _) ->
            binds.[progress] <- Key(k, (false, false, false))
            progress <- progress + 1

            if progress = int keymode.Value then
                this.Focus()
            else
                Input.listen_to_next_key input_callback

            refresh_text ()
            Style.key.Play()
        | _ -> Input.listen_to_next_key input_callback

    do
        this
        |+ Text(
            (fun () -> text),
            Color = (fun () -> (if this.Selected then Colors.yellow_accent else Colors.white), Colors.shadow_1),
            Align = Alignment.LEFT
        )
        |* Clickable(
            (fun () ->
                if not this.Selected then
                    this.Select()
            ),
            OnHover =
                fun b ->
                    if b then
                        this.Focus()
        )

    override this.OnFocus() =
        Style.hover.Play()
        base.OnFocus()

    override this.OnSelected() =
        base.OnSelected()
        progress <- 0
        refresh_text ()
        Style.click.Play()
        Input.listen_to_next_key input_callback

    override this.OnDeselected() =
        base.OnDeselected()
        Input.remove_listener ()

        text <-
            options.GameplayBinds.[int keymode.Value - 3]
            |> Seq.map (sprintf "%O")
            |> String.concat ",  "

    member this.OnKeymodeChanged() = refresh_text ()

type LanecoverPage() as this =
    inherit Page()

    let preview = NoteskinPreview(0.35f, true)

    do
        menu 2.0f
        |+ PageSetting("gameplay.lanecover.enabled", Selector<_>.FromBool options.LaneCover.Enabled)
        |. 0.5f
        |+ PageSetting("gameplay.lanecover.hidden", Slider.Percent(options.LaneCover.Hidden))
            .Tooltip(Tooltip.Info "gameplay.lanecover.hidden")
        |+ PageSetting("gameplay.lanecover.sudden", Slider.Percent(options.LaneCover.Sudden))
            .Tooltip(Tooltip.Info "gameplay.lanecover.sudden")
        |+ PageSetting("gameplay.lanecover.fadelength", Slider(options.LaneCover.FadeLength, Step = 5.0f))
            .Tooltip(Tooltip.Info "gameplay.lanecover.fadelength")
        |+ PageSetting("gameplay.lanecover.color", ColorPicker(options.LaneCover.Color, true), Height = PRETTYHEIGHT * 2.0f)
        |+ preview
        |>> this.Content

    override this.Title = %"gameplay.lanecover.name"
    override this.OnDestroy() = preview.Destroy()
    override this.OnClose() = ()

type PresetKeymodeCheckbox(preset_id: int, keymode: int) as this =
    inherit StaticContainer(NodeType.Switch(fun () -> this.Button))

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

type EditPresetPage(preset_id: int, setting: Setting<Preset option>) as this =
    inherit Page()

    let mutable delete = false

    let delete_button =
        PageButton(
            "gameplay.preset.delete",
            fun () ->
                delete <- true
                Menu.Back()
        )

    let preset = setting.Value.Value
    let name = Setting.simple preset.Name

    let mode =
        Setting.simple preset.Mode
        |> Setting.trigger (fun mode -> delete_button.Enabled <- mode <> PresetMode.Locked)

    do
        let keymode_preference =
            FlowContainer.LeftToRight<PresetKeymodeCheckbox>(100.0f, Spacing = 10.0f)

        for keymode = 3 to 10 do
            keymode_preference.Add(PresetKeymodeCheckbox(preset_id, keymode))

        menu 2.0f
        |+ PageTextEntry("gameplay.preset.name", name)
        |+ PageSetting(
            "gameplay.preset.mode",
            Selector<PresetMode>(
                [|
                    PresetMode.Unlocked, %"gameplay.preset.mode.unlocked"
                    PresetMode.Locked, %"gameplay.preset.mode.locked"
                    PresetMode.Autosave, %"gameplay.preset.mode.autosave"
                |],
                mode
            )
        )
            .Tooltip(Tooltip.Info("gameplay.preset.mode"))
        |+ PageSetting("gameplay.preset.keymode_preference", keymode_preference, Width = PRETTYTEXTWIDTH + 800.0f + 70.0f)
            .Tooltip(Tooltip.Info("gameplay.preset.keymode_preference"))
        |+ delete_button
        |>> this.Content

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

type GameplayPage() as this =
    inherit Page()

    let keymode: Setting<Keymode> =
        Setting.simple <| Gameplay.Chart.keymode()

    let binds = GameplayKeybinder(keymode)
    let preview = NoteskinPreview(0.35f, true)

    let preset_buttons (preset_id: int) (setting: Setting<Preset option>) =
        StaticContainer(
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
                                preview.Refresh()
                                EditPresetPage(preset_id, setting).Show()
                        )
                            .Show()
                    else
                        Presets.load preset_id |> ignore
                        preview.Refresh()
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
                                    preview.Refresh()

                                    Notifications.action_feedback (
                                        Icons.ALERT_OCTAGON,
                                        %"notification.preset_loaded",
                                        s.Name
                                    )
                            )
                                .Show()
                        else
                            Presets.load preset_id |> ignore
                            preview.Refresh()
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

    do
        menu 1.0f
        |+ PageSetting("gameplay.scrollspeed", Slider.Percent(options.ScrollSpeed))
            .Tooltip(Tooltip.Info("gameplay.scrollspeed"))
        |+ PageSetting("gameplay.hitposition", Slider(options.HitPosition, Step = 1f))
            .Tooltip(Tooltip.Info("gameplay.hitposition"))
        |+ PageSetting("gameplay.upscroll", Selector<_>.FromBool options.Upscroll)
            .Tooltip(Tooltip.Info("gameplay.upscroll"))
        |+ PageSetting("gameplay.backgrounddim", Slider.Percent(options.BackgroundDim))
            .Tooltip(Tooltip.Info("gameplay.backgrounddim"))
        |+ PageSetting(
            "system.audiooffset",
            { new Slider(options.AudioOffset, Step = 1f) with
                override this.OnDeselected() =
                    base.OnDeselected()
                    Song.set_global_offset (options.AudioOffset.Value * 1.0f<ms>)
            }
        )
            .Tooltip(Tooltip.Info("system.audiooffset"))
        |+ PageSetting("system.visualoffset", Slider(options.VisualOffset, Step = 1f))
            .Tooltip(Tooltip.Info("system.visualoffset"))
        |. 0.5f
        |+ PageSetting(
            "generic.keymode",
            Selector<_>
                .FromEnum(keymode |> Setting.trigger (ignore >> binds.OnKeymodeChanged))
        )
        |+ PageSetting("gameplay.keybinds", binds, Width = Viewport.vwidth - 200.0f)
            .Tooltip(Tooltip.Info("gameplay.keybinds"))
        |. 0.5f
        |+ PageButton("gameplay.lanecover", (fun () -> Menu.ShowPage LanecoverPage))
            .Tooltip(Tooltip.Info("gameplay.lanecover"))
        |+ PageButton("gameplay.pacemaker", (fun () -> Menu.ShowPage PacemakerPage))
            .Tooltip(Tooltip.Info("gameplay.pacemaker").Body(%"gameplay.pacemaker.hint"))
        |+ preview
        |+ preset_buttons 1 options.Preset1
        |+ preset_buttons 2 options.Preset2
        |+ preset_buttons 3 options.Preset3
        |>> this.Content

    override this.Title = %"gameplay.name"
    override this.OnDestroy() = preview.Destroy()
    override this.OnClose() = ()
