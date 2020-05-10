﻿namespace Interlude.Options

open System
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Drawing
open Prelude.Json
open Prelude.Common
open Prelude.Data.Themes

type Theme(storage) =
    inherit Prelude.Data.Themes.Theme(storage)

    member this.GetTexture(noteskin: string option, name: string) =
        let folder = 
            match noteskin with
            | None -> "Textures"
            | Some n ->
                match storage with
                | Folder _ -> Path.Combine("Noteskins", n)
                | Zip _ -> "Noteskins/" + n
        use stream = base.GetFile(folder, name + ".png")
        let bmp = new Bitmap(stream)
        let info: TextureConfig =
            try
                this.GetJson(folder, name + ".json")
            with
            | :? FileNotFoundException -> TextureConfig.Default
            //what error does zip archive give?
            | err -> Logging.Error("Could not load texture data for '" + name + "'") (err.ToString()); TextureConfig.Default
        (bmp, info)

    member this.GetConfig() = this.GetJson("theme.json")

    member this.GetNoteSkins() =
        Seq.choose
            (fun ns ->
                try
                    let config: NoteSkinConfig = this.GetJson("Noteskins", ns, "noteskin.json")
                    Some (ns, config)
                with
                | err -> Logging.Error("Failed to load noteskin '" + ns + "'") (err.ToString()); None)
            (this.GetFolders("Noteskins"))

    static member FromZipStream(stream: Stream) = Theme(Zip <| new ZipArchive(stream))
    static member FromThemeFolder(name: string) = Theme(Folder <| getDataPath(Path.Combine("Themes", name)))

module Themes =
    open Interlude.Render

    let private noteskinTextures = [|"note"; "receptor"; "mine"; "holdhead"; "holdbody"; "holdtail"|]

    let defaultTheme = Theme.FromZipStream <| Interlude.Utils.getResourceStream("default.zip")
    let mutable themeConfig = ThemeConfig.Default
    let mutable loadedThemes = []
    let mutable currentNoteSkin = "default"
    let loadedNoteskins = new Dictionary<string, NoteSkinConfig * int>()

    let availableThemes =
        let l = new List<string>()
        for t in Directory.EnumerateDirectories(getDataPath("Themes")) do
            l.Add(Path.GetFileName(t))
        l

    let private sprites = new Dictionary<string, Sprite>()
    let private sounds = "nyi"

    let loadThemes(themes: List<string>) =
        loadedThemes <- [defaultTheme]
        loadedNoteskins.Clear()
        Seq.choose (fun t ->
            let theme = Theme.FromThemeFolder(t)
            try
                let config: ThemeConfig = theme.GetJson("theme.json")
                Some (theme, config)
            with
            | err -> Logging.Error("Failed to load theme '" + t + "'") (err.ToString()); None)
            themes
        |> Seq.iteri(fun i (t, conf) ->
            //this is where we load other stuff like scripting in future
            t.GetNoteSkins()
            |> Seq.iter (fun (ns, c) ->
                loadedNoteskins.Remove(ns) |> ignore //overwrites existing skin with same name
                loadedNoteskins.Add(ns, (c, i)))
            themeConfig <- conf
            loadedThemes <- t :: loadedThemes)
        Seq.iter Sprite.destroy sprites.Values
        sprites.Clear()

    let rec private getInherited f themes =
        match themes with
        | x :: xs ->
            match f x with
            | Some v -> v
            | None -> getInherited f <| List.tail themes
        | [] -> failwith "f should give some value for default theme"

    let getTexture(name: string) =
        if not <| sprites.ContainsKey(name) then
            let (bmp, config) =
                if Array.contains name noteskinTextures then
                    let (ns, i) = loadedNoteskins.[currentNoteSkin]
                    loadedThemes.[List.length loadedThemes - 1 - i].GetTexture(Some currentNoteSkin, name)
                else
                    getInherited (fun (t: Theme) ->
                        try
                            Some <| t.GetTexture(None, name)
                        with
                        | err -> Logging.Error("Failed to load texture '" + name + "'") (err.ToString()); None)
                        loadedThemes
            sprites.Add(name, Sprite.upload(bmp, config.Rows, config.Columns, false))
        sprites.[name]