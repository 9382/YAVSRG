﻿namespace Prelude.Data.Charts.Archive

open System
open System.Collections.Generic
open Percyqaz.Json
open Prelude

type ArtistName = string
[<Json.AutoCodec>]
type VerifiedArtist =
    {
        Alternatives: string list
        IsJapaneseFullName: bool
    }
// store of artists that i've confirmed exist, with the correct STYLISATION of their name
[<Json.AutoCodec>]
type VerifiedArtists =
    { 
        Artists: Dictionary<string, VerifiedArtist>
    }
    // all-lowercase string map to correct stylised artist name, if exists
    member this.CreateMapping() =
        let d = Dictionary<string, string>()
        for a in this.Artists.Keys do
            let v = this.Artists.[a]
            for alt in v.Alternatives do
                d.Add(alt.ToLower(), a)
            if v.IsJapaneseFullName then
                let split = a.Split(' ', 2)
                d.Add((split.[1] + " " + split.[0]).ToLower(), a)
        d

type SongId = string
[<Json.AutoCodec>]
type Song =
    {
        Artists: ArtistName list // never empty
        OtherArtists: ArtistName list // alternate performers/cover artists
        Remixers: ArtistName list
        Title: string
        AlternativeTitles: string list
        Source: string option
        Tags: string list
    }
    member this.FormattedTitle =
        String.concat ", " this.Artists
        + if this.OtherArtists <> [] then " ft. " + String.concat ", " this.OtherArtists else ""
        + " - "
        + this.Title

type Songs = Dictionary<SongId, Song>

type StepmaniaPackId = int
[<Json.AutoCodec>]
type StepmaniaPack =
    {
        Title: string
        Mirrors: string list
        Size: int64
    }
type CommunityPackId = int
[<Json.AutoCodec>]
type CommunityPack =
    {
        Title: string
        Description: string option
        Mirrors: string list
        Size: int64
    }

[<Json.AutoCodec>]
type Packs =
    { 
        Stepmania: Dictionary<StepmaniaPackId, StepmaniaPack>
        Community: Dictionary<CommunityPackId, CommunityPack>
    }

[<Json.AutoCodec>]
type ChartSource =
    | Osu of {| BeatmapId: int; BeatmapSetId: int |}
    | Stepmania of StepmaniaPackId
    | CommunityPack of {| PackId: CommunityPackId |}

type ChartHash = string

[<Json.AutoCodec>]
type Chart =
    {
        SongId: SongId
        Creators: string list // never empty
        Keys: int
        DifficultyName: string
        Subtitle: string option
        Tags: string list
        Duration: Time
        Notecount: int
        BPM: (float32<ms/beat> * float32<ms/beat>)
        Sources: ChartSource list
        LastUpdated: DateTime
    }

type Charts = Dictionary<ChartHash, Chart>

module DownloadUrl =

    let into_base64 (str: string) =
        str
        |> Text.Encoding.UTF8.GetBytes
        |> Convert.ToBase64String

    let from_base64 (str: string) =
        str
        |> Convert.FromBase64String
        |> Text.Encoding.UTF8.GetString

    let create(str: string) =
        str.Replace("https://", "").Replace("http://", "") |> into_base64

    let unpickle(str: string) = "https://" + Uri.EscapeUriString(from_base64 str)