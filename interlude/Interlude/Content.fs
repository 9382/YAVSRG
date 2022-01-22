﻿namespace Interlude

open System
open System.IO
open System.Collections.Generic
open Prelude.Common
open Prelude.Scoring
open Prelude.Data.Themes
open Interlude.Graphics

module Content =

    let mutable accentColor = ThemeConfig.Default.DefaultAccentColor
    let mutable font : Fonts.SpriteFont = null

    module Sprites =
        
        let private cache = new Dictionary<string, Sprite>()
        
        let getTexture (id: string) =
            if cache.ContainsKey id then cache.[id]
            else failwithf "should have already loaded %s" id

        let add (id: string) (s: Sprite) = 
            if cache.ContainsKey id then
                Sprite.destroy cache.[id]
                cache.Remove id |> ignore
            cache.Add(id, s)

    module Sounds =
        "not yet implemented"
        |> ignore

    module Themes =
        
        let private _default = Theme.FromZipStream <| Utils.getResourceStream "default.zip"

        let private loaded = Dictionary<string, Theme>()

        module Current =

            let mutable id = "*default"
            let mutable instance = _default
            let mutable config = instance.Config

            module Rulesets =
                
                let private defaultRulesets =
                    List.map
                        (fun od -> (sprintf "osu-od-%.0f" od, Rulesets.Osu.create od))
                        [0.0f; 1.0f; 2.0f; 3.0f; 4.0f; 5.0f; 6.0f; 7.0f; 8.0f; 9.0f; 10.0f]
                    @ List.map
                        (fun j -> (sprintf "sc-j%i" j, Rulesets.SC.create j))
                        [1; 2; 3; 4; 5; 6; 7; 8; 9]
                    @ List.map
                        (fun j -> (sprintf "wife-j%i" j, Rulesets.Wife.create j))
                        [1; 2; 3; 4; 5; 6; 7; 8; 9]
                    @ List.map
                        (fun (d: Rulesets.Ex_Score.Type) -> (sprintf "xs-%s" (d.Name.ToLower()), Rulesets.Ex_Score.create d))
                        [Rulesets.Ex_Score.sdvx]

                let loaded = 
                    let x = Dictionary<string, Ruleset>()
                    for name, rs in defaultRulesets do x.Add("*" + name, rs)
                    x

                let reload() =
                    loaded.Clear()
                    for name, rs in defaultRulesets do loaded.Add("*" + name, rs)
                    for name, rs in instance.GetRulesets() do loaded.Add(name, rs)

                let exists = loaded.ContainsKey

            module GameplayConfig =

                open WidgetConfig

                let private cache = Dictionary<string, obj>()

                let private add<'T>() =
                    let id = typeof<'T>.Name
                    cache.Remove(id) |> ignore
                    cache.Add(id, fst (instance.GetGameplayConfig<'T> id))

                let reload() =
                    add<AccuracyMeter>()
                    add<HitMeter>()
                    add<LifeMeter>()
                    add<Combo>()
                    add<SkipButton>()
                    add<ProgressMeter>()
                    add<JudgementMeter>()
            
                let get<'T>() = 
                    let id = typeof<'T>.Name
                    if cache.ContainsKey id then
                        cache.[id] :?> 'T
                    else failwithf "config not loaded: %s" id

            let changeConfig(new_config: ThemeConfig) =
                instance.Config <- new_config
                config <- instance.Config

            let reload() =
                if config.OverrideAccentColor then accentColor <- config.DefaultAccentColor
                for font in _default.GetFonts() do
                    Fonts.add font
                for font in instance.GetFonts() do
                    Fonts.add font
                if font <> null then font.Dispose()
                font <- Fonts.create config.Font

                for id in themeTextures do
                    match instance.GetTexture id with
                    | Some (img, config) -> Sprite.upload(img, config.Rows, config.Columns, false) |> Sprite.cache id |> Sprites.add id
                    | None ->
                        match loaded.["*default"].GetTexture id with
                        | Some (img, config) -> Sprite.upload(img, config.Rows, config.Columns, false) |> Sprite.cache id |> Sprites.add id
                        | None -> failwith "default doesnt have this texture!!"

                GameplayConfig.reload()
                Rulesets.reload()

            let switch (new_id: string) =
                let new_id = if loaded.ContainsKey new_id then new_id else Logging.Warn("Theme '" + new_id + "' not found, switching to default"); "*default"
                if new_id <> id || font = null then // font = null acts as flag for first load
                    id <- id
                    instance <- loaded.[id]
                    config <- loaded.[id].Config
                    reload()

        // Loading into memory

        let load() =
            loaded.Clear()
            loaded.Add ("*default", _default)

            for source in Directory.EnumerateDirectories(getDataPath "Themes") do
                let id = Path.GetFileName source
                try
                    let theme = Theme.FromFolderName id
                    Logging.Debug(sprintf "  Loaded theme '%s' (%s)" theme.Config.Name id)
                    loaded.Add(id, theme)
                with err -> Logging.Error("  Failed to load theme '" + id + "'", err)

            Logging.Info(sprintf "Loaded %i themes. (Including default)" loaded.Count)

            Current.switch Current.id

        let list() = loaded |> Seq.map (fun kvp -> (kvp.Key, kvp.Value.Config.Name)) |> Array.ofSeq

        let createNew (id: string) =
             let id = Text.RegularExpressions.Regex("[^a-zA-Z0-9_-]").Replace(id, "")
             let target = Path.Combine(getDataPath "Themes", id)
             if id <> "" && not (Directory.Exists target) then _default.CopyTo(Path.Combine(getDataPath "Themes", id))
             load()
             Current.switch id
             Current.changeConfig { Current.config with Name = Current.config.Name + " (Extracted)" }

        let clearToColor (cleared: bool) = Current.config.ClearColors |> (if cleared then fst else snd)

    module Noteskins =
        
        open Prelude.Data.SkinConversions

        let loaded : Dictionary<string, Noteskin> = new Dictionary<string, Noteskin>()

        let private defaults =
            let skins = ["defaultBar.isk"; "defaultArrow.isk"; "defaultOrb.isk"]
            skins
            |> List.map Utils.getResourceStream
            |> List.map Noteskin.FromZipStream
            |> List.zip (List.map (fun s -> "*" + s) skins)

        module Current =
            
            let mutable id = fst defaults.[0]
            let mutable instance = snd defaults.[0]
            let mutable config = instance.Config
            
            let changeConfig(new_config: NoteskinConfig) =
                instance.Config <- new_config
                config <- instance.Config

            let reload() =
                for id in noteskinTextures do
                    match instance.GetTexture id with
                    | Some (img, config) -> Sprite.upload(img, config.Rows, config.Columns, false) |> Sprite.cache id |> Sprites.add id
                    | None ->
                        match loaded.["*defaultBar.isk"].GetTexture id with
                        | Some (img, config) -> Sprite.upload(img, config.Rows, config.Columns, false) |> Sprite.cache id |> Sprites.add id
                        | None -> failwith "defaultBar doesnt have this texture!!"

            let switch (new_id: string) =
                let new_id = if loaded.ContainsKey id then new_id else Logging.Warn("Noteskin '" + new_id + "' not found, switching to default"); "*defaultBar.isk"
                if new_id <> id then
                    id <- new_id
                    instance <- loaded.[id]
                    config <- instance.Config
                reload()

        // Loading into memory

        let load () =

            loaded.Clear()

            for (id, ns) in defaults do
                loaded.Add(id, ns)

            let add (source: string) (isZip: bool) =
                let id = Path.GetFileName source
                try 
                    let ns = if isZip then Noteskin.FromZipFile source else Noteskin.FromFolder source
                    loaded.Add(id, ns)
                    Logging.Debug(sprintf "  Loaded noteskin '%s' (%s)" ns.Config.Name id)
                with err -> Logging.Error("  Failed to load noteskin '" + id + "'", err)

            for source in Directory.EnumerateDirectories(getDataPath "Noteskins") do add source false
            for source in 
                Directory.EnumerateFiles(getDataPath "Noteskins")
                |> Seq.filter (fun p -> Path.GetExtension(p).ToLower() = ".isk")
                do add source true

            Logging.Info(sprintf "Loaded %i noteskins. (%i by default)" loaded.Count defaults.Length)
            
            Current.switch Current.id

        let list () = loaded |> Seq.map (fun kvp -> (kvp.Key, kvp.Value.Config.Name)) |> Array.ofSeq

        let extractCurrent() =
            let id = Guid.NewGuid().ToString()
            Current.instance.CopyTo(Path.Combine(getDataPath "Noteskins", id))
            load()
            Current.switch id
            Current.changeConfig { Current.config with Name = Current.config.Name + " (Extracted)" }

        let tryImport(path: string) : bool =
            match path with
            | OsuSkinFolder ->
                let id = Guid.NewGuid().ToString()
                try
                    //OsuSkin.Converter(path).ToNoteskin(Path.Combine(getDataPath "Noteskins", id)) 4
                    load()
                    true
                with err -> Logging.Error("Something went wrong converting this skin!", err); true
            | InterludeSkinArchive ->
                try 
                    File.Copy(path, Path.Combine(getDataPath "Noteskins", Path.GetFileName path))
                    load()
                    true
                with err -> Logging.Error("Something went wrong when moving this skin!", err); true
            | OsuSkinArchive -> Logging.Info("Can't directly drop .osks yet, sorry :( You'll have to extract it first"); true
            | Unknown -> false

    let init (themeId: string) (noteskinId: string) =
        Themes.Current.id <- themeId
        Noteskins.Current.id <- noteskinId
        Logging.Info "===== Loading game content ====="
        Noteskins.load()
        Themes.load()

    let inline getGameplayConfig<'T>() = Themes.Current.GameplayConfig.get<'T>()
    let inline getTexture (id: string) = Sprites.getTexture id
    let inline noteskinConfig() = Noteskins.Current.config
    let inline themeConfig() = Themes.Current.config