﻿namespace Prelude.Gameplay

open Percyqaz.Data

[<RequireQualifiedAccess>]
type Improvement<'T> =
    | FasterBetter of rate_increase: float32 * improvement: 'T
    | Faster of rate_increase: float32
    | Better of improvement: 'T
    | New
    | None

type ImprovementFlags =
    {
        Lamp: Improvement<int>
        Accuracy: Improvement<float>
        Grade: Improvement<int>
    }
    static member None =
        {
            Lamp = Improvement.None
            Accuracy = Improvement.None
            Grade = Improvement.None
        }

    static member New =
        {
            Lamp = Improvement.New
            Accuracy = Improvement.New
            Grade = Improvement.New
        }

type PersonalBestEntry<'T> = 'T * float32 * int64 // Value, Rate, Timestamp
type PersonalBests<'T> = PersonalBestEntry<'T> list

module PersonalBests =

    let rec get_best_above (minimum_rate: float32) (bests: PersonalBests<'T>) : PersonalBestEntry<'T> option =
        match bests with
        | [] -> None
        | (value, rate, timestamp) :: xs ->
            if rate = minimum_rate then
                Some(value, rate, timestamp)
            elif rate < minimum_rate then
                None
            else
                get_best_above minimum_rate xs
                |> function
                    | None -> Some(value, rate, timestamp)
                    | Some x -> Some x

    let rec get_best_below (maximum_rate: float32) (bests: PersonalBests<'T>) : PersonalBestEntry<'T> option =
        match bests with
        | [] -> None
        | (value, rate, timestamp) :: xs ->
            if rate > maximum_rate then
                get_best_below maximum_rate xs
            else
                Some(value, rate, timestamp)

    let create (value: 'T, rate: float32, timestamp: int64) : PersonalBests<'T> = [ value, rate, timestamp ]

    let inline update (value: 'T, rate: float32, timestamp: int64) (bests: PersonalBests<'T>) : PersonalBests<'T> * Improvement<'T> =
        let rec remove_worse_breakpoints (v: 'T) (bests: PersonalBests<'T>) =
            match bests with
            | [] -> []
            | (value, _, _) :: xs when value <= v -> remove_worse_breakpoints v xs
            | xs -> xs

        let rec loop (xs: PersonalBests<'T>) : PersonalBests<'T> * Improvement<'T> =
            match xs with
            | [] -> (value, rate, timestamp) :: [], Improvement.New
            | (v, r, t) :: xs ->
                if rate < r && value > v then
                    let res, imp = loop xs in (v, r, t) :: res, imp
                elif rate < r then
                    (v, r, t) :: xs, Improvement.None
                elif rate = r && value > v then
                    (value, rate, timestamp) :: remove_worse_breakpoints value xs, Improvement.Better(value - v)
                elif rate = r then
                    (v, r, t) :: xs, Improvement.None
                else if value > v then
                    (value, rate, timestamp) :: remove_worse_breakpoints value xs, Improvement.FasterBetter(rate - r, value - v)
                elif value = v then
                    (value, rate, timestamp) :: remove_worse_breakpoints value xs, Improvement.Faster(rate - r)
                else
                    (value, rate, timestamp) :: (v, r, t) :: xs, Improvement.New

        loop bests

[<Json.AutoCodec(true)>]
type Bests =
    {
        Lamp: PersonalBests<int>
        Accuracy: PersonalBests<float>
        Grade: PersonalBests<int>
    }
    static member Default = { Lamp = []; Accuracy = []; Grade = [] }

module Bests =
    
    let ruleset_best_below<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (maximum_rate: float32) (map: Map<string, Bests>) : PersonalBestEntry<'T> option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_below maximum_rate (property bests)
    
    let ruleset_best_above<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (minimum_rate: float32) (map: Map<string, Bests>) : PersonalBestEntry<'T> option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_above minimum_rate (property bests)