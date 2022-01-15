﻿namespace Interlude.UI.Screens.LevelSelect

open System
open Prelude.Common
open Prelude.Data.Scores
open Prelude.Data.Charts.Caching
open Prelude.Scoring
open Prelude.Gameplay.Mods
open Prelude.Gameplay.Difficulty
open Prelude.ChartFormats.Interlude
open Interlude.UI
open Interlude.Utils
open Interlude.Graphics
open Interlude.Input
open Interlude.Gameplay
open Interlude.Options
open Interlude.UI.Animation
open Interlude.UI.Components
open Interlude.UI.Components.Selection
open Interlude.UI.Components.Selection.Containers
open Interlude.UI.Screens.LevelSelect.Globals

module private InfoPanel =

    type ScoreboardSort =
    | Time = 0
    | Performance = 1
    | Accuracy = 2

    type ScoreboardFilter =
    | All = 0
    | CurrentRate = 1
    | CurrentPlaystyle = 2
    | CurrentMods = 3

    type ScoreboardItem(data: ScoreInfoProvider) as this =
        inherit Widget()

        let fade = AnimationFade 0.0f

        do
            data.Physical |> ignore
            data.Lamp |> ignore

            let colfun = fun () -> let a = int (255.0f * fade.Value) in (Color.FromArgb(a, Color.White), Color.FromArgb(a, Color.Black))
            
            TextBox((fun() -> data.Scoring.FormatAccuracy()), colfun, 0.0f)
            |> positionWidget(5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.6f)
            |> this.Add

            TextBox((fun () -> sprintf "%s  •  %ix  •  %.2f" (data.ScoringConfig.LampName data.Lamp) data.Scoring.State.BestCombo data.Physical), colfun, 0.0f)
            |> positionWidget(5.0f, 0.0f, 0.0f, 0.6f, 0.0f, 0.5f, 0.0f, 1.0f)
            |> this.Add

            TextBox(K (formatTimeOffset(DateTime.Now - data.ScoreInfo.time)), colfun, 1.0f)
            |> positionWidget(0.0f, 0.5f, 0.0f, 0.6f, -5.0f, 1.0f, 0.0f, 1.0f)
            |> this.Add

            TextBox(K data.Mods, colfun, 1.0f)
            |> positionWidget(0.0f, 0.5f, 0.0f, 0.0f, -5.0f, 1.0f, 0.0f, 0.6f)
            |> this.Add

            Clickable((fun () -> Screen.changeNew (fun () -> new Screens.Score.Screen(data, BestFlags.Default) :> Screen.T) Screen.Type.Score Screen.TransitionFlag.Default), ignore)
            |> this.Add

            this.Animation.Add fade
            Animation.Serial(AnimationTimer 150.0, AnimationAction (fun () -> let (l, t, r, b) = this.Anchors in l.Snap(); t.Snap(); r.Snap(); b.Snap(); fade.Target <- 1.0f))
            |> this.Animation.Add

            this.Reposition(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 75.0f, 0.0f)

        override this.Draw() =
            Draw.rect this.Bounds (Style.accentShade(int (127.0f * fade.Value), 0.8f, 0.0f)) Sprite.Default
            base.Draw()
        member this.Data = data

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if Mouse.Hover this.Bounds && options.Hotkeys.Delete.Value.Tapped() then
                let scoreName = sprintf "%s | %s" (data.Scoring.FormatAccuracy()) (data.Lamp.ToString())
                Tooltip.callback (
                    options.Hotkeys.Delete.Value,
                    Localisation.localiseWith [scoreName] "misc.Delete",
                    Warning,
                    fun () ->
                        chartSaveData.Value.Scores.Remove data.ScoreInfo |> ignore
                        LevelSelect.refresh <- true
                        Notification.add (Localisation.localiseWith [scoreName] "notification.Deleted", Info)
                )

    type Scoreboard() as this =
        inherit Selectable()

        let mutable count = -1

        let mutable chart = ""
        let mutable scoring = ""
        let ls = new ListSelectable(true)


        let filter = Setting.simple ScoreboardFilter.All
        let sort = Setting.map enum int options.ScoreSortMode

        let sorter() : Comparison<Widget> =
            match sort.Value with
            | ScoreboardSort.Accuracy -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.Scoring.Value.CompareTo((b :?> ScoreboardItem).Data.Scoring.Value))
            | ScoreboardSort.Performance -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.Physical.CompareTo((b :?> ScoreboardItem).Data.Physical))
            | ScoreboardSort.Time
            | _ -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.ScoreInfo.time.CompareTo((b :?> ScoreboardItem).Data.ScoreInfo.time))

        let filterer() : Widget -> bool =
            match filter.Value with
            | ScoreboardFilter.CurrentRate -> (fun a -> (a :?> ScoreboardItem).Data.ScoreInfo.rate = rate.Value)
            | ScoreboardFilter.CurrentPlaystyle -> (fun a -> (a :?> ScoreboardItem).Data.ScoreInfo.layout = options.Playstyles.[(a :?> ScoreboardItem).Data.ScoreInfo.keycount - 3])
            | ScoreboardFilter.CurrentMods -> (fun a -> (a :?> ScoreboardItem).Data.ScoreInfo.selectedMods = selectedMods.Value)
            | _ -> K true

        let flowContainer = new FlowContainer(Sort = sorter(), Filter = filterer())

        let scoreLoader =
            let future = BackgroundTask.futureSeq<ScoreboardItem> "Scoreboard loader" (fun item -> flowContainer.Synchronized(fun () -> flowContainer.Add item))
            fun () ->
                future
                    (fun () ->
                        flowContainer.Synchronized(flowContainer.Clear)
                        match chartSaveData with
                        | None -> Seq.empty
                        | Some d ->
                            seq { 
                                for score in d.Scores do
                                    yield ScoreInfoProvider(score, currentChart.Value, getCurrentScoreSystem())
                                    |> ScoreboardItem
                            }
                    )

        do
            flowContainer
            |> positionWidgetA(0.0f, 10.0f, 0.0f, -50.0f)
            |> this.Add

            StylishButton.FromEnum("Sort",
                sort |> Setting.trigger (fun _ -> flowContainer.Sort <- sorter()),
                Style.main 100, TiltLeft = false )
            |> TooltipRegion.Create (Localisation.localise "levelselect.scoreboard.tooltip.Sort")
            |> positionWidget(0.0f, 0.0f, -45.0f, 1.0f, -15.0f, 0.25f, -5.0f, 1.0f)
            |> ls.Add

            StylishButton.FromEnum("Filter",
                filter |> Setting.trigger (fun _ -> this.Refresh()),
                Style.main 90 )
            |> TooltipRegion.Create (Localisation.localise "levelselect.scoreboard.tooltip.Filter")
            |> positionWidget(10.0f, 0.25f, -45.0f, 1.0f, -15.0f, 0.5f, -5.0f, 1.0f)
            |> ls.Add

            StylishButton(
                (fun () -> Setting.app WatcherSelection.cycleForward options.ScoringSystems; LevelSelect.refresh <- true),
                (fun () -> currentScoreSystem.Name),
                Style.main 80 )
            |> TooltipRegion.Create (Localisation.localise "levelselect.scoreboard.tooltip.ScoreSystems")
            |> positionWidget(10.0f, 0.5f, -45.0f, 1.0f, -15.0f, 0.75f, -5.0f, 1.0f)
            |> ls.Add

            StylishButton(
                this.Refresh,
                K <| Localisation.localise "levelselect.scoreboard.storage.Local",
                Style.main 70, TiltRight = false ) //nyi
            |> TooltipRegion.Create (Localisation.localise "levelselect.scoreboard.tooltip.Storage")
            |> positionWidget(10.0f, 0.75f, -45.0f, 1.0f, -15.0f, 1.0f, -5.0f, 1.0f)
            |> ls.Add

            ls |> this.Add

            let noLocalScores = Localisation.localise "levelselect.scoreboard.NoLocalScores"
            TextBox((fun () -> if count = 0 then noLocalScores else ""), K (Color.White, Color.Black), 0.5f)
            |> positionWidget(50.0f, 0.0f, 0.0f, 0.3f, -50.0f, 1.0f, 0.0f, 0.5f)
            |> this.Add

        member this.Refresh() =
            let h = match currentCachedChart with Some c -> c.Hash | None -> ""
            if (match chartSaveData with None -> false | Some d -> let v = d.Scores.Count <> count in count <- d.Scores.Count; v) || h <> chart then
                chart <- h
                scoreLoader()
            elif scoring <> scoreSystemId then
                let s = getCurrentScoreSystem()
                for c in flowContainer.Children do (c :?> ScoreboardItem).Data.ScoringConfig <- s
                scoring <- scoreSystemId
            flowContainer.Filter <- filterer()

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            this.HoverChild <- None

open InfoPanel

type InfoPanel() as this =
    inherit Widget()

    let scores = Scoreboard()
    let mutable length = ""
    let mutable bpm = ""
    let mutable notecount = ""

    do
        scores
        |> positionWidgetA(0.0f, 0.0f, 0.0f, -200.0f)
        |> this.Add

        new TextBox(
            (fun () -> sprintf "%.2f%s" (match difficultyRating with None -> 0.0 | Some d -> d.Physical) Interlude.Icons.star),
            (fun () -> Color.White, match difficultyRating with None -> Color.Black | Some d -> physicalColor d.Physical), 0.0f)
        |> positionWidget(10.0f, 0.0f, -190.0f, 1.0f, 0.0f, 0.5f, -120.0f, 1.0f)
        |> this.Add

        new TextBox(
            (fun () -> sprintf "%.2f%s" (match difficultyRating with None -> 0.0 | Some d -> d.Technical) Interlude.Icons.star),
            (fun () -> Color.White, match difficultyRating with None -> Color.Black | Some d -> technicalColor d.Technical), 0.0f)
        |> positionWidget(10.0f, 0.0f, -120.0f, 1.0f, 0.0f, 0.5f, -50.0f, 1.0f)
        |> this.Add

        new TextBox((fun () -> bpm), K (Color.White, Color.Black), 1.0f)
        |> positionWidget(0.0f, 0.5f, -190.0f, 1.0f, -10.0f, 1.0f, -120.0f, 1.0f)
        |> this.Add

        new TextBox((fun () -> length), K (Color.White, Color.Black), 1.0f)
        |> positionWidget(0.0f, 0.5f, -120.0f, 1.0f, -10.0f, 1.0f, -50.0f, 1.0f)
        |> this.Add

        new TextBox((fun () -> notecount), K (Color.White, Color.Black), 1.0f)
        |> positionWidget(10.0f, 0.0f, -50.0f, 1.0f, -17.0f, 1.0f, -10.0f, 1.0f)
        |> this.Add

        new TextBox((fun () -> getModString(rate.Value, selectedMods.Value, autoplay)), K (Color.White, Color.Black), 0.0f)
        |> positionWidget(17.0f, 0.0f, -50.0f, 1.0f, -10.0f, 1.0f, -10.0f, 1.0f)
        |> this.Add

    member this.Refresh() =
        length <-
            match currentCachedChart with
            | Some cc -> cc.Length
            | None -> 0.0f<ms>
            |> fun x -> x / rate.Value
            |> fun x -> (x / 1000.0f / 60.0f |> int, (x / 1000f |> int) % 60)
            |> fun (x, y) -> sprintf "%s%i:%02i" Interlude.Icons.time x y
        bpm <-
            match currentCachedChart with
            | Some cc -> cc.BPM
            | None -> (500.0f<ms/beat>, 500.0f<ms/beat>)
            |> fun (b, a) -> (60000.0f<ms> / a * rate.Value |> int, 60000.0f<ms> / b * rate.Value |> int)
            |> fun (a, b) ->
                if a > 9000 || b < 0 then sprintf "%s∞" Interlude.Icons.bpm
                elif Math.Abs(a - b) < 5 || b > 9000 then sprintf "%s%i" Interlude.Icons.bpm a
                else sprintf "%s%i-%i" Interlude.Icons.bpm a b
        notecount <-
            match currentChart with
            | Some c ->
                let mutable notes = 0
                let mutable lnotes = 0
                for (_, nr) in c.Notes.Data do
                    for n in nr do
                        if n = NoteType.NORMAL then notes <- notes + 1
                        elif n = NoteType.HOLDHEAD then notes <- notes + 1; lnotes <- lnotes + 1
                sprintf "%i Notes | %.0f%% Holds" notes (100.0f * float32 lnotes / float32 notes)
            | None -> ""
        scores.Refresh()