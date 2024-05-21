﻿namespace Interlude.Features.Play

open System
open Prelude
open Prelude.Charts
open Prelude.Charts.Processing
open Prelude.Gameplay
open Interlude.Features.Gameplay
open Interlude.Features.Pacemaker

type PlayState =
    {
        Chart: Chart
        WithColors: ColoredChart
        mutable Scoring: IScoreMetric
        ScoringChanged: Event<unit>
        CurrentChartTime: unit -> ChartTime
        Pacemaker: PacemakerState
    }
    member this.Ruleset = this.Scoring.Ruleset

    static member Dummy(info: LoadedChartInfo) =
        let s = Metrics.create_dummy info.WithMods

        {
            Chart = info.Chart
            WithColors = info.WithColors
            Scoring = s
            ScoringChanged = Event<unit>()
            CurrentChartTime = fun () -> 0.0f<ms>
            Pacemaker = PacemakerState.None
        }

    member this.SubscribeToHits(handler: HitEvent<HitEventGuts> -> unit) =
        let mutable obj: IDisposable = this.Scoring.OnHit.Subscribe handler

        this.ScoringChanged.Publish.Add(fun () ->
            obj.Dispose()
            obj <- this.Scoring.OnHit.Subscribe handler
        )

    member this.ChangeScoring(scoring) =
        this.Scoring <- scoring
        this.ScoringChanged.Trigger()
