﻿namespace Prelude.Gameplay

open System
open Prelude
open Prelude.Charts

type HitEventGutsInternal =
    | Hit_ of delta: Time * is_hold_head: bool * missed: bool
    | Release_ of delta: Time * missed: bool * overhold: bool * dropped: bool * missed_head: bool

type HitEventGuts =
    | Hit of
        {|
            Judgement: JudgementId option
            Missed: bool
            Delta: Time
            IsHold: bool
        |}
    | Release of
        {|
            Judgement: JudgementId option
            Missed: bool
            Delta: Time
            Overhold: bool
            Dropped: bool
        |}

type HitEvent<'Guts> =
    {
        Index: int
        Time: ChartTime
        Column: int
        Guts: 'Guts
    }

(*
    Accuracy/scoring system metric.
    Each note you hit is assigned a certain number of points - Your % accuracy is points scored out of the possible maximum.
    Combo/combo breaking also built-in - Your combo is the number of notes hit well in a row
*)

[<Struct>]
type private HoldInternalState =
    | Nothing
    | Holding
    | Dropped
    | MissedHeadThenHeld
    | MissedHead

[<Struct>]
[<RequireQualifiedAccess>]
type HoldState =
    | Released
    | Holding
    | Dropped
    | MissedHead
    | InTheFuture
    member this.ShowInReceptor = this = Holding || this = Dropped || this = Released


[<Struct>]
type ScoreMetricSnapshot =
    {
        Time: ChartTime
        PointsScored: float
        MaxPointsScored: float
        Combo: int
        Lamp: int
    }
    static member COUNT = 100

[<AbstractClass>]
type IScoreMetric(ruleset: Ruleset, keys: int, replay: IReplayProvider, notes: TimeArray<NoteRow>, rate: float32) =
    inherit ReplayConsumer(keys, replay)

    let first_note = (TimeArray.first notes).Value.Time
    let last_note = (TimeArray.last notes).Value.Time
    let duration = last_note - first_note
    let miss_window = ruleset.Accuracy.MissWindow * rate

    // having two seekers improves performance when feeding scores rather than playing live
    let mutable note_seek_passive = 0
    let mutable note_seek_active = 0

    let hold_states = Array.create keys (Nothing, -1)

    let snapshots = ResizeArray<ScoreMetricSnapshot>()
    let hit_data = InternalScore.create_gameplay ruleset.Accuracy.MissWindow keys notes
    let hit_events = ResizeArray<HitEvent<HitEventGuts>>()

    let on_hit_ev = Event<HitEvent<HitEventGuts>>()
    let on_hit = on_hit_ev.Publish

    //let osu_cbrush_cancel =
    //    match ruleset.Accuracy.HoldNoteBehaviour with
    //    | HoldNoteBehaviour.Osu _ -> true
    //    | _ -> false

    member this.OnHit = on_hit

    member val State =
        {
            Judgements = Array.zeroCreate ruleset.Judgements.Length
            PointsScored = 0.0
            MaxPointsScored = 0.0
            CurrentCombo = 0
            BestCombo = 0
            ComboBreaks = 0
            MaxPossibleCombo = 0
        }

    member this.Name = ruleset.Name

    member this.Value =
        let v = this.State.PointsScored / this.State.MaxPointsScored
        if Double.IsNaN v then 1.0 else v

    member this.FormatAccuracy() = sprintf "%.2f%%" (this.Value * 100.0)
    member this.MissWindow = ruleset.Accuracy.MissWindow
    member this.ScaledMissWindow = miss_window
    member this.Ruleset = ruleset
    member this.ReplayRecentEvents() = replay.EnumerateRecentEvents()

    member this.HoldState (index: int) (k: int) =
        let state, i = hold_states.[k]

        if i = index then
            match state with
            | Nothing -> HoldState.Released
            | Holding -> HoldState.Holding
            | Dropped -> HoldState.Dropped
            | MissedHead
            | MissedHeadThenHeld -> HoldState.MissedHead
        elif i > index then
            let struct (_, _, flags) = hit_data.[index]

            if flags.[k] <> HitStatus.HIT_HOLD_REQUIRED then
                HoldState.Released
            else
                HoldState.MissedHead
        else
            HoldState.InTheFuture

    member this.IsNoteHit (index: int) (k: int) =
        let struct (_, _, flags) = hit_data.[index]
        flags.[k] = HitStatus.HIT_ACCEPTED

    member this.HitData = hit_data

    member this.Finished = note_seek_passive = hit_data.Length

    member this.HitEvents = hit_events.AsReadOnly()
    member this.Snapshots = snapshots.AsReadOnly()

    // correctness guaranteed up to the time you update, no matter how you update
    // call Update with Time.infinity to do a correct feeding of the whole replay
    member this.Update(chart_time: ChartTime) =
        this.PollReplay chart_time // calls HandleKeyDown and HandleKeyUp appropriately
        this.HandlePassive chart_time

    member private this.UpdateStateSnapshots(chart_time: ChartTime) =
        let snapshot_target_count =
            if chart_time > duration then 
                ScoreMetricSnapshot.COUNT
            else
                (float32 ScoreMetricSnapshot.COUNT * chart_time) / duration
                |> ceil
                |> int
                |> max 0
                |> min ScoreMetricSnapshot.COUNT

        while snapshots.Count < snapshot_target_count do
            snapshots.Add
                {
                    Time = float32 (snapshots.Count + 1) / float32 ScoreMetricSnapshot.COUNT * duration
                    PointsScored = this.State.PointsScored
                    MaxPointsScored = this.State.MaxPointsScored
                    Combo = this.State.CurrentCombo
                    Lamp = Lamp.calculate ruleset.Grading.Lamps this.State
                }

    member private this.HandlePassive(chart_time: ChartTime) =
        let now = first_note + chart_time
        let target = now - miss_window

        while note_seek_passive < hit_data.Length
              && InternalScore.offsetOf hit_data.[note_seek_passive] <= target do
            let struct (t, deltas, status) = hit_data.[note_seek_passive]

            for k = 0 to (keys - 1) do

                if status.[k] = HitStatus.HIT_REQUIRED then
                    this._HandleEvent
                        {
                            Index = note_seek_passive
                            Time = t - first_note + miss_window
                            Column = k
                            Guts = Hit_(deltas.[k], false, true)
                        }

                elif status.[k] = HitStatus.HIT_HOLD_REQUIRED then
                    hold_states.[k] <- MissedHead, note_seek_passive

                    this._HandleEvent
                        {
                            Index = note_seek_passive
                            Time = t - first_note + miss_window
                            Column = k
                            Guts = Hit_(deltas.[k], true, true)
                        }

                elif status.[k] = HitStatus.RELEASE_REQUIRED then
                    let overhold =
                        match hold_states.[k] with
                        | Dropped, i
                        | Holding, i when i <= note_seek_passive -> Bitmask.has_key k this.KeyState
                        | _ -> false

                    let dropped =
                        match hold_states.[k] with
                        | Dropped, _
                        | MissedHeadThenHeld, _
                        | MissedHead, _ -> true
                        | _ -> false

                    let missed_head =
                        match hold_states.[k] with
                        | MissedHeadThenHeld, _ -> true
                        | _ -> false

                    this._HandleEvent
                        {
                            Index = note_seek_passive
                            Time = t - first_note + miss_window
                            Column = k
                            Guts = Release_(deltas.[k], true, overhold, dropped, missed_head)
                        }

                    match hold_states.[k] with
                    | _, i when i < note_seek_passive -> hold_states.[k] <- Nothing, note_seek_passive
                    | _ -> ()

            note_seek_passive <- note_seek_passive + 1

        this.UpdateStateSnapshots(chart_time)

    override this.HandleKeyDown(chart_time: ChartTime, k: int) =
        this.HandlePassive chart_time
        let now = first_note + chart_time

        while note_seek_active < hit_data.Length
              && InternalScore.offsetOf hit_data.[note_seek_active] < now - miss_window do
            note_seek_active <- note_seek_active + 1

        let mutable i = note_seek_active
        let mutable cbrush_absorb_delta = miss_window
        let mutable matching_note_index = -1
        let mutable matching_note_delta = miss_window
        let target = now + miss_window

        while i < hit_data.Length && InternalScore.offsetOf hit_data.[i] <= target do
            let struct (t, deltas, status) = hit_data.[i]
            let delta = now - t

            // Find unhit note that is closer than the current candidate
            if (status.[k] = HitStatus.HIT_REQUIRED || status.[k] = HitStatus.HIT_HOLD_REQUIRED) then
                if (Time.abs matching_note_delta > Time.abs delta) then
                    matching_note_index <- i
                    matching_note_delta <- delta

                // If new candidate is within cbrush window, stop looking resulting in earliest match being used
                // Otherwise keep looking for something closer and allow this note to be missed
                if Time.abs matching_note_delta < ruleset.Accuracy.CbrushWindow then
                    i <- hit_data.Length
            // Osu's naive version of cbrush cancelling <-- added for tests and then removed
            //elif 
            //    osu_cbrush_cancel
            //    && status.[k] = HitStatus.HIT_ACCEPTED
            //    && t > now
            //then
            //    cbrush_absorb_delta <- -1.0f<ms>
            // Find hit note that got hit earlier than the cbrush window, and track how close it is
            elif
                status.[k] = HitStatus.HIT_ACCEPTED
                && deltas.[k] < -ruleset.Accuracy.CbrushWindow
            then
                if (Time.abs cbrush_absorb_delta > Time.abs delta) then
                    cbrush_absorb_delta <- delta

            i <- i + 1

        if matching_note_index >= 0 then
            let struct (_, deltas, status) = hit_data.[matching_note_index]
            // If fumbled note detected is closer than hit candidate, swallow the hit
            if Time.abs cbrush_absorb_delta >= Time.abs matching_note_delta then
                let is_hold_head = status.[k] <> HitStatus.HIT_REQUIRED
                status.[k] <- HitStatus.HIT_ACCEPTED
                deltas.[k] <- matching_note_delta / rate

                this._HandleEvent
                    {
                        Index = matching_note_index
                        Time = chart_time
                        Column = k
                        Guts = Hit_(deltas.[k], is_hold_head, false)
                    }
                // Begin tracking if it's a hold note
                if is_hold_head then
                    hold_states.[k] <- Holding, matching_note_index
        else // If no note to hit, but a hold note head was missed, pressing key marks it dropped instead
            hold_states.[k] <-
                match hold_states.[k] with
                | MissedHead, i -> MissedHeadThenHeld, i
                | x -> x

    override this.HandleKeyUp(chart_time: ChartTime, k: int) =
        this.HandlePassive chart_time
        let now = first_note + chart_time

        match hold_states.[k] with
        | Holding, head_index
        | Dropped, head_index
        | MissedHeadThenHeld, head_index ->

            let mutable i = head_index
            let mutable delta = miss_window
            let mutable found = -1
            let target = now + miss_window

            while i < hit_data.Length && InternalScore.offsetOf hit_data.[i] <= target do
                let struct (t, _, status) = hit_data.[i]
                let d = now - t

                if status.[k] = HitStatus.RELEASE_REQUIRED then
                    // Get the first unreleased hold tail we see, after the head of the hold we're tracking
                    found <- i
                    delta <- d
                    i <- hit_data.Length

                i <- i + 1

            if found >= 0 then
                let struct (_, deltas, status) = hit_data.[found]
                status.[k] <- HitStatus.RELEASE_ACCEPTED
                deltas.[k] <- delta / rate

                this._HandleEvent
                    {
                        Index = found
                        Time = chart_time
                        Column = k
                        Guts =
                            Release_(
                                deltas.[k],
                                false,
                                false,
                                fst hold_states.[k] = Dropped || fst hold_states.[k] = MissedHeadThenHeld,
                                fst hold_states.[k] = MissedHeadThenHeld
                            )
                    }

                hold_states.[k] <- Nothing, head_index
            else // If we released but too early (no sign of the tail within range) make the long note dropped
                hold_states.[k] <-
                    match hold_states.[k] with
                    | Holding, i -> Dropped, i
                    | x -> x

                match ruleset.Accuracy.HoldNoteBehaviour with
                | HoldNoteBehaviour.Osu _ -> this.State.BreakCombo(false)
                | _ -> ()
        | MissedHead, _
        | Nothing, _ -> ()

    abstract member HandleEvent: HitEvent<HitEventGutsInternal> -> HitEvent<HitEventGuts>

    member private this._HandleEvent ev =
        let ev = this.HandleEvent ev
        hit_events.Add ev
        on_hit_ev.Trigger ev

module Helpers =

    let window_func (default_judge: JudgementId) (gates: (Time * JudgementId) list) (delta: Time) : JudgementId =
        let rec loop gates =
            match gates with
            | [] -> default_judge
            | (w, j) :: xs -> if delta < w then j else loop xs

        loop gates

    let points (conf: Ruleset) (delta: Time) (judge: JudgementId) : float =
        match conf.Accuracy.Points with
        | AccuracyPoints.WifeCurve j -> RulesetUtils.wife_curve j delta
        | AccuracyPoints.Weights(maxweight, weights) -> weights.[judge] / maxweight

// Concrete implementation of rulesets

type ScoreMetric(config: Ruleset, keys, replay, notes, rate) =
    inherit IScoreMetric(config, keys, replay, notes, rate)

    let head_judgements = Array.create keys config.DefaultJudgement
    let head_deltas = Array.create keys config.Accuracy.MissWindow

    let point_func = Helpers.points config

    let window_func =
        Helpers.window_func config.DefaultJudgement config.Accuracy.Timegates

    override this.HandleEvent ev =
        {
            Index = ev.Index
            Time = ev.Time
            Column = ev.Column
            Guts =
                match ev.Guts with
                | Hit_(delta, isHold, missed) ->
                    let judgement = window_func delta

                    if isHold then
                        head_judgements.[ev.Column] <- judgement
                        head_deltas.[ev.Column] <- delta

                        match config.Accuracy.HoldNoteBehaviour with
                        | HoldNoteBehaviour.JustBreakCombo
                        | HoldNoteBehaviour.JudgeReleases _ ->
                            this.State.Add(point_func delta judgement, 1.0, judgement)

                            if config.Judgements.[judgement].BreaksCombo then
                                this.State.BreakCombo true
                            else
                                this.State.IncrCombo()

                            Hit
                                {|
                                    Judgement = Some judgement
                                    Missed = missed
                                    Delta = delta
                                    IsHold = true
                                |}

                        | HoldNoteBehaviour.Osu _
                        | HoldNoteBehaviour.Normal _
                        | HoldNoteBehaviour.OnlyJudgeReleases _ ->
                            Hit
                                {|
                                    Judgement = None
                                    Missed = missed
                                    Delta = delta
                                    IsHold = true
                                |}
                    else
                        
                    match config.Accuracy.HoldNoteBehaviour with 
                    | HoldNoteBehaviour.OnlyJudgeReleases _ ->
                        if missed then this.State.BreakCombo true else this.State.IncrCombo()
                        Hit
                            {|
                                Judgement = None
                                Missed = missed
                                Delta = delta
                                IsHold = false
                            |}
                    | _ ->
                        this.State.Add(point_func delta judgement, 1.0, judgement)

                        if config.Judgements.[judgement].BreaksCombo then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Hit
                            {|
                                Judgement = Some judgement
                                Missed = missed
                                Delta = delta
                                IsHold = false
                            |}

                | Release_(delta, missed, overhold, dropped, missed_head) ->
                    let headJudgement = head_judgements.[ev.Column]

                    match config.Accuracy.HoldNoteBehaviour with
                    | HoldNoteBehaviour.Osu windows ->
                        let judgement =
                            RulesetUtils.osu_ln_judgement windows head_deltas.[ev.Column] delta overhold dropped

                        this.State.Add(point_func delta judgement, 1.0, judgement)

                        if config.Judgements.[judgement].BreaksCombo || missed_head then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Release
                            {|
                                Judgement = Some judgement
                                Missed = missed
                                Delta = delta
                                Overhold = overhold
                                Dropped = dropped
                            |}

                    | HoldNoteBehaviour.JustBreakCombo ->
                        if (not overhold) && (missed || dropped) then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Release
                            {|
                                Judgement = None
                                Missed = missed
                                Delta = delta
                                Overhold = overhold
                                Dropped = dropped
                            |}

                    | HoldNoteBehaviour.JudgeReleases d ->
                        let judgement = Helpers.window_func config.DefaultJudgement d.Timegates delta
                        this.State.Add(point_func delta judgement, 1.0, judgement)

                        if config.Judgements.[judgement].BreaksCombo then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Release
                            {|
                                Judgement = Some judgement
                                Missed = missed
                                Delta = delta
                                Overhold = overhold
                                Dropped = dropped
                            |}

                    | HoldNoteBehaviour.Normal rules ->
                        let judgement =
                            if overhold && not dropped then
                                max headJudgement rules.JudgementIfOverheld
                            elif missed || dropped then
                                max headJudgement rules.JudgementIfDropped
                            else
                                headJudgement

                        this.State.Add(point_func delta judgement, 1.0, judgement)

                        if config.Judgements.[judgement].BreaksCombo then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Release
                            {|
                                Judgement = Some judgement
                                Missed = missed
                                Delta = delta
                                Overhold = overhold
                                Dropped = dropped
                            |}

                    | HoldNoteBehaviour.OnlyJudgeReleases if_dropped ->
                        let judgement = 
                            if overhold || dropped then if_dropped else window_func delta
                        this.State.Add(point_func delta judgement, 1.0, judgement)

                        if config.Judgements.[judgement].BreaksCombo then
                            this.State.BreakCombo true
                        else
                            this.State.IncrCombo()

                        Release
                            {|
                                Judgement = Some judgement
                                Missed = missed
                                Delta = delta
                                Overhold = overhold
                                Dropped = dropped
                            |}
        }

module Metrics =

    let create (ruleset: Ruleset) (keys: int) (replay: IReplayProvider) notes rate : ScoreMetric =
        ScoreMetric(ruleset, keys, replay, notes, rate)

    let run (ruleset: Ruleset) (keys: int) (replay: IReplayProvider) notes rate : ScoreMetric =
        let scoring = ScoreMetric(ruleset, keys, replay, notes, rate)
        scoring.Update Time.infinity
        scoring

    open Prelude.Charts.Processing

    let create_dummy (chart: ModdedChart) : ScoreMetric =
        let ruleset = PremadeRulesets.SC.create 4
        create ruleset chart.Keys (StoredReplayProvider Array.empty) chart.Notes 1.0f
