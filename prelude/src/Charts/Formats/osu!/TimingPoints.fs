﻿namespace Prelude.Charts.Formats.osu

open System.Globalization

type TimingEffect =
    | None = 0
    | Kiai = 1
    | OmitFirstBarline = 8

type UninheritedTimingPoint =
    {
        Time: float
        MsPerBeat: float
        Meter: int
        SampleSet: SampleSet
        SampleIndex: int
        Volume: int
        Effects: TimingEffect
    }
    override this.ToString() =
        sprintf "%s,%s,%i,%i,%i,%i,1,%i"
            (this.Time.ToString(CultureInfo.InvariantCulture))
            (this.MsPerBeat.ToString(CultureInfo.InvariantCulture))
            this.Meter
            (int this.SampleSet)
            this.SampleIndex
            this.Volume
            (int this.Effects)
    static member inline Create(time: ^X, ms_per_beat: ^Y, meter: ^Z) =
        {
            Time = int time
            MsPerBeat = float ms_per_beat
            Meter = int meter
            SampleSet = SampleSet.Soft
            SampleIndex = 0
            Volume = 10
            Effects = TimingEffect.None
        }

type InheritedTimingPoint =
    {
        Time: float
        Multiplier: float
        SampleSet: SampleSet
        SampleIndex: int
        Volume: int
        Effects: TimingEffect
    }
    override this.ToString() =
        sprintf "%s,%s,4,%i,%i,%i,0,%i"
            (this.Time.ToString(CultureInfo.InvariantCulture))
            (-100.0 / this.Multiplier |> fun f -> f.ToString(CultureInfo.InvariantCulture))
            (int this.SampleSet)
            this.SampleIndex
            this.Volume
            (int this.Effects)
    static member inline Create(time: ^Z, multiplier: ^Y) =
        {
            Time = int time
            Multiplier = float multiplier
            SampleSet = SampleSet.Soft
            SampleIndex = 0
            Volume = 10
            Effects = TimingEffect.None
        }

type TimingPoint =
    | Uninherited of UninheritedTimingPoint
    | Inherited of InheritedTimingPoint
    member this.Time =
        match this with
        | Uninherited x -> x.Time
        | Inherited x -> x.Time
    override this.ToString() =
        match this with
        | Uninherited x -> x.ToString()
        | Inherited x -> x.ToString()
    static member inline CreateBPM(time, ms_per_beat, meter) =
        UninheritedTimingPoint.Create(time, ms_per_beat, meter) |> Uninherited
    static member inline CreateSV(time, multiplier) =
        InheritedTimingPoint.Create(time, multiplier) |> Inherited