﻿namespace Prelude.Charts.Conversions

open System
open System.IO
open System.Collections.Generic
open System.Linq
open Percyqaz.Common
open Prelude
open Prelude.Charts.Formats.osu
open Prelude.Charts

module Osu_To_Interlude =

    let private convert_hit_objects (objects: HitObject list) (keys: int) : TimeArray<NoteRow> =
        let output = List<TimeItem<NoteRow>>()
        let holding_until: Time option array = Array.zeroCreate keys
        let mutable last_row: TimeItem<NoteRow> = { Time = -Time.infinity; Data = [||] }

        let x_to_column (x: float) =
            x / 512.0 * float keys |> int |> min (keys - 1) |> max 0

        let finish_holds time =
            let mutable earliest_upcoming_release = Time.infinity

            let find_earliest_upcoming_release () =
                earliest_upcoming_release <- Time.infinity

                for h in holding_until do
                    match h with
                    | Some v -> earliest_upcoming_release <- min earliest_upcoming_release v
                    | None -> ()

            find_earliest_upcoming_release ()

            while earliest_upcoming_release < time do
                for k = 0 to keys - 1 do
                    if holding_until.[k] = Some earliest_upcoming_release then
                        assert (earliest_upcoming_release >= last_row.Time)

                        if earliest_upcoming_release > last_row.Time then
                            last_row <-
                                {
                                    Time = earliest_upcoming_release
                                    Data = Array.zeroCreate keys
                                }

                            output.Add last_row

                            for k = 0 to keys - 1 do
                                if holding_until.[k] <> None then
                                    last_row.Data.[k] <- NoteType.HOLDBODY

                        match last_row.Data.[k] with
                        | NoteType.NOTHING
                        | NoteType.HOLDBODY ->
                            last_row.Data.[k] <- NoteType.HOLDTAIL
                            holding_until.[k] <- None
                        | _ -> failwithf "impossible"

                find_earliest_upcoming_release ()

        let add_note column time =
            finish_holds time
            assert (time >= last_row.Time)

            if time > last_row.Time then
                last_row <-
                    {
                        Time = time
                        Data = Array.zeroCreate keys
                    }

                output.Add last_row

                for k = 0 to keys - 1 do
                    if holding_until.[k] <> None then
                        last_row.Data.[k] <- NoteType.HOLDBODY

            match last_row.Data.[column] with
            | NoteType.NOTHING -> last_row.Data.[column] <- NoteType.NORMAL
            | _ -> skip_conversion (sprintf "Stacked note at %f" time)

        let start_hold column time end_time =
            finish_holds time
            assert (time >= last_row.Time)

            if time > last_row.Time then
                last_row <-
                    {
                        Time = time
                        Data = Array.zeroCreate keys
                    }

                output.Add last_row

                for k = 0 to keys - 1 do
                    if holding_until.[k] <> None then
                        last_row.Data.[k] <- NoteType.HOLDBODY

            match last_row.Data.[column] with
            | NoteType.NOTHING ->
                last_row.Data.[column] <- NoteType.HOLDHEAD
                holding_until.[column] <- Some end_time
            | _ -> skip_conversion (sprintf "Stacked LN at %f" time)

        for object in objects do
            match object with
            | HitCircle x -> add_note (x_to_column x.X) (Time.of_number x.Time)
            | Hold x when x.EndTime > x.Time -> start_hold (x_to_column x.X) (Time.of_number x.Time) (Time.of_number x.EndTime)
            | Hold x -> add_note (x_to_column x.X) (Time.of_number x.Time)
            | _ -> ()

        finish_holds Time.infinity
        output.ToArray()

    let rec private find_bpm_durations
        (points: TimingPoint list)
        (end_time: Time)
        : Dictionary<float32<ms / beat>, Time> =
        if List.isEmpty points then
            skip_conversion "Beatmap has no BPM points set"

        match List.head points with
        | Uninherited b ->
            let mutable current: float32<ms / beat> = Time.of_number b.MsPerBeat / 1f<beat>
            let mutable t: Time = Time.of_number b.Time
            let data = new Dictionary<float32<ms / beat>, Time>()

            for p in points do
                if (not (data.ContainsKey current)) then
                    data.Add(current, 0.0f<ms>)

                match p with
                | Uninherited b2 ->
                    data.[current] <- data.[current] + Time.of_number b2.Time - t
                    t <- Time.of_number b2.Time
                    current <- Time.of_number b2.MsPerBeat / 1f<beat>
                | _ -> ()

            if (not (data.ContainsKey current)) then
                data.Add(current, 0.0f<ms>)

            data.[current] <- data.[current] + end_time - t
            data
        | _ -> find_bpm_durations (List.tail points) end_time

    let private convert_timing_points
        (points: TimingPoint list)
        (end_time: Time)
        : TimeArray<BPM> * TimeArray<float32> =
        let most_common_bpm =
            (find_bpm_durations points end_time)
                .OrderByDescending(fun p -> p.Value)
                .First()
                .Key

        let add_sv_value (offset, new_speed) sv =
            match sv with
            | [] -> [ { Time = offset; Data = new_speed } ]
            | { Time = time; Data = current_speed } :: s ->
                if current_speed = new_speed then
                    sv
                else
                    { Time = offset; Data = new_speed } :: sv

        let (bpm, sv, _) =
            let func
                ((bpm, sv, scroll): (TimeItem<BPM> list * TimeItem<float32> list * float32))
                (point: TimingPoint)
                : (TimeItem<BPM> list * TimeItem<float32> list * float32) =
                match point with
                | Uninherited b ->
                    let mspb = Time.of_number b.MsPerBeat / 1f<beat>
                    {
                        Time = (Time.of_number b.Time)
                        Data = { Meter = b.Meter * 1<beat>; MsPerBeat = mspb }
                    }
                    :: bpm,
                    add_sv_value (Time.of_number b.Time, most_common_bpm / mspb) sv,
                    most_common_bpm / mspb
                | Inherited s -> bpm, add_sv_value (Time.of_number s.Time, float32 s.Multiplier * scroll) sv, scroll

            List.fold func ([], [], 1.0f) points

        bpm |> Array.ofList |> Array.rev, sv |> Array.ofList |> Array.rev

    let convert (b: Beatmap) (action: ConversionAction) : Result<ImportChart, SkippedConversion> =
        try
            let keys = b.Difficulty.CircleSize |> int

            if b.General.Mode <> Gamemode.OSU_MANIA then
                skip_conversion "Beatmap is not osu!mania gamemode"

            if keys < 3 || keys > 10 then
                skip_conversion "Keymode not supported"

            if b.Objects.Length < 20 then
                skip_conversion "Beatmap has less than 20 notes"

            let path = Path.GetDirectoryName action.Source

            let rec find_background_file e =
                match e with
                | (Background(bg, _, _)) :: _ -> bg
                | _ :: es -> find_background_file es
                | [] -> ""

            let header =
                {
                    Title = b.Metadata.Title.Trim()
                    TitleNative =
                        let t = b.Metadata.TitleUnicode.Trim() in

                        if t.Length > 0 && t <> b.Metadata.Title.Trim() then
                            Some t
                        else
                            None
                    Artist = b.Metadata.Artist.Trim()
                    ArtistNative =
                        let t = b.Metadata.ArtistUnicode.Trim() in

                        if t.Length > 0 && t <> b.Metadata.Artist.Trim() then
                            Some t
                        else
                            None
                    Creator = b.Metadata.Creator
                    DiffName = b.Metadata.Version
                    Subtitle = None
                    Source = let t = b.Metadata.Source.Trim() in if t.Length > 0 then Some t else None
                    Tags = b.Metadata.Tags

                    PreviewTime = float32 b.General.PreviewTime * 1.0f<ms>
                    BackgroundFile =
                        let r = find_background_file b.Events

                        if File.Exists(Path.Combine(path, r)) then
                            if action.Config.MoveAssets then
                                Relative r
                            else
                                Absolute(Path.Combine(path, r))
                        else
                            Missing
                    AudioFile =
                        let r = b.General.AudioFilename

                        if File.Exists(Path.Combine(path, r)) then
                            if action.Config.MoveAssets then
                                Relative r
                            else
                                Absolute(Path.Combine(path, r))
                        else
                            Missing

                    ChartSource = Osu(b.Metadata.BeatmapSetID, b.Metadata.BeatmapID)
                }

            let snaps = convert_hit_objects b.Objects keys

            let bpm, sv = convert_timing_points b.Timing (TimeArray.last snaps).Value.Time

            Ok {
                Header = header
                LoadedFromPath = action.Source
                Chart = {
                    Keys = keys
                    Notes = snaps
                    BPM = bpm
                    SV = sv
                }
            }
        with
        | :? ConversionSkipException as skip_reason -> 
            Error (action.Source, skip_reason.msg)
        | other_error ->
            Logging.Debug(sprintf "Unexpected error converting %s" action.Source, other_error)
            Error (action.Source, other_error.Message)