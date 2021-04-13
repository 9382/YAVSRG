﻿namespace Prelude.Data

open System
open System.IO
open System.IO.Compression
open System.Collections.Generic
open Percyqaz.Json
open Prelude.Common
open Prelude.Charts.Interlude
open Prelude.Charts.ChartConversions
open Prelude.Gameplay.Mods
open Prelude.Gameplay.Layout
open Prelude.Gameplay.Difficulty

module ChartManager =

    (*
        Caching of charts
    *)

    [<Json.AllRequired>]
    type CachedChart = {
        FilePath: string
        Title: string
        Artist: string
        Creator: string
        Pack: string
        Hash: string
        Keys: int
        Length: Time
        BPM: (float32<ms/beat> * float32<ms/beat>)
        DiffName: string
        Physical: float
        Technical: float
    }
    with static member Default = { FilePath = ""; Title = ""; Artist = ""; Creator = ""; Pack = ""; Hash = ""; Keys = 4; Length = 0.0f<ms>; BPM = (500.0f<ms/beat>, 500.0f<ms/beat>); DiffName = ""; Physical = 0.0; Technical = 0.0 }

    let cacheChart (chart : Chart) : CachedChart =
        let endTime =
            if chart.Notes.Count = 0 then 0.0f<ms> else
                chart.Notes.GetPointAt (infinityf * 1.0f<ms>) |> offsetOf
        let rating = RatingReport(chart.Notes, 1.0f, Layout.Spread, chart.Keys)
        {
        FilePath = chart.FileIdentifier
        Title = chart.Header.Title
        Artist = chart.Header.Artist
        Creator = chart.Header.Creator
        Pack = chart.Header.SourcePack
        Hash = calculateHash chart
        Keys = chart.Keys
        Length = if endTime = 0.0f<ms> then 0.0f<ms> else endTime - (offsetOf chart.Notes.First.Value)
        BPM = minMaxBPM (chart.BPM.Data |> List.ofSeq) endTime
        DiffName = chart.Header.DiffName
        Physical = rating.Physical
        Technical = rating.Technical }

    let osuSongFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!", "Songs")
    let smPackFolder = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "Games", "Stepmania 5", "Songs")
    let etternaPackFolder = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "Games", "Etterna", "Songs")

    (*
        Goals and playlists
    *)

    type Goal =
        | NoGoal
        | Clear of unit
        | Grade of unit * int
        | Accuracy of unit * float

    type PlaylistData = string * ModState * float
    type Collection =
        | Collection of List<string>
        | Playlist of List<PlaylistData>
        | Goals of List<PlaylistData * Goal>

    (*
        Sorting and searching
    *)

    module Sorting =
        open FParsec

        let private firstCharacter(s : string) =
            if (s.Length = 0) then "?"
            else
                if Char.IsLetterOrDigit(s.[0]) then s.[0].ToString().ToUpper() else "?"

        let groupBy = dict[
                "Physical", fun c -> let i = int (c.Physical / 2.0) * 2 in i.ToString().PadLeft(2, '0') + " - " + (i + 2).ToString().PadLeft(2, '0')
                "Technical", fun c -> let i = int (c.Technical / 2.0) * 2 in i.ToString().PadLeft(2, '0') + " - " + (i + 2).ToString().PadLeft(2, '0')
                "Pack", fun c -> c.Pack
                "Title", fun c -> firstCharacter(c.Title)
                "Artist", fun c -> firstCharacter(c.Artist)
                "Creator", fun c -> firstCharacter(c.Creator)
                "Keymode", fun c -> c.Keys.ToString() + "k"
            ]

        let sortBy = dict[
                "Physical", Comparison(fun a b -> a.Physical.CompareTo(b.Physical))
                "Technical", Comparison(fun a b -> a.Technical.CompareTo(b.Technical))
                "Title", Comparison(fun a b -> a.Title.CompareTo(b.Title))
                "Artist", Comparison(fun a b -> a.Artist.CompareTo(b.Artist))
                "Creator", Comparison(fun a b -> a.Creator.CompareTo(b.Creator))
            ]

        type FilterPart = 
        | Criterion of string * string
        | String of string
        | Impossible

        let string = " =:<>\"" |> isNoneOf |> many1Satisfy |>> fun s -> s.ToLower()
        let word = string |>> String
        let pstring = between (pchar '"') (pchar '"') ("\"" |> isNoneOf |> many1Satisfy) |>> fun s -> String <| s.ToLower()
        let criterion = string .>>. (pchar '=' >>. string) |>> Criterion
        let filter = sepBy (attempt criterion <|> pstring <|> word) spaces1

        let parseFilter str =
            match run filter str with
            | Success (x, _, _) -> x
            | Failure (f, _, _) -> [Impossible]

        type Filter = FilterPart list

    open Sorting

    type ChartGroup = (string * CachedChart list)

    //todo: add reverse lookup from hash -> id and remove id storage in score data
    type Cache() =
        let charts, collections = Cache.Load()

        member this.Save() = Json.toFile(Path.Combine(getDataPath("Data"), "cache.json"), true)(charts, collections)
        static member Load() = loadImportantJsonFile("Cache")(Path.Combine(getDataPath("Data"), "cache.json"))((new Dictionary<string, CachedChart>(), new Dictionary<string, Collection>()))(false)
    
        member this.CacheChart (c: Chart) = lock(this) (fun () -> charts.[c.FileIdentifier] <- cacheChart c)

        member this.Count = charts.Count

        member this.LookupChart (id: string) : CachedChart option = if charts.ContainsKey(id) then Some charts.[id] else None

        member this.LoadChart (cc: CachedChart) : Chart option = 
            match cc.FilePath |> loadChartFile with
            | Some c ->
                this.CacheChart c
                Some c
            | None -> None

        member private this.FilterCharts(filter: Filter)(charts: CachedChart seq) =
            seq {
                for c in charts do
                    let s = (c.Title + " " + c.Artist + " " + c.Creator + " " + c.DiffName + " " + c.Pack).ToLower()
                    if List.forall
                        (
                            function
                            | Impossible -> false
                            | String str -> s.Contains(str)
                            | Criterion ("k", n)
                            | Criterion ("key", n)
                            | Criterion ("keys", n) -> c.Keys.ToString() = n
                            | _ -> true
                        )
                        filter
                    then yield c
            }

        member this.GetGroups grouping (sorting: Comparison<CachedChart>) filter =
            let groups = new Dictionary<string, List<CachedChart>>()
            lock this //thread safe
                (fun _ ->
                    for c in this.FilterCharts filter charts.Values do
                        let s = grouping(c)
                        if (groups.ContainsKey(s) |> not) then groups.Add(s, new List<CachedChart>())
                        groups.[s].Add(c)
                    for g in groups.Values do
                        g.Sort(sorting)
                    groups)

        member this.GetCollections (sorting: Comparison<CachedChart>) filter =
            let groups = new Dictionary<string, List<CachedChart>>()
            for name in collections.Keys do
                let c = collections.[name]
                match c with
                | Collection ids -> Seq.choose this.LookupChart ids
                | Playlist ps -> Seq.choose (fun (i, _, _) -> this.LookupChart i) ps
                | Goals gs -> Seq.choose (fun ((i, _, _), _) -> this.LookupChart i) gs
                |> this.FilterCharts filter
                |> ResizeArray<CachedChart>
                |> fun x -> groups.Add(name, x)
            for g in groups.Values do
                g.Sort(sorting)
            groups

        member this.RebuildCache : StatusTask =
            fun output ->
                async {
                    lock this (fun _ -> charts.Clear())
                    for pack in Directory.EnumerateDirectories(getDataPath "Songs") do
                        for song in Directory.EnumerateDirectories(pack) do
                            for file in Directory.EnumerateFiles(song) do
                                match Path.GetExtension(file).ToLower() with
                                | ".yav" ->
                                    lock this (fun _ ->
                                    (
                                        match loadChartFile(file) with
                                        | Some c ->
                                            output("Caching " + file)
                                            this.CacheChart(c)
                                        | None -> ()
                                    ))
                                | _ -> ()
                    this.Save()
                    output("Saved cache.")
                    return true
                }

        member this.DeleteChart (c: CachedChart) =
            lock this (fun _ -> charts.Remove c.FilePath |> ignore)
            if File.Exists c.FilePath then File.Delete c.FilePath
            let songfolder = Path.GetDirectoryName c.FilePath
            match songfolder with SongFolder _ -> () | _ -> Directory.Delete(songfolder, true) 
            let packfolder = Path.GetDirectoryName songfolder
            if packfolder |> Directory.EnumerateDirectories |> Seq.isEmpty then Directory.Delete(packfolder, false)

        member this.DeleteCharts (cs: CachedChart seq) = Seq.iter this.DeleteChart cs

        member this.ConvertSongFolder (path: string) (packname: string) : StatusTask =
            fun output ->
                async {
                    for file in Directory.EnumerateFiles(path) do
                        match file with
                        | ChartFile _ ->
                            output("Converting " + file)
                            try
                                loadAndConvertFile file
                                |> List.map (fun c -> relocateChart c path (Path.Combine(getDataPath "Songs", packname, Path.GetFileName(path))))
                                |> fun charts ->
                                    lock this (fun _ ->
                                    (
                                        List.iter this.CacheChart charts
                                    ))
                            with err -> Logging.Error("Failed to load/convert file: " + file, err)
                        | _ -> ()
                    return true
                }

        member this.ConvertPackFolder (path: string) (packname: string) : StatusTask =
            fun output ->
                async {
                    let! _ =
                        Directory.EnumerateDirectories(path)
                        |> Seq.map (fun song -> (this.ConvertSongFolder song packname output))
                        |> fun s -> Async.Parallel(s, 5)
                    return true
                }

        member this.AutoConvert path : StatusTask =
            fun output ->
                async {
                    match File.GetAttributes(path) &&& FileAttributes.Directory |> int with
                    | 0 ->
                        match path with
                        | ChartFile _ -> failwith "single chart file not yet supported"
                        | ChartArchive ->
                            output("Extracting...")
                            let dir = Path.ChangeExtension(path, null)
                            // todo: is this safe from directory traversal?
                            ZipFile.ExtractToDirectory(path, dir)
                            output("Extracted! " + dir)
                            do! (this.AutoConvert dir |> BackgroundTask.Callback(fun _ -> Directory.Delete(dir, true))) output |> Async.Ignore
                        | _ -> failwith "unrecognised file"
                    | _ ->
                        match path with
                        | SongFolder ext ->
                            do! (this.ConvertSongFolder path (if ext = ".osu" then "osu!" else "Singles")) output |> Async.Ignore
                        | PackFolder ->
                            let packname =
                                match Path.GetFileName(path) with
                                | "Songs" -> if path |> Path.GetDirectoryName |> Path.GetFileName = "osu!" then "osu!" else "Songs"
                                | s -> s
                            do! (this.ConvertPackFolder path packname) output |> Async.Ignore
                        | FolderOfPacks ->
                            do! 
                                Directory.EnumerateDirectories(path)
                                |> Seq.map (fun packFolder -> this.ConvertPackFolder packFolder (Path.GetFileName packFolder) output)
                                |> fun s -> Async.Parallel(s, 5)
                                |> Async.Ignore
                        | _ -> failwith "no importable folder structure detected"
                    return true
                }