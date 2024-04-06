﻿namespace Prelude.Gameplay.Mods

open System
open Prelude
open Prelude.Charts
open Prelude.Charts.Processing

(*
    Marker for status of mods.
        0 = This is a non-silly mod suitable for online upload, personal bests, leaderboards
        1 = This is a for-fun mod that may transform a chart in large ways or are otherwise not suitable for leaderboards e.g. randomiser
        2 = Scores with this mod enabled should not be saved at all e.g. something experimental or still in development
*)
type ModStatus =
    | Ranked = 0
    | Unranked = 1
    | Unstored = 2

type ModState = Map<string, int>

(*
    Mods (Modifiers) are a set of optional effects that can be enabled for a chart before playing it
    They will change something about the chart that can vary gameplay in interesting ways
    Mods will have an integer "state" passed to them when they are applied - This allows some sub-behaviours within mods
    RandomSeed indicates that state should be randomised and is used to seed random behaviour for the mod
        (This number is then saved so the mod/score can be replicated for replay purposes)

    Sanity rules
        - 'Check' should not modify anything about the Chart passed
        - 'Apply' should not modify the header of the Chart passed
*)
type Mod =
    {
        Status: ModStatus
        States: int
        RandomSeed: bool
        Exclusions: string list
        // Returns resulting chart + flag
        // flag is true if the mod made meaningful changes to the chart
        // The resulting chart can still be modified in ways that don't affect gameplay such as changing BPMs
        Apply: int -> ModdedChartInternal -> ModdedChartInternal * bool
    }
    static member internal Default =
        {
            Status = ModStatus.Unstored
            States = 1
            Exclusions = []
            RandomSeed = false
            Apply = (fun _ mc -> mc, false)
        }

module Mods =

    let AVAILABLE_MODS : Map<string, Mod> = 
        Map.ofList [

            "mirror",
            { Mod.Default with
                Status = ModStatus.Ranked
                Exclusions = [ "shuffle"; "random" ]
                Apply = fun _ mc -> Mirror.apply mc
            }

            "shuffle",
            { Mod.Default with
                Status = ModStatus.Unstored
                RandomSeed = true
                Apply = fun s mc -> Randomise.shuffle s mc
                Exclusions = [ "random"; "mirror" ]
            }
            
            "random",
            { Mod.Default with
                Status = ModStatus.Unstored
                RandomSeed = true
                Apply = fun s mc -> Randomise.randomise s mc
                Exclusions = [ "shuffle"; "mirror" ]
            }

            "nosv",
            { Mod.Default with
                Status = ModStatus.Unranked
                Apply = fun _ mc -> NoSV.apply mc
            }

            "noln",
            { Mod.Default with
                Status = ModStatus.Unranked
                Exclusions = [ "inverse" ]
                Apply = fun _ mc -> NoLN.apply mc
            }

            "inverse",
            { Mod.Default with
                Status = ModStatus.Unranked
                States = 1 // todo: in future support half-size/double-size gaps? code is already there
                Exclusions = [ "noln" ]
                Apply = fun s mc -> Inverse.apply (s > 0) mc
            }

            "more_notes",
            { Mod.Default with
                Status = ModStatus.Unstored
                States = 2
                Apply = fun s mc -> if s = 1 then MoreNotes.apply_chordjacks mc else MoreNotes.apply_minijacks mc
            }
        ]

    let APPLICATION_PRIORITY_ORDER =
        [
            "noln"
            "more_notes"
            "mirror"
            "shuffle"
            "random"
            "inverse"
            "nosv"
        ]

    let MENU_DISPLAY_ORDER =
        [
            "mirror"
            "random"
            "shuffle"
            "inverse"
            "more_notes"
            "noln"
            "nosv"
        ]

    do
        assert(APPLICATION_PRIORITY_ORDER.Length = AVAILABLE_MODS.Count)
        assert(MENU_DISPLAY_ORDER.Length = AVAILABLE_MODS.Count)

    let private r = new Random()

    let name (id: string) (state: int option) =
        match state with
        | Some i when i > 0 -> Localisation.localise (sprintf "mod.%s.%i.name" id i)
        | _ -> Localisation.localise (sprintf "mod.%s.name" id)

    let desc (id: string) (state: int option) =
        match state with
        | Some i when i > 0 -> Localisation.localise (sprintf "mod.%s.%i.desc" id i)
        | _ -> Localisation.localise (sprintf "mod.%s.desc" id)

    let cycle (id: string) (mods: ModState) : ModState =
        if (mods.ContainsKey id) then
            let state = mods.[id] + 1

            if state = AVAILABLE_MODS.[id].States || AVAILABLE_MODS.[id].RandomSeed then
                Map.remove id mods
            else
                Map.add id state mods
        else
            let state =
                if AVAILABLE_MODS.[id].RandomSeed then
                    r.Next(-Int32.MinValue,0)
                else
                    0

            List.fold (fun m i -> Map.remove i m) (Map.add id state mods) AVAILABLE_MODS.[id].Exclusions

    let in_priority_order (mods: ModState) =
        APPLICATION_PRIORITY_ORDER
        |> Seq.choose (fun id ->
            match Map.tryFind id mods with
            | Some state -> Some (id, AVAILABLE_MODS.[id], state)
            | None -> None
        )

    (*
        Mod application pipeline
    *)

    let apply (mods: ModState) (chart: Chart) : ModdedChart =
        let mutable modchart_internal =
            {
                Keys = chart.Keys
                Notes = chart.Notes
                SV = chart.SV
                BPM = chart.BPM
            }

        let mutable mods_applied = []

        for id, m, state in in_priority_order mods do
            let new_mc, mod_was_applied = m.Apply state modchart_internal

            if mod_was_applied then
                mods_applied <- mods_applied @ [ id ]
                modchart_internal <- new_mc

        {
            Keys = modchart_internal.Keys
            Notes = modchart_internal.Notes
            SV = modchart_internal.SV
            BPM = modchart_internal.BPM

            ModsSelected = mods
            ModsApplied = List.fold (fun m i -> Map.add i mods.[i] m) Map.empty mods_applied
        }

    let format (rate: float32, mods: ModState, autoplay: bool) =
        String.Join(
            ", ",
            sprintf "%.2fx" rate
            :: (mods
                |> in_priority_order
                |> Seq.map (fun (id, _, state) -> name id (Some state))
                |> List.ofSeq)
        )
        + if autoplay then ", " + name "auto" None else ""

    let check (mods: ModState) =
        try
            let mutable status = ModStatus.Ranked

            for m in mods.Keys do
                if AVAILABLE_MODS.ContainsKey m then
                    status <- max status AVAILABLE_MODS.[m].Status

                    if mods.[m] >= AVAILABLE_MODS.[m].States then
                        failwithf "Mod '%s' in invalid state %i" m mods.[m]

                    for e in AVAILABLE_MODS.[m].Exclusions do
                        if mods.ContainsKey e then
                            failwithf "Mods '%s' and '%s' cannot both be selected" m e
                else
                    failwithf "No such mod '%s'" m

            Ok status
        with err ->
            Error err.Message
