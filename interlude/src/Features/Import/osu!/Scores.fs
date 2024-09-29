﻿namespace Interlude.Features.Import.osu

open System
open System.IO
open System.Text
open Percyqaz.Common
open Prelude
open Prelude.Charts
open Prelude.Charts.Conversions
open Prelude.Charts.Formats.osu
open Prelude.Gameplay
open Prelude.Data.OsuClientInterop
open Prelude.Data.User
open Prelude.Data.Library
open Interlude.Options
open Interlude.Content

module Scores =

    let private import_osu_scores () =
        match options.OsuMount.Value with
        | None -> Logging.Warn "Requires osu! Songs folder to be mounted"
        | Some m ->

        let scores =
            use file = Path.Combine(m.SourceFolder, "..", "scores.db") |> File.OpenRead
            Logging.Info "Reading scores database .."
            use reader = new BinaryReader(file, Encoding.UTF8)
            OsuScoreDatabase.Read(reader)

        Logging.Info(sprintf "Read score data, containing info about %i maps" scores.Beatmaps.Length)

        let main_db =
            use file = Path.Combine(m.SourceFolder, "..", "osu!.db") |> File.OpenRead
            Logging.Info "Reading osu! database .."
            use reader = new BinaryReader(file, Encoding.UTF8)
            OsuDatabase.Read(reader)

        Logging.Info(
            sprintf
                "Read %s's osu! database containing %i maps, starting import .."
                main_db.PlayerName
                main_db.Beatmaps.Length
        )

        let chart_map =
            main_db.Beatmaps
            |> Seq.filter (fun b -> b.Mode = 3uy)
            |> Seq.map (fun b -> b.Hash, b)
            |> Map.ofSeq

        let mutable chart_count = 0
        let mutable score_count = 0

        let find_matching_chart (beatmap_data: OsuDatabase_Beatmap) (chart: Chart) =
            let chart_hash = Chart.hash chart

            match ChartDatabase.get_meta chart_hash Content.Charts with
            | None ->
                match Imports.detect_rate_mod beatmap_data.Difficulty with
                | Some rate ->
                    let chart = Chart.scale rate chart
                    let chart_hash = Chart.hash chart
                    
                    match ChartDatabase.get_meta chart_hash Content.Charts with
                    | None ->
                        Logging.Warn(
                            sprintf
                                "Skipping %.2fx of %s [%s], can't find a matching imported 1.00x chart"
                                rate
                                beatmap_data.TitleUnicode
                                beatmap_data.Difficulty
                        )

                        None
                    | Some _ -> Some(chart, chart_hash, rate)
                | None ->
                    Logging.Warn(sprintf "%s [%s] skipped, can't find a matching imported chart" beatmap_data.TitleUnicode beatmap_data.Difficulty)
                    None
            | Some _ -> Some(chart, chart_hash, 1.0f)

        for beatmap_score_data in
            scores.Beatmaps
            |> Seq.where (fun b -> b.Scores.Length > 0 && b.Scores.[0].Mode = 3uy) do
            match Map.tryFind beatmap_score_data.Hash chart_map with
            | None -> ()
            | Some beatmap_data ->

            let osu_file =
                Path.Combine(m.SourceFolder, beatmap_data.FolderName, beatmap_data.Filename)

            match
                match Beatmap.FromFile osu_file with
                | Ok beatmap ->
                    Osu_To_Interlude.convert
                        beatmap
                        {
                            Config = ConversionOptions.Default
                            Source = osu_file
                        }
                    |> function Ok i -> Ok i | Error s -> Error (snd s)
                | Error reason -> Error reason
            with
            | Error reason -> Logging.Warn(sprintf "%s [%s] skipped, conversion failed: %s" beatmap_data.TitleUnicode beatmap_data.Difficulty reason)
            | Ok chart ->

            match find_matching_chart beatmap_data chart.Chart with
            | None -> ()
            | Some(chart, chart_hash, rate) ->

            chart_count <- chart_count + 1

            for score in beatmap_score_data.Scores do
                let replay_file =
                    Path.Combine(
                        m.SourceFolder,
                        "..",
                        "Data",
                        "r",
                        sprintf "%s-%i.osr" score.BeatmapHash score.Timestamp
                    )

                match
                    try
                        use file = File.OpenRead replay_file
                        use br = new BinaryReader(file)
                        Some(OsuScoreDatabase_Score.Read br)
                    with err ->
                        Logging.Error(sprintf "Error loading replay file %s" replay_file, err)
                        None
                with
                | None -> ()
                | Some replay_info ->

                match Mods.to_interlude_rate_and_mods replay_info.ModsUsed with
                | None -> () // score is invalid for import in some way, skip
                | Some(rate2, mods) ->

                let combined_rate = float32 rate2 * rate

                if
                    MathF.Round(combined_rate, 3) <> MathF.Round(combined_rate, 2)
                    || combined_rate > 3.0f
                    || combined_rate < 0.5f
                then
                    Logging.Info(
                        sprintf "Skipping score with rate %.3f because this isn't supported in Interlude" combined_rate
                    )
                else

                let replay_data = OsuReplay.decode_replay (replay_info, chart, rate * 1.0f<rate>)

                let score: Score =
                    {
                        Timestamp =
                            DateTime.FromFileTimeUtc(replay_info.Timestamp).ToLocalTime()
                            |> Timestamp.from_datetime
                        Replay = Replay.compress_bytes replay_data
                        Rate = MathF.Round(combined_rate, 2)
                        Mods = mods
                        IsImported = true
                        Keys = chart.Keys
                    }

                if not (UserDatabase.delete_score chart_hash score.Timestamp Content.UserData) then
                    score_count <- score_count + 1

                UserDatabase.save_score chart_hash score Content.UserData

        Logging.Info(sprintf "Finished importing osu! scores (%i scores from %i maps)" score_count chart_count)

    let import_osu_scores_service =
        { new Async.Service<unit, unit>() with
            override this.Handle(()) = async { import_osu_scores () }
        }
