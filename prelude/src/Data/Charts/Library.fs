﻿namespace Prelude.Data.Charts

open System
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Collections.Concurrent
open Percyqaz.Json
open Percyqaz.Common
open Prelude.Common
open Prelude.Charts.Formats.Interlude
open Prelude.Charts.Formats.Conversions
open Prelude.Data.Charts.Tables
open Prelude.Data.Charts.Caching

open Sorting
open Collections

module Library =

    let cache : Cache = Cache.from_path (getDataPath "Songs")
    let collections = 
        let cs : Collections = loadImportantJsonFile "Collections" (Path.Combine(getDataPath "Data", "collections.json")) false
        Logging.Info (sprintf "Loaded chart library of %i charts, %i folders, %i playlists" cache.Entries.Count cs.Folders.Count cs.Playlists.Count)
        cs

    // ---- Basic data layer stuff ----

    let save() = 
        Cache.save cache
        saveImportantJsonFile (Path.Combine(getDataPath "Data", "collections.json")) collections

    // ---- Retrieving library for level select ----

    type [<RequireQualifiedAccess; Json.AutoCodec>] LibraryMode = All | Collections | Table
    type Group = { Charts: ResizeArray<CachedChart * LibraryContext>; Context: LibraryGroupContext }
    type LexSortedGroups = Dictionary<int * string, Group>

    let getGroups (ctx: GroupContext) (grouping: GroupMethod) (sorting: SortMethod) (filter: Filter) : LexSortedGroups =

        let groups = new Dictionary<int * string, Group>()

        for c in Filter.apply filter cache.Entries.Values do
            let s = grouping (c, ctx)
            if groups.ContainsKey s |> not then groups.Add(s, { Charts = ResizeArray<CachedChart * LibraryContext>(); Context = LibraryGroupContext.None })
            groups.[s].Charts.Add (c, LibraryContext.None)

        for g in groups.Values do
            g.Charts.Sort sorting

        groups

    let getCollectionGroups (sorting: SortMethod) (filter: Filter) : LexSortedGroups =

        let groups = new Dictionary<int * string, Group>()

        for name in collections.Folders.Keys do
            let collection = collections.Folders.[name]
            collection.Charts
            |> Seq.choose
                ( fun entry ->
                    match Cache.by_key entry.Path cache with
                    | Some cc -> Some (cc, LibraryContext.Folder name)
                    | None ->
                    match Cache.by_hash entry.Hash cache with
                    | Some cc -> entry.Path <- cc.Key; Some (cc, LibraryContext.Folder name)
                    | None -> Logging.Warn(sprintf "Could not find chart: %s [%s] for collection %s" entry.Path entry.Hash name); None
                )
            |> Filter.applyf filter
            |> ResizeArray<CachedChart * LibraryContext>
            |> fun x -> x.Sort sorting; x
            |> fun x -> if x.Count > 0 then groups.Add((0, name), { Charts = x; Context = LibraryGroupContext.Folder name })

        for name in collections.Playlists.Keys do
            let playlist = collections.Playlists.[name]
            playlist.Charts
            |> Seq.indexed
            |> Seq.choose
                ( fun (i, (entry, info)) ->
                    match Cache.by_key entry.Path cache with
                    | Some cc -> Some (cc, LibraryContext.Playlist (i, name, info))
                    | None ->
                    match Cache.by_key entry.Hash cache with
                    | Some cc -> entry.Path <- cc.Key; Some (cc, LibraryContext.Playlist (i, name, info))
                    | None -> Logging.Warn(sprintf "Could not find chart: %s [%s] for playlist %s" entry.Path entry.Hash name); None
                )
            |> Filter.applyf filter
            |> ResizeArray<CachedChart * LibraryContext>
            |> fun x -> if x.Count > 0 then groups.Add((0, name), { Charts = x; Context = LibraryGroupContext.Playlist name })

        groups

    let getTableGroups (sorting: SortMethod) (filter: Filter) : LexSortedGroups =
        let groups = new Dictionary<int * string, Group>()
        match Table.current() with
        | Some table ->
            for level_no, level in Seq.indexed table.Levels do
                level.Charts
                |> Seq.choose
                    ( fun (c: TableChart) ->
                        Cache.by_hash c.Hash cache
                        |> Option.map (fun x -> x, LibraryContext.Table level.Name)
                    )
                |> Filter.applyf filter
                |> ResizeArray<CachedChart * LibraryContext>
                |> fun x -> x.Sort sorting; x
                |> fun x -> if x.Count > 0 then groups.Add((level_no, level.Name), { Charts = x; Context = LibraryGroupContext.Table level.Name })
        | None -> ()
        groups

    // ---- Importing charts to library ----

    module Imports =
    
        let osuSongFolder = Path.Combine (Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData, "osu!", "Songs")
        let stepmaniaPackFolder = Path.Combine (Path.GetPathRoot Environment.CurrentDirectory, "Games", "Stepmania 5", "Songs")
        let etternaPackFolder = Path.Combine (Path.GetPathRoot Environment.CurrentDirectory, "Games", "Etterna", "Songs")
        
        [<Json.AutoCodec>]
        type MountedChartSourceType =
            | Pack of name: string
            | Library
        
        [<Json.AutoCodec>]
        type MountedChartSource =
            {
                SourceFolder: string
                mutable LastImported: DateTime
                Type: MountedChartSourceType
                ImportOnStartup: bool
            }
            static member Pack (name: string, path: string) =
                { SourceFolder = path; LastImported = DateTime.UnixEpoch; Type = Pack name; ImportOnStartup = false }
            static member Library (path: string) =
                { SourceFolder = path; LastImported = DateTime.UnixEpoch; Type = Library; ImportOnStartup = false }

        let convert_song_folder = 
            { new Async.Service<string * ConversionActionConfig, unit>() with
                override this.Handle((path, config)) =
                    async {
                        for file in Directory.EnumerateFiles path do
                            match file with
                            | ChartFile _ ->
                                try
                                    let action = { Config = config; Source = file }
                                    loadAndConvertFile action
                                    |> fun charts -> Cache.add_new config.PackName charts cache
                                with err -> Logging.Error ("Failed to convert/cache file: " + file, err)
                            | _ -> ()
                    }
            }

        let convert_pack_folder =
            { new Async.Service<string * ConversionActionConfig, unit>() with
                override this.Handle((path, config)) =
                    async {
                        for songFolder in
                            Directory.EnumerateDirectories path
                            |> match config.ChangedAfter with None -> id | Some timestamp -> Seq.filter (fun path -> Directory.GetLastWriteTime path >= timestamp)
                            do
                            do! convert_song_folder.RequestAsync(songFolder, config)
                    }
            }

        let import_mounted_source =
            
            { new Async.Service<MountedChartSource, unit>() with
                override this.Handle(source) =
                    async {
                        match source.Type with
                        | Pack packname ->
                            let config = { ConversionActionConfig.Default with CopyMediaFiles = false; ChangedAfter = Some source.LastImported; PackName = packname }
                            do! convert_pack_folder.RequestAsync(source.SourceFolder, config)
                            source.LastImported <- DateTime.Now
                        | Library ->
                            for packFolder in
                                Directory.EnumerateDirectories source.SourceFolder
                                |> Seq.filter (fun path -> Directory.GetLastWriteTime path >= source.LastImported)
                                do
                                do! convert_pack_folder.RequestAsync(packFolder, { ConversionActionConfig.Default with CopyMediaFiles = false; ChangedAfter = Some source.LastImported; PackName = Path.GetFileName packFolder })
                            source.LastImported <- DateTime.Now
                    }
            }

        let auto_convert =
            { new Async.Service<string, bool>() with
                override this.Handle(path) =
                    async {
                        match File.GetAttributes path &&& FileAttributes.Directory |> int with
                        | 0 ->
                            match path with
                            | ChartFile ext ->
                                do! convert_song_folder.RequestAsync(
                                    Path.GetDirectoryName path,
                                    { ConversionActionConfig.Default with PackName = if ext = ".osu" then "osu!" else "Singles" })
                                return true
                            | ChartArchive ->
                                let dir = Path.ChangeExtension(path, null)
                                // This has built-in directory traversal protection
                                ZipFile.ExtractToDirectory(path, dir)
                                this.Request(dir, fun _ -> Directory.Delete(dir, true))
                                return true
                            | _ -> Logging.Warn(sprintf "%s: Unrecognised file for import" path); return false
                        | _ ->
                            match path with
                            | SongFolder ext ->
                                do! convert_song_folder.RequestAsync(path, { ConversionActionConfig.Default with PackName = if ext = ".osu" then "osu!" else "Singles" })
                                return true
                            | PackFolder ->
                                let packname =
                                    match Path.GetFileName path with
                                    | "Songs" -> if path |> Path.GetDirectoryName |> Path.GetFileName = "osu!" then "osu!" else "Songs"
                                    | s -> s
                                do! convert_pack_folder.RequestAsync(path, { ConversionActionConfig.Default with PackName = packname })
                                return true
                            | FolderOfPacks ->
                                for packFolder in Directory.EnumerateDirectories path do
                                    do! convert_pack_folder.RequestAsync(packFolder, { ConversionActionConfig.Default with PackName = Path.GetFileName packFolder })
                                return true
                            | _ -> Logging.Warn(sprintf "%s: No importable folder structure detected" path); return false
                    }
            }