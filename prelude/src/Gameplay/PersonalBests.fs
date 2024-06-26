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

type PersonalBests<'T> = ('T * float32) list

module PersonalBests =

    let rec get_best_above_with_rate (minimum_rate: float32) (bests: PersonalBests<'T>) =
        match bests with
        | [] -> None
        | (value, rate) :: xs ->
            if rate = minimum_rate then
                Some(value, rate)
            elif rate < minimum_rate then
                None
            else
                get_best_above_with_rate minimum_rate xs
                |> function
                    | None -> Some(value, rate)
                    | Some x -> Some x

    let get_best_above minimum_rate =
        get_best_above_with_rate minimum_rate >> Option.map fst

    let rec get_best_below_with_rate (maximum_rate: float32) (bests: PersonalBests<'T>) =
        match bests with
        | [] -> None
        | (value, rate) :: xs ->
            if rate > maximum_rate then
                get_best_below_with_rate maximum_rate xs
            else
                Some(value, rate)

    let get_best_below maximum_rate =
        get_best_below_with_rate maximum_rate >> Option.map fst

    let create (value: 'T, rate: float32) : PersonalBests<'T> = [ value, rate ]

    let inline update (value: 'T, rate: float32) (bests: PersonalBests<'T>) : PersonalBests<'T> * Improvement<'T> =
        let rec remove_worse_breakpoints (v: 'T) (bests: PersonalBests<'T>) =
            match bests with
            | [] -> []
            | (value, _) :: xs when value <= v -> remove_worse_breakpoints v xs
            | xs -> xs

        let rec loop (xs: PersonalBests<'T>) : PersonalBests<'T> * Improvement<'T> =
            match xs with
            | [] -> (value, rate) :: [], Improvement.New
            | (v, r) :: xs ->
                if rate < r && value > v then
                    let res, imp = loop xs in (v, r) :: res, imp
                elif rate < r then
                    (v, r) :: xs, Improvement.None
                elif rate = r && value > v then
                    (value, rate) :: remove_worse_breakpoints value xs, Improvement.Better(value - v)
                elif rate = r then
                    (v, r) :: xs, Improvement.None
                else if value > v then
                    (value, rate) :: remove_worse_breakpoints value xs, Improvement.FasterBetter(rate - r, value - v)
                elif value = v then
                    (value, rate) :: remove_worse_breakpoints value xs, Improvement.Faster(rate - r)
                else
                    (value, rate) :: (v, r) :: xs, Improvement.New

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
    
    let ruleset_best_below<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (maximum_rate: float32) (map: Map<string, Bests>) : 'T option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_below maximum_rate (property bests)
    
    let ruleset_best_above<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (minimum_rate: float32) (map: Map<string, Bests>) : 'T option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_above minimum_rate (property bests)
    
    let ruleset_best_below_with_rate<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (maximum_rate: float32) (map: Map<string, Bests>) : ('T * float32) option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_below_with_rate maximum_rate (property bests)
    
    let ruleset_best_above_with_rate<'T> (ruleset: string) (property: Bests -> PersonalBests<'T>) (minimum_rate: float32) (map: Map<string, Bests>) : ('T * float32) option =
        match map.TryFind ruleset with
        | None -> None
        | Some bests -> PersonalBests.get_best_above_with_rate minimum_rate (property bests)