﻿namespace Prelude.Data

open System
open System.IO
open System.Collections.Generic
open Prelude.Json
open Prelude.Common
open Prelude.Charts.Interlude
open Prelude.Gameplay.Score
open Prelude.Gameplay.Mods
open Prelude.Gameplay.Difficulty
open Prelude.Gameplay.Layout
open Prelude.Data.ChartManager

module ScoreManager =

    //todo: add json required attributes to these records OR a way to insert default values
    type Score = {
        time: DateTime
        hitdata: string
        player: string
        playerUUID: string
        rate: float32
        selectedMods: Dictionary<string, int>
        layout: Layout
        keycount: int
    }

    type ChartSaveData = {
        Path: string
        Offset: Setting<Time>
        Scores: List<Score>
        Lamp: Dictionary<string, Lamp>
        Accuracy: Dictionary<string, float>
        Clear: Dictionary<string, bool>
    }
    with
        static member FromChart(c: Chart) = {
            Path = c.FileIdentifier
            Offset = Setting(if c.Notes.IsEmpty() then 0.0f<ms> else offsetOf <| c.Notes.First());
            Scores = new List<Score>()
            Lamp = new Dictionary<string, Lamp>()
            Accuracy = Dictionary<string, float>()
            Clear = Dictionary<string, bool>()
        }

    (*
        Gameplay pipelines that need to happen to play a chart
        Chart -> Modified chart -> Colorized chart
                                -> Replay data -> Mod replay data
                                -> Difficulty rating data
    *)

    type ScoreInfoProvider(score: Score, chart: Chart) =
        let (modchart, hitdata) = getModChartWithScore (ModState(score.selectedMods)) chart score.hitdata
        let difficulty =
            lazy (let (keys, notes, _, _, _) = modchart.Force()
                RatingReport(notes, score.rate, score.layout, keys))
        let scoring =
            lazy (let m = createAccuracyMetric(SCPlus 4) in m.ProcessAll(hitdata.Force()); m) //todo: connect to profile settings
        let hp = lazy (let m = createHPMetric VG (scoring.Force()) in m.ProcessAll(hitdata.Force()); m)
        let performance =
            lazy (
                let (keys, _, _, _, _) = modchart.Force()
                let m = performanceMetric (difficulty.Force()) keys in m.ProcessAll(hitdata.Force()); m)
        let lamp = lazy (lamp (scoring.Force().State))

        member this.Scoring = scoring.Force()
        member this.Clear = hp.Force().Failed
        member this.Lamp = lamp.Force()
        member this.Physical = performance.Force().Value
        member this.Technical = 0.0 //nyi
        member this.Mods = String.Join(", ", sprintf "%.2fx" score.rate :: (score.selectedMods.Keys |> List.ofSeq |> List.map ModState.GetModName))

    type ScoresDB() =
        let data = ScoresDB.Load()

        member this.Save() = JsonHelper.saveFile data (Path.Combine(getDataPath("Data"), "scores.json"))

        static member Load() =
            try
                JsonHelper.loadFile(Path.Combine(getDataPath("Data"), "scores.json"))
            with
            | :? FileNotFoundException -> Logging.Info("No scores database found, creating one.") ""; new Dictionary<string, ChartSaveData>()
            | err -> Logging.Critical("Could not load score database! Creating from scratch") (err.ToString()); new Dictionary<string, ChartSaveData>()

        member this.GetScoreData (chart: Chart) =
            let hash = calculateHash(chart)
            if not <| data.ContainsKey(hash) then data.Add(hash, ChartSaveData.FromChart(chart))
            data.[hash]
            

    type TopScore = string * DateTime * float //Hash, Timestamp, Rating

    module TopScore =
        let private count = 50

        let add ((hash, timestamp, rating): TopScore) (data: TopScore list) =
            let rec f count data =
                match count with
                | 0 -> []
                | 1 ->
                    match data with
                    | (h, t, r) :: _ ->
                        if r >= rating then
                            (h, t, r) :: []
                        else
                            (hash, timestamp, rating) :: []
                    | [] -> []
                | _ ->
                    match data with
                    | (h, t, r) :: xs ->
                        if h = hash then
                            if r >= rating then
                                (h, t, r) :: xs
                            else
                                (hash, timestamp, rating) :: xs
                        else
                            if r >= rating then
                                (h, t, r) :: (f (count - 1) xs)
                            else
                                (hash, timestamp, rating) :: (h, t, r) :: (f (count - 2) xs)
                    | [] -> []
            f count data