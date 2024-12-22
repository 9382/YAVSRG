﻿namespace Interlude.Content

open System
open System.IO
open System.Collections.Generic
open Percyqaz.Common
open Prelude
open Prelude.Gameplay.Rulesets

module Rulesets =

    let DEFAULT_ID = "sc-j4"
    let DEFAULT = SC.create 4
    let DEFAULT_HASH = Ruleset.hash DEFAULT

    let mutable private initialised = false
    let private loaded = Dictionary<string, Ruleset>()
    let mutable current = DEFAULT
    let mutable current_hash = DEFAULT_HASH
    let private _selected_id = Setting.simple DEFAULT_ID
    let private on_changed_ev = Event<Ruleset>()
    let on_changed = on_changed_ev.Publish

    let load () =
        loaded.Clear()

        let path = get_game_folder "Rulesets"
        let default_path = Path.Combine(path, DEFAULT_ID + ".ruleset")

        if not (File.Exists default_path) then
            JSON.ToFile (default_path, true) DEFAULT

        for f in Directory.GetFiles(path) do
            if Path.GetExtension(f).ToLower() = ".ruleset" then
                let id = Path.GetFileNameWithoutExtension(f)

                match JSON.FromFile<Ruleset>(f) with
                | Ok rs ->
                    match Ruleset.check rs with
                    | Ok rs -> loaded.Add(id, rs)
                    | Error reason -> Logging.Error "Error validating ruleset '%s': %s" id reason
                | Error e -> Logging.Error "Error loading ruleset '%s': %O" id e

        if not (loaded.ContainsKey DEFAULT_ID) then
            loaded.Add(DEFAULT_ID, DEFAULT)

        if not (loaded.ContainsKey _selected_id.Value) then
            Logging.Warn "Ruleset '%s' not found, switching to default" _selected_id.Value
            _selected_id.Value <- DEFAULT_ID

        current <- loaded.[_selected_id.Value]
        current_hash <- Ruleset.hash current

    let init_window () =
        load ()
        initialised <- true

    let selected_id =
        Setting.make
            (fun new_id ->
                if initialised then
                    if not (loaded.ContainsKey new_id) then
                        Logging.Warn "Ruleset '%s' not found, switching to default" new_id
                        _selected_id.Value <- DEFAULT_ID
                    else
                        _selected_id.Value <- new_id

                    current <- loaded.[_selected_id.Value]
                    current_hash <- Ruleset.hash current
                    on_changed_ev.Trigger current
                else
                    _selected_id.Value <- new_id
            )
            (fun () -> _selected_id.Value)

    let by_id (id) = loaded.[id]

    let by_hash (hash) =
        loaded.Values |> Seq.tryFind (fun rs -> Ruleset.hash rs = hash)

    let list () =
        seq {
            for k in loaded.Keys do
                yield (k, loaded.[k])
        }
        |> Seq.sortBy (fun (_, rs) -> rs.Name)

    let exists = loaded.ContainsKey

    let install (ruleset: Ruleset) =

        let new_id : string =
            ruleset.Name
            |> Seq.filter (function '\'' | '_' | '.' | ' ' -> true | c -> Char.IsAsciiLetterOrDigit c)
            |> Array.ofSeq
            |> String
            |> fun s -> s.ToLowerInvariant()
            |> fun s -> s.Split(' ', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> String.concat "-"

        let new_id = if exists new_id then sprintf "%s_%i" new_id (Timestamp.now()) else new_id

        assert(not (loaded.ContainsKey new_id))

        loaded.Add(new_id, ruleset)

        JSON.ToFile (Path.Combine(get_game_folder "Rulesets", new_id + ".ruleset"), true) ruleset

    let update (existing_id: string) (ruleset: Ruleset) =

        let new_id : string =
            ruleset.Name
            |> Seq.filter (function '\'' | '_' | '.' | ' ' -> true | c -> Char.IsAsciiLetterOrDigit c)
            |> Array.ofSeq
            |> String
            |> fun s -> s.ToLowerInvariant()
            |> fun s -> s.Split(' ', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> String.concat "-"

        let new_id = if new_id <> existing_id && exists new_id then sprintf "%s_%i" new_id (Timestamp.now()) else new_id

        let new_id =
            try
                if new_id <> existing_id then
                    File.Move(Path.Combine(get_game_folder "Rulesets", existing_id + ".ruleset"), Path.Combine(get_game_folder "Rulesets", new_id + ".ruleset"))
                new_id
            with err ->
                Logging.Error "Failed to move ruleset file from '%s' to '%s': %O" existing_id new_id err
                existing_id

        if loaded.Remove(existing_id) then
            loaded.Add(new_id, ruleset)

            if _selected_id.Value = existing_id then
                _selected_id.Value <- new_id
                current <- ruleset
                current_hash <- Ruleset.hash current
                on_changed_ev.Trigger current

            JSON.ToFile (Path.Combine(get_game_folder "Rulesets", new_id + ".ruleset"), true) ruleset

    let delete (id: string) =
        if id <> DEFAULT_ID && exists id then
            try
                File.Delete(Path.Combine(get_game_folder "Rulesets", id + ".ruleset"))
                loaded.Remove id
            with
            | :? IOException as err ->
                Logging.Error "IO error deleting ruleset: %O" err
                false
        else false