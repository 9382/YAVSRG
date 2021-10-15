﻿namespace Interlude.UI

open OpenTK
open Prelude.Gameplay.NoteColors
open Prelude.Scoring
open Prelude.Scoring.Metrics
open Prelude.Common
open Prelude.Data.Charts
open Interlude
open Interlude.Graphics
open Interlude.Options
open Interlude.UI.Components
open Interlude.UI.Components.Selection
open Interlude.UI.Screens.LevelSelect
open Interlude.UI.Components.Selection.Containers
open Interlude.UI.Components.Selection.Buttons
open Interlude.UI.Components.Selection.Controls
open Interlude.UI.Components.Selection.Compound
open Interlude.UI.Components.Selection.Menu

(*
    Actual options menu structure/design data
*)

module OptionsMenu =
    
    let system() : SelectionPage =
        {
            Content = fun add ->
                column [
                    PrettySetting("AudioOffset",
                        { new Slider<float>(options.AudioOffset, 0.01f)
                            with override this.OnDeselect() = Audio.globalOffset <- float32 options.AudioOffset.Value * 1.0f<ms> }
                    ).Position(200.0f)

                    PrettySetting("AudioVolume",
                        new Slider<float>(options.AudioVolume |> Setting.trigger Audio.changeVolume, 0.01f)
                    ).Position(300.0f)

                    PrettySetting("WindowMode", Selector.FromEnum(config.WindowMode)).Position(400.0f)
                    //todo: way to edit resolution settings?
                    PrettySetting(
                        "FrameLimiter",
                        new Selector(
                            [|"UNLIMITED"; "30"; "60"; "90"; "120"; "240"|],
                            config.FrameLimiter.Value / 30.0
                            |> int
                            |> min 5
                            |> Setting.simple
                            |> Setting.trigger
                                (let e = [|0.0; 30.0; 60.0; 90.0; 120.0; 240.0|] in fun i -> config.FrameLimiter.Value <- e.[i]) )
                    ).Position(500.0f)
                ] :> Selectable
            Callback = Options.applyOptions
        }

    let themeChanger refresh : SelectionPage =
        Themes.refreshAvailableThemes()
        {
            Content = fun add ->
                column [
                    PrettySetting("ChooseTheme",
                        ListOrderedSelect.ListOrderedSelector(
                            Setting.make
                                ( fun v ->
                                    options.EnabledThemes.Clear()
                                    options.EnabledThemes.AddRange(v)
                                    Themes.loadThemes(options.EnabledThemes)
                                    Themes.changeNoteSkin(options.NoteSkin.Value)
                                    refresh()
                                )
                                (fun () -> options.EnabledThemes),
                            Themes.availableThemes
                        )
                    ).Position(200.0f, PRETTYWIDTH, 500.0f)
                    Divider().Position(750.0f)
                    PrettyButton("OpenThemeFolder",
                        fun () ->
                            //todo: move this to utils
                            let target = System.Diagnostics.ProcessStartInfo("file://" + System.IO.Path.GetFullPath(getDataPath("Themes")), UseShellExecute = true)
                            System.Diagnostics.Process.Start target |> ignore).Position(800.0f)
                    PrettyButton("NewTheme", fun () -> Dialog.add <| TextInputDialog(Render.bounds, "Enter theme name", Themes.createNew)).Position(900.0f)
                ] :> Selectable
            Callback = refresh
        }

    let themes() : SelectionPage =
        let keycount = Setting.simple options.KeymodePreference.Value
        
        let g keycount i =
            let k = if options.ColorStyle.Value.UseGlobalColors then 0 else int keycount - 2
            Setting.make
                (fun v -> options.ColorStyle.Value.Colors.[k].[i] <- v)
                (fun () -> options.ColorStyle.Value.Colors.[k].[i])

        let colors, refreshColors =
            refreshRow
                (fun () -> colorCount (int keycount.Value) options.ColorStyle.Value.Style)
                (fun i k ->
                    let x = -60.0f * float32 k
                    let n = float32 i
                    ColorPicker(g keycount.Value i)
                    |> positionWidget(x + 120.0f * n, 0.5f, 0.0f, 0.0f, x + 120.0f * n + 120.0f, 0.5f, 0.0f, 1.0f))

        let noteskins = PrettySetting("Noteskin", Selectable())
        let refreshNoteskins() =
            let ns = Themes.noteskins() |> Seq.toArray
            let ids = ns |> Array.map fst
            let names = ns |> Array.map (fun (id, data) -> data.Config.Name)
            options.NoteSkin.Value <- Themes.currentNoteSkin
            Selector.FromArray(names, ids, options.NoteSkin |> Setting.trigger (fun id -> Themes.changeNoteSkin id; refreshColors()))
            |> noteskins.Refresh
        refreshNoteskins()

        {
            Content = fun add ->
                column [
                    PrettyButton("ChangeTheme", fun () -> add ("ChangeTheme", themeChanger(fun () -> refreshColors(); refreshNoteskins()))).Position(200.0f)
                    PrettyButton("EditTheme", ignore).Position(300.0f)
                    PrettySetting("Keymode",
                        Selector.FromEnum<Keymode>(keycount |> Setting.trigger (ignore >> refreshColors))
                    ).Position(450.0f)
                    PrettySetting(
                        "ColorStyle",
                        Selector.FromEnum(
                            Setting.make
                                (fun v -> Setting.app (fun x -> { x with Style = v }) options.ColorStyle)
                                (fun () -> options.ColorStyle.Value.Style)
                            |> Setting.trigger (ignore >> refreshColors))
                    ).Position(550.0f)
                    PrettySetting("NoteColors", colors).Position(650.0f, Render.vwidth - 200.0f, 120.0f)
                    noteskins.Position(800.0f)
                    PrettyButton("EditNoteskin", ignore).Position(900.0f)
                ] :> Selectable
            Callback = ignore
        }

    let pacemaker() : SelectionPage =
        let utype =
            match options.Pacemaker.Value with
            | Accuracy _ -> 0
            | Lamp _ -> 1
            |> Setting.simple
        let accuracy =
            match options.Pacemaker.Value with
            | Accuracy a -> a
            | Lamp _ -> 0.95
            |> Setting.simple
            |> Setting.bound 0.0 1.0
            |> Setting.round 3
        let lamp =
            match options.Pacemaker.Value with
            | Accuracy _ -> Lamp.SDCB
            | Lamp l -> l
            |> Setting.simple
        {
            Content = fun add ->
                column [
                    PrettySetting("PacemakerType",
                        refreshChoice
                            [|"ACCURACY"; "LAMP"|]
                            [|
                                [| PrettySetting("PacemakerAccuracy", Slider(accuracy, 0.01f)).Position(300.0f) |]
                                [| PrettySetting("PacemakerLamp", Selector.FromEnum lamp).Position(300.0f) |]
                            |] utype
                    ).Position(200.0f)
                ] :> Selectable
            Callback = fun () ->
                match utype.Value with
                | 0 -> options.Pacemaker.Value <- Accuracy accuracy.Value
                | 1 -> options.Pacemaker.Value <- Lamp lamp.Value
                | _ -> failwith "impossible"
        }

    let editAccuracySystem (index, sys) =
        let utype =
            match sys with
            | SC _ -> 0
            | SCPlus _ -> 1
            | Wife _ -> 2
            | OM _ -> 3
            | _ -> 0 //nyi
            |> Setting.simple

        let judge =
            match sys with
            | SC (judge, rd)
            | SCPlus (judge, rd)
            | Wife (judge, rd) -> judge
            | _ -> 4
            |> Setting.simple
            |> Setting.bound 1 9
        let judgeEdit = PrettySetting("Judge", Slider(judge, 0.1f)).Position(300.0f)

        let od =
            match sys with
            | OM od -> od
            | _ -> 8.0f
            |> Setting.simple
            |> Setting.bound 0.0f 10.0f
            |> Setting.roundf 1
        let odEdit = PrettySetting("OverallDifficulty", Slider(od, 0.01f)).Position(300.0f)

        let ridiculous =
            match sys with
            | SC (judge, rd)
            | SCPlus (judge, rd)
            | Wife (judge, rd) -> rd
            | _ -> false
            |> Setting.simple
        let ridiculousEdit = PrettySetting("EnableRidiculous", Selector.FromBool ridiculous).Position(400.0f)

        {
            Content = fun add ->
                column [
                    PrettySetting("ScoreSystemType",
                        refreshChoice
                            [|"SC"; "SC+"; "Wife3"; "osu!mania"|]
                            [|
                                [| judgeEdit; ridiculousEdit |]
                                [| judgeEdit; ridiculousEdit |]
                                [| judgeEdit; ridiculousEdit |]
                                [| odEdit |]
                            |] utype
                    ).Position(200.0f)
                ] :> Selectable
            Callback = fun () ->
                let value =
                    match utype.Value with
                    | 0 -> SC (judge.Value, ridiculous.Value)
                    | 1 -> SCPlus (judge.Value, ridiculous.Value)
                    | 2 -> Wife (judge.Value, ridiculous.Value)
                    | 3 -> OM od.Value
                    | _ -> failwith "impossible"
                Setting.app (WatcherSelection.replace index value) options.AccSystems
        }

    let scoreSystems() : SelectionPage =
        {
            Content = fun add ->
                column [
                    let setting =
                        Setting.make ignore ( fun () -> WatcherSelection.indexed options.AccSystems.Value )
                    PrettySetting("ScoreSystems",
                        CardSelect.Selector(
                            setting,
                            { CardSelect.Config.Default with
                                NameFunc = fun (_, s) -> s.ToString()
                                DuplicateFunc = Some (fun (_, s) -> Setting.app (WatcherSelection.add s) options.AccSystems)
                                EditFunc = Some (fun (i, s) -> editAccuracySystem (i, s))
                                DeleteFunc = Some (fun (_, s) -> Setting.app (WatcherSelection.delete s) options.AccSystems)
                                MarkFunc = fun ((_, s), b) -> if b then Setting.app (WatcherSelection.moveToTop s) options.AccSystems
                            },
                            add
                        )
                    ).Position(200.0f, PRETTYWIDTH, 800.0f)
                ] :> Selectable
            Callback = ignore
        }

    let gameplay() : SelectionPage =
        {
            Content = fun add ->
                column [
                    PrettySetting("ScrollSpeed", Slider(options.ScrollSpeed, 0.005f)).Position(200.0f)
                    PrettySetting("HitPosition", Slider(options.HitPosition, 0.005f)).Position(280.0f)
                    PrettySetting("Upscroll", Selector.FromBool(options.Upscroll)).Position(360.0f)
                    PrettySetting("BackgroundDim", Slider(options.BackgroundDim, 0.01f)).Position(440.0f)
                    PrettyButton("ScreenCover", 
                        fun() ->
                            //todo: preview of what screencover looks like
                            add("ScreenCover",
                                {
                                    Content = fun add ->
                                        column [
                                            PrettySetting("ScreenCoverUp", Slider(options.ScreenCoverUp, 0.01f)).Position(200.0f)
                                            PrettySetting("ScreenCoverDown", Slider(options.ScreenCoverDown, 0.01f)).Position(300.0f)
                                            PrettySetting("ScreenCoverFadeLength", Slider(options.ScreenCoverFadeLength, 0.01f)).Position(400.0f)
                                        ] :> Selectable
                                    Callback = ignore
                                }
                            )
                    ).Position(520.0f)
                    PrettyButton("Pacemaker", fun () -> add("Pacemaker", pacemaker())).Position(670.0f)
                    PrettyButton("ScoreSystems", fun () -> add("ScoreSystems", scoreSystems())).Position(750.0f)
                    PrettyButton("LifeSystems", ignore).Position(830.0f)
                ] :> Selectable
            Callback = ignore
        }

    let keybinds() : SelectionPage = 
        let keycount = Setting.simple options.KeymodePreference.Value
        
        let f k i =
            Setting.make
                (fun v -> options.GameplayBinds.[k - 3].[i] <- v)
                (fun () -> options.GameplayBinds.[k - 3].[i])

        let binds, refreshBinds =
            refreshRow
                (fun () -> int keycount.Value)
                (fun i k ->
                    let x = -60.0f * float32 k
                    let n = float32 i
                    { new KeyBinder(f (int keycount.Value) i, false) with
                        override this.OnDeselect() =
                            base.OnDeselect()
                            if i + 1 < k then
                                match this.SParent.Value with
                                | :? ListSelectable as s -> s.Synchronized(fun () -> if s.Selected && s.HoverChild.IsSome then s.Next(); s.HoverChild.Value.Selected <- true)
                                | _ -> failwith "impossible"
                    }
                    |> positionWidget(x + 120.0f * n, 0.5f, 0.0f, 0.0f, x + 120.0f * n + 120.0f, 0.5f, 0.0f, 1.0f))
                    
        {
            Content = fun add ->
                column [
                    PrettySetting("Keymode", Selector.FromEnum<Keymode>(keycount |> Setting.trigger (ignore >> refreshBinds))).Position(200.0f)
                    PrettySetting("GameplayBinds", binds).Position(280.0f, Render.vwidth - 200.0f, 120.0f)
                    PrettyButton("Hotkeys", ignore).Position(400.0f)
                ] :> Selectable
            Callback = ignore
        }

    let debug() : SelectionPage =
        {
            Content = fun add ->
                column [
                    PrettyButton("RebuildCache", fun () -> BackgroundTask.Create TaskFlags.LONGRUNNING "Rebuilding Cache" Library.rebuildTask |> ignore).Position(200.0f)
                    PrettyButton("DownloadUpdate",
                        fun () ->
                            if Interlude.Utils.AutoUpdate.updateAvailable then
                                Interlude.Utils.AutoUpdate.applyUpdate(fun () -> Notification.add (Localisation.localise "notification.UpdateInstalled", NotificationType.System))
                    ).Position(300.0f)
                ] :> Selectable
            Callback = ignore
        }

    let mainOptionsMenu() : SelectionPage =
        {
            Content = fun add ->
                row [
                    BigButton(localiseOption "System", 0, fun () -> add("System", system())) |> positionWidget(-790.0f, 0.5f, -150.0f, 0.5f, -490.0f, 0.5f, 150.0f, 0.5f);
                    BigButton(localiseOption "Themes", 1, fun () -> add("Themes", themes())) |> positionWidget(-470.0f, 0.5f, -150.0f, 0.5f, -170.0f, 0.5f, 150.0f, 0.5f);
                    BigButton(localiseOption "Gameplay", 2, fun () -> add("Gameplay", gameplay())) |> positionWidget(-150.0f, 0.5f, -150.0f, 0.5f, 150.0f, 0.5f, 150.0f, 0.5f);
                    BigButton(localiseOption "Keybinds", 3, fun () -> add("Keybinds", keybinds())) |> positionWidget(170.0f, 0.5f, -150.0f, 0.5f, 470.0f, 0.5f, 150.0f, 0.5f);
                    BigButton(localiseOption "Debug", 4, fun () -> add("Debug", debug())) |> positionWidget(490.0f, 0.5f, -150.0f, 0.5f, 790.0f, 0.5f, 150.0f, 0.5f);
                ] :> Selectable
            Callback = fun () -> LevelSelect.refresh <- true
        }