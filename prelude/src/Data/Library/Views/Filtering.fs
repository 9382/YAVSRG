﻿namespace Prelude.Data.Library

open System
open FParsec
open Prelude
open Prelude.Charts.Processing.Patterns

type FilterPart =
    | Equals of string * string
    | NotEquals of string * string
    | LessThan of string * float32
    | MoreThan of string * float32
    | Clamp of string * float32
    | String of string
    | NotString of string
    | Tag of string
    | Impossible

module FilterParts =

    let private string = " !=<>#~\"" |> isNoneOf |> many1Satisfy |>> fun s -> s.ToLowerInvariant()

    let private quoted_string =
        between (pchar '"') (pchar '"') ("\"" |> isNoneOf |> many1Satisfy)

    let private word = string |>> String
    let private quoted = quoted_string |>> fun s -> String <| s.ToLowerInvariant()
    let private antiquoted = pchar '!' >>. quoted_string |>> fun s -> NotString <| s.ToLowerInvariant()

    let private equals =
        string .>>. (pchar '=' >>. (string <|> quoted_string)) |>> (fun (k, v) -> Equals(k.ToLowerInvariant(), v.ToLowerInvariant()))

    let private notequals =
        string .>>. (pchar '!' >>. pchar '=' >>. (string <|> quoted_string)) |>> NotEquals

    let private pfloat32 = pfloat |>> float32

    let private less = string .>>. (pchar '<' >>. optional (pchar '=') >>. pfloat32) |>> LessThan
    let private more = string .>>. (pchar '>' >>. optional (pchar '=') >>. pfloat32) |>> MoreThan
    let private clamp = string .>>. (pchar '~' >>. pfloat32) |>> Clamp
    let private tag = pchar '#' >>. string |>> Tag

    let private filter =
        sepBy (
            attempt equals 
            <|> attempt notequals
            <|> attempt clamp
            <|> attempt less
            <|> attempt more
            <|> attempt tag
            <|> attempt antiquoted
            <|> quoted
            <|> word
        ) spaces1
        .>> spaces

    let parse (str: string) =
        match run filter (str.Trim()) with
        | Success(x, _, _) -> x
        | Failure(f, _, _) -> [ Impossible ]

type Filter =
    internal {
        PatternTerms: string array
        PatternAntiTerms: string array

        BPMClamp: (CorePattern * float) option

        Keymode: int option
        LengthMin: float32 option
        LengthMax: float32 option
        DifficultyMin: float32 option
        DifficultyMax: float32 option
        LNPercentMin: float32 option
        LNPercentMax: float32 option
        SV: bool option
    }

    static member Empty =
        {
            PatternTerms = [||]
            PatternAntiTerms = [||]

            BPMClamp = None

            Keymode = None
            LengthMin = None
            LengthMax = None
            DifficultyMin = None
            DifficultyMax = None
            LNPercentMin = None
            LNPercentMax = None
            SV = None
        }

    member internal this.Compile : ChartMeta -> bool =
        seq {
            match this.Keymode with
            | Some k -> yield fun cc -> cc.Keys = k
            | None -> ()

            match this.LengthMin with
            | Some min_length -> yield fun cc -> cc.Length / 1000.0f<ms> >= min_length
            | None -> ()
            match this.LengthMax with
            | Some max_length -> yield fun cc -> cc.Length / 1000.0f<ms> <= max_length
            | None -> ()

            match this.DifficultyMin with
            | Some min_diff -> yield fun cc -> cc.Rating >= min_diff
            | None -> ()
            match this.DifficultyMax with
            | Some max_diff -> yield fun cc -> cc.Rating <= max_diff
            | None -> ()

            match this.LNPercentMin with
            | Some min_pc -> yield fun cc -> cc.Patterns.LNPercent > min_pc
            | None -> ()
            match this.LNPercentMax with
            | Some max_pc -> yield fun cc -> cc.Patterns.LNPercent < max_pc
            | None -> ()

            match this.SV with
            | Some false -> yield fun cc -> not (cc.Patterns.SVAmount > Categorise.SV_AMOUNT_THRESHOLD)
            | Some true -> yield fun cc -> cc.Patterns.SVAmount > Categorise.SV_AMOUNT_THRESHOLD
            | None -> ()

            if this.PatternTerms.Length <> 0 || this.PatternAntiTerms.Length <> 0 then
                yield fun cc ->
                    let report = cc.Patterns

                    let matches (pattern: string) =
                        report.Category.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                        || (
                            report.Clusters
                            |> Array.exists (fun f -> 
                                f.SpecificTypes
                                |> List.exists (fun (p, amount) -> 
                                    amount * f.Amount / cc.Length > 0.1f && p.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                                )
                            )
                        )

                    Array.forall matches this.PatternTerms
                    && Array.forall (matches >> not) this.PatternAntiTerms
        }
        |> Array.ofSeq
        |> fun checks -> (fun (cc: ChartMeta) -> Array.forall (fun f -> f cc) checks)

    member this.Apply (charts: ChartMeta seq) =
        Seq.filter this.Compile charts

    member this.Apply<'T> (charts: (ChartMeta * 'T) seq) =
        Seq.filter (fst >> this.Compile) charts

    static member Build (parts: FilterPart list) : Filter =
        let mutable filter = Filter.Empty

        for p in parts do
            match p with
            | Equals("p", str)
            | Equals("pattern", str) -> filter <- { filter with PatternTerms = Array.append filter.PatternTerms [| str |] }
            | NotEquals("p", str)
            | NotEquals("pattern", str) -> filter <- { filter with PatternAntiTerms = Array.append filter.PatternAntiTerms [| str |] }

            | Equals("k", n)
            | Equals("key", n)
            | Equals("keys", n) -> 
                match Int32.TryParse(n) with
                | true, k -> filter <- { filter with Keymode = Some k }
                | false, _ -> ()

            | MoreThan("d", d)
            | MoreThan("diff", d) -> filter <- { filter with DifficultyMin = Some d }
            | LessThan("d", d)
            | LessThan("diff", d) -> filter <- { filter with DifficultyMax = Some d }

            | MoreThan("l", l)
            | MoreThan("length", l) -> filter <- { filter with LengthMin = Some l }
            | LessThan("l", l)
            | LessThan("length", l) -> filter <- { filter with LengthMax = Some l }

            | LessThan("ln", pc)
            | LessThan("holds", pc)
            | LessThan("lns", pc) -> filter <- { filter with LNPercentMin = Some pc }
            | MoreThan("ln", pc)
            | MoreThan("holds", pc)
            | MoreThan("lns", pc) -> filter <- { filter with LNPercentMax = Some pc }

            | Tag "nosv"
            | Tag "nsv" -> filter <- { filter with SV = Some false }
            | Tag "sv" -> filter <- { filter with SV = Some true }

            | _ -> ()
        filter

type FilteredSearch =
    {
        SearchTerms: string array
        SearchAntiTerms: string array

        Filter: Filter
    }

    member internal this.Compile : ChartMeta -> bool =
        let matches_filter = this.Filter.Compile

        if this.SearchTerms.Length <> 0 || this.SearchAntiTerms.Length <> 0 then

            fun cc ->
                if not (matches_filter cc) then false else

                let s =
                    (cc.Title
                        + " "
                        + cc.Artist
                        + " "
                        + cc.Creator
                        + " "
                        + cc.DifficultyName
                        + " "
                        + (cc.Subtitle |> Option.defaultValue "")
                        + " "
                        + String.concat " " cc.Packs)
                        .ToLowerInvariant()
                Array.forall (s.Contains : string -> bool) this.SearchTerms
                && Array.forall (s.Contains >> not : string -> bool) this.SearchAntiTerms

        else matches_filter

    member this.Apply (charts: ChartMeta seq) =
        Seq.filter this.Compile charts

    member this.Apply<'T> (charts: (ChartMeta * 'T) seq) =
        Seq.filter (fst >> this.Compile) charts

    static member Build (parts: FilterPart list) : FilteredSearch =
        let mutable filtered_search =
            {
                SearchTerms = [||]
                SearchAntiTerms = [||]
                Filter = Filter.Build parts
            }

        for p in parts do
            match p with
            | Impossible -> filtered_search <- { filtered_search with SearchAntiTerms = [|""|] }
            | String str -> filtered_search <- { filtered_search with SearchTerms = Array.append filtered_search.SearchTerms [| str |] }
            | NotString str -> filtered_search <- { filtered_search with SearchAntiTerms = Array.append filtered_search.SearchAntiTerms [| str |] }

            | _ -> ()

        filtered_search