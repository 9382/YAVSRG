﻿namespace Prelude.Charts.Processing.Difficulty

open System
open Prelude
open Prelude.Charts

(*
    Representation of hand/keyboard layouts.
    Used in the difficulty calculation to model how some things strain your hand(s) differently depending on which fingers are used
*)

type Layout =
    | Spread = 0
    | OneHand = 1
    | LeftOne = 2
    | RightOne = 3
    | LeftTwo = 4
    | RightTwo = 5
    | BMSLeft = 6
    | BMSRight = 7

module Layout =

    type Hand = int list
    type LayoutInfo = Hand list

    let get_finger_position k h =
        let rec f k h i =
            match h with
            | n :: fs -> if n = k then i else f k fs (i + 1)
            | [] -> -1

        f k h 0

    let get_hand_bit_mask (h: Hand) = h |> Seq.ofList |> Bitmask.ofSeq

    let info (l, keycount) : LayoutInfo option =
        match (l, keycount) with
        | (Layout.OneHand, 3) -> Some [ [ 0; 1; 2 ] ]
        | (Layout.LeftOne, 3) -> Some [ [ 0; 1 ]; [ 2 ] ]
        | (Layout.RightOne, 3) -> Some [ [ 0 ]; [ 1; 2 ] ]

        | (Layout.OneHand, 4) -> Some [ [ 0; 1; 2; 3 ] ]
        | (Layout.Spread, 4) -> Some [ [ 0; 1 ]; [ 2; 3 ] ]
        | (Layout.LeftOne, 4) -> Some [ [ 0; 1; 2 ]; [ 3 ] ]
        | (Layout.RightOne, 4) -> Some [ [ 0 ]; [ 1; 2; 3 ] ]

        | (Layout.OneHand, 5) -> Some [ [ 0; 1; 2; 3; 4 ] ]
        | (Layout.LeftOne, 5) -> Some [ [ 0; 1; 2 ]; [ 3; 4 ] ]
        | (Layout.RightOne, 5) -> Some [ [ 0; 1 ]; [ 2; 3; 4 ] ]
        | (Layout.LeftTwo, 5) -> Some [ [ 0; 1; 2; 3 ]; [ 4 ] ]
        | (Layout.RightTwo, 5) -> Some [ [ 0 ]; [ 1; 2; 3; 4 ] ]

        | (Layout.Spread, 6) -> Some [ [ 0; 1; 2 ]; [ 3; 4; 5 ] ]
        | (Layout.LeftOne, 6) -> Some [ [ 0; 1; 2; 3 ]; [ 4; 5 ] ]
        | (Layout.RightOne, 6) -> Some [ [ 0; 1 ]; [ 2; 3; 4; 5 ] ]
        | (Layout.LeftTwo, 6) -> Some [ [ 0; 1; 2; 3; 4 ]; [ 5 ] ]
        | (Layout.RightTwo, 6) -> Some [ [ 0 ]; [ 1; 2; 3; 4; 5 ] ]

        | (Layout.LeftOne, 7) -> Some [ [ 0; 1; 2; 3 ]; [ 4; 5; 6 ] ]
        | (Layout.RightOne, 7) -> Some [ [ 0; 1; 2 ]; [ 3; 4; 5; 6 ] ]
        | (Layout.LeftTwo, 7) -> Some [ [ 0; 1; 2; 3; 4 ]; [ 5; 6 ] ]
        | (Layout.RightTwo, 7) -> Some [ [ 0; 1 ]; [ 2; 3; 4; 5; 6 ] ]
        | (Layout.BMSLeft, 7) -> Some [ [ 0; 1; 3; 2 ]; [ 4; 5; 6 ] ]
        | (Layout.BMSRight, 7) -> Some [ [ 0; 1; 2 ]; [ 4; 3; 5; 6 ] ]

        | (Layout.Spread, 8) -> Some [ [ 0; 1; 2; 3 ]; [ 4; 5; 6; 7 ] ]
        | (Layout.LeftOne, 8) -> Some [ [ 0; 1; 2 ]; [ 3; 4; 5; 6; 7 ] ]
        | (Layout.RightOne, 8) -> Some [ [ 0; 1; 2; 3; 4 ]; [ 5; 6; 7 ] ]

        | (Layout.LeftOne, 9) -> Some [ [ 0; 1; 2; 3; 4 ]; [ 5; 6; 7; 8 ] ]
        | (Layout.RightOne, 9) -> Some [ [ 0; 1; 2; 3 ]; [ 4; 5; 6; 7; 8 ] ]

        | (Layout.Spread, 10) -> Some [ [ 0; 1; 2; 3; 4 ]; [ 5; 6; 7; 8; 9 ] ]

        | _ -> None

    let name (l, keycount) =
        match (l, keycount) with
        | (Layout.OneHand, 3) -> "One-Handed"
        | (Layout.LeftOne, 3) -> "2k+1"
        | (Layout.RightOne, 3) -> "1k+2"

        | (Layout.OneHand, 4) -> "One-Handed"
        | (Layout.Spread, 4) -> "Spread"
        | (Layout.LeftOne, 4) -> "3k+1"
        | (Layout.RightOne, 4) -> "1k+3"

        | (Layout.OneHand, 5) -> "One-Handed"
        | (Layout.LeftOne, 5) -> "3k+2"
        | (Layout.RightOne, 5) -> "2k+3"
        | (Layout.LeftTwo, 5) -> "4k+1"
        | (Layout.RightTwo, 5) -> "1k+4"

        | (Layout.Spread, 6) -> "Spread"
        | (Layout.LeftOne, 6) -> "4k+2"
        | (Layout.RightOne, 6) -> "2k+4"
        | (Layout.LeftTwo, 6) -> "5k+1"
        | (Layout.RightTwo, 6) -> "1k+5"

        | (Layout.LeftOne, 7) -> "Left Thumb"
        | (Layout.RightOne, 7) -> "Right Thumb"
        | (Layout.LeftTwo, 7) -> "5k+2"
        | (Layout.RightTwo, 7) -> "2k+5"
        | (Layout.BMSLeft, 7) -> "IIDX Left Thumb"
        | (Layout.BMSRight, 7) -> "IIDX Right Thumb"

        | (Layout.Spread, 8) -> "Spread"
        | (Layout.LeftOne, 8) -> "5k+3"
        | (Layout.RightOne, 8) -> "3k+5"

        | (Layout.LeftOne, 9) -> "Left Thumb"
        | (Layout.RightOne, 9) -> "Right Thumb"

        | (Layout.Spread, 10) -> "Spread"

        | _ -> "Unknown Layout"

    let list (k: int) =
        [
            (Layout.Spread, k)
            (Layout.OneHand, k)
            (Layout.LeftOne, k)
            (Layout.RightOne, k)
            (Layout.LeftTwo, k)
            (Layout.RightTwo, k)
            (Layout.BMSLeft, k)
            (Layout.BMSRight, k)
        ]
        |> List.filter (info >> Option.isSome)
        |> List.map (fun (a, b) -> a)

(*
    Difficulty calculation
    To be one day scrapped as it gets overshadowed by a better system for picking what to play
    Is just a port of the original C# version I wrote when I was 18
*)

type DifficultyRating =
    {
        Physical: float
        Technical: float

        PhysicalData: float array
        TechnicalData: float array

        PhysicalComposite: float array2d
        TechnicalComposite: float array2d
    }

module DifficultyRating =

    let private jackCurve delta =
        let widthScale = 0.02
        let heightScale = 26.3
        let curveExp = 1.0
        Math.Min(heightScale / Math.Pow(widthScale * float delta, curveExp), 20.0)

    let private streamCurve delta =
        let widthScale = 0.02
        let heightScale = 13.7
        let curveExp = 1.0
        let cutoff = 10.0

        Math.Max(
            (heightScale / Math.Pow(widthScale * float delta, curveExp)
             - 0.1 * heightScale / Math.Pow(widthScale * float delta, curveExp * cutoff)),
            0.0
        )

    let private jackCompensation jackDelta streamDelta =
        Math.Min(Math.Pow(Math.Max(Math.Log(float (jackDelta / streamDelta), 2.0), 0.0), 2.0), 1.0)

    let private rootMeanPower values power =
        match values with
        | x :: [] -> x
        | [] -> 0.0
        | xs ->
            let (count, sumpow) =
                List.fold (fun (count, a) b -> (count + 1.0, a + Math.Pow(b, power))) (0.0, 0.0) xs

            Math.Pow(sumpow / count, 1.0 / power)

    let stamina_func value input (delta: GameplayTime) =
        let staminaBaseFunc ratio = 1.0 + 0.105 * ratio
        let staminaDecayFunc delta = Math.Exp(-0.00044 * delta)
        let v = Math.Max(value * staminaDecayFunc (float delta), 0.01)
        v * staminaBaseFunc (input / v)

    let private overallDifficulty arr =
        Math.Pow(Array.fold (fun v x -> v * Math.Exp(0.01 * Math.Max(0.0, Math.Log(x / v)))) 0.01 arr, 0.6)
        * 2.5

    let private OHTNERF = 3.0
    let private SCALING_VALUE = 0.55

    let private calculate_uncached (rate: Rate) (notes: TimeArray<NoteRow>) : DifficultyRating =

        let keys = notes.[0].Data.Length

        let layoutData =
            Layout.list keys
            |> List.head
            |> fun l -> Layout.info (l, keys) |> fun x -> x.Value

        let fingers = Array.zeroCreate<Time> keys

        let physicalData = Array.zeroCreate notes.Length
        let technicalData = Array.zeroCreate notes.Length

        let delta = Array2D.zeroCreate notes.Length keys
        let jack = Array2D.zeroCreate notes.Length keys
        let trill = Array2D.zeroCreate notes.Length keys
        let anchor = Array2D.zeroCreate notes.Length keys
        let physicalComposite = Array2D.zeroCreate notes.Length keys
        let technicalComposite = Array2D.zeroCreate notes.Length keys

        let updateNoteDifficulty (column, index, offset: Time, otherColumns: Bitmask) =
            let s = otherColumns |> Bitmask.unset_key column

            let delta1 =
                if fingers.[column] > 0.0f<ms> then
                    delta.[index, column] <- (offset - fingers.[column]) / rate
                    jack.[index, column] <- Math.Pow(jackCurve (delta.[index, column]), OHTNERF)
                    delta.[index, column]
                else
                    10000.0f<ms / rate>

            for k in Bitmask.toSeq s do
                if fingers.[k] > 0.0f<ms> then
                    let delta2 = (offset - fingers.[k]) / rate

                    trill.[index, column] <-
                        trill.[index, column]
                        + Math.Pow((streamCurve delta2) * (jackCompensation delta1 delta2), OHTNERF)

            physicalComposite.[index, column] <- Math.Pow(trill.[index, column] + jack.[index, column], 1.0 / OHTNERF)
            technicalComposite.[index, column] <- Math.Pow(jack.[index, column], 1.0 / OHTNERF)

        let snapDifficulty (strain: float array) mask =
            let mutable vals = []

            for k in Bitmask.toSeq mask do
                vals <- strain.[k] :: vals

            rootMeanPower vals 1.0

        let (physical, technical) =
            let lastHandUse = Array.zeroCreate (List.length layoutData)
            let currentStrain = Array.zeroCreate<float> keys
            let mutable i = 0

            for { Time = offset; Data = nr } in notes do
                let hits =
                    seq {
                        for k = 0 to keys - 1 do
                            if nr.[k] = NoteType.NORMAL || nr.[k] = NoteType.HOLDHEAD then
                                yield k
                    }
                    |> Bitmask.ofSeq

                if hits > 0us then
                    List.fold
                        (fun h hand ->
                            let handHits = hits &&& Layout.get_hand_bit_mask hand

                            for k in Bitmask.toSeq handHits do
                                updateNoteDifficulty (k, i, offset, Layout.get_hand_bit_mask hand)

                                currentStrain.[k] <-
                                    stamina_func
                                        (currentStrain.[k])
                                        (physicalComposite.[i, k] * SCALING_VALUE)
                                        ((offset - fingers.[k]) / rate)

                            for k in Bitmask.toSeq handHits do
                                fingers.[k] <- offset

                            lastHandUse.[h] <- offset
                            h + 1
                        )
                        0
                        layoutData
                    |> ignore

                    physicalData.[i] <- snapDifficulty currentStrain hits
                    technicalData.[i] <- Bitmask.count hits |> float

                i <- i + 1

            (overallDifficulty physicalData, overallDifficulty technicalData)

        {
            Physical = if Double.IsFinite physical then physical else 0.0
            Technical = if Double.IsFinite technical then technical else 0.0

            PhysicalData = physicalData
            TechnicalData = technicalData

            PhysicalComposite = physicalComposite
            TechnicalComposite = technicalComposite
        }

    let calculate = calculate_uncached |> cached

    let technical_color v =
        try
            let a = Math.Min(1.0, v * 0.1)
            let b = Math.Min(1.0, Math.Max(1.0, v * 0.1) - 1.0)
            Color.FromArgb(255.0 * (1.0 - a) |> int, 255.0 * b |> int, 255.0 * a |> int)
        with _ ->
            Color.Blue

    let physical_color v =
        try
            let a = Math.Min(1.0, v * 0.1)
            let b = Math.Min(1.0, Math.Max(1.0, v * 0.1) - 1.0)
            Color.FromArgb(255.0 * a |> int, 255.0 * (1.0 - a) |> int, 255.0 * b |> int)
        with _ ->
            Color.Red
