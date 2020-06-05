﻿namespace Prelude.Charts

open FParsec
open System

//https://github.com/stepmania/stepmania/wiki/sm
//https://github.com/stepmania/stepmania/wiki/ssc
(*
    Parsing tools for reading overall .sm structure
*)

module StepMania =

    type Header = (string * string list) list

    type DifficultyType =
        | Beginner = 0
        | Easy = 1
        | Medium = 2
        | Hard = 3
        | Challenge = 4
        | Edit = 5

    type ChartType =
        | Dance_Single
        | Dance_Double
        | Dance_Solo
        | Dance_Couple
        | Dance_Threepanel
        | Dance_Routine
        | Pump_Single
        | Pump_Halfdouble
        | Pump_Double
        | Pump_Couple
        | Pump_Routine
        | Kb7_Single
        | UNKNOWN

    let keyCount (mode: ChartType): int =
        match mode with
        | Dance_Threepanel -> 3
        | Dance_Single
        | UNKNOWN -> 4
        | Pump_Single -> 5
        | Dance_Solo
        | Pump_Halfdouble -> 6
        | Kb7_Single -> 7
        | Dance_Couple
        | Dance_Double
        | Dance_Routine -> 8
        | Pump_Couple
        | Pump_Double
        | Pump_Routine -> 10

    type Note =
        | Nothing = '0'
        | Normal = '1'
        | Hold_Head = '2'
        | Hold_Tail = '3'
        | Roll_Head = '4'
        | Mine = 'M'
        | Lift = 'L'
        | Fake = 'F'

    type Measure = string list

    type ChartData =
        { NOTES: Measure list
          CHARTNAME: string
          STEPSTYPE: ChartType
          DESCRIPTION: string
          CHARTSTYLE: string
          DIFFICULTY: DifficultyType
          METER: int
          CREDIT: string }

    let private parseText = spaces >>. (manyChars (noneOf ":;" <|> (previousCharSatisfies (isAnyOf "\\") >>. anyChar)))
    let parseKeyValue = (pchar '#') >>. parseText .>> pchar ':' .>>. (sepBy parseText (pchar ':')) .>> pchar ';' .>> spaces
    let private comment = optional (pstring "//" >>. restOfLine true)
    let parseHeader: Parser<Header, unit> = many (parseKeyValue .>> comment)

    (*
        Parsing for retrieving specific useful data from the file
        Some common tags have been omitted due to having no relevance to this project or poor documentation
    *)

    type StepmaniaData =
        {   TITLE: string
            SUBTITLE: string
            ARTIST: string
            TITLETRANSLIT: string
            SUBTITLETRANSLIT: string
            ARTISTTRANSLIT: string
            GENRE: string
            CREDIT: string
            BANNER: string
            BACKGROUND: string
            CDTITLE: string
            MUSIC: string
            OFFSET: float
            BPMS: (float * float) list
            STOPS: (float * float) list
            SAMPLESTART: float
            SAMPLELENGTH: float
            DISPLAYBPM: float * float
            SELECTABLE: bool
            //The following tags are SSC features
            VERSION: string
            ORIGIN: string
            WARPS: (float * float) list
            DELAYS: (float * float) list
            TIMESIGNATURES: (float * float) list
            Charts: ChartData list }
            static member Default =
                {   TITLE = "Unknown Chart"
                    SUBTITLE = ""
                    ARTIST = ""
                    TITLETRANSLIT = ""
                    SUBTITLETRANSLIT = ""
                    ARTISTTRANSLIT = ""
                    GENRE = ""
                    CREDIT = ""
                    BANNER = ""
                    BACKGROUND = ""
                    CDTITLE = ""
                    MUSIC = ""
                    OFFSET = 0.0
                    BPMS = [ (0.0, 120.0) ]
                    STOPS = []
                    SAMPLESTART = 0.0
                    SAMPLELENGTH = 1.0
                    DISPLAYBPM = (120.0, 120.0)
                    SELECTABLE = true
                    VERSION = ""
                    ORIGIN = ""
                    WARPS = []
                    DELAYS = []
                    TIMESIGNATURES = []
                    Charts = [] }

    let private parsePairs = sepBy (pfloat .>> pchar '=' .>>. pfloat) (pchar ',')
    let private parseMeasure = many ((many1Chars (anyOf "01234MLF")) .>> spaces)
    let parseMeasures = sepBy parseMeasure (pchar ',' .>> spaces)
    //https://github.com/etternagame/etterna/blob/master/src/Etterna/Singletons/GameManager.cpp
    let readChartType t =
        match t with
        | "dance-single" -> Dance_Single
        | "dance-double" -> Dance_Double
        | "dance-couple" -> Dance_Couple
        | "dance-solo" -> Dance_Solo
        | "dance-routine" -> Dance_Routine
        | "dance-threepanel" -> Dance_Threepanel
        | "pump-single" -> Pump_Single
        | "pump-double" -> Pump_Double
        | "pump-couple" -> Pump_Couple
        | "pump-halfdouble" -> Pump_Halfdouble
        | "pump-routine" -> Pump_Routine
        | "kb7-single" -> Kb7_Single
        | _ -> UNKNOWN

    let readSMData header =
        let f s (key, values) =
            match key, values with
            | "TITLE", [ t ] -> { s with TITLE = t }
            | "SUBTITLE", [ t ] -> { s with SUBTITLE = t }
            | "ARTIST", [ t ] -> { s with ARTIST = t }
            | "TITLETRANSLIT", [ t ] -> { s with TITLETRANSLIT = t }
            | "SUBTITLETRANSLIT", [ t ] -> { s with SUBTITLETRANSLIT = t }
            | "ARTISTTRANSLIT", [ t ] -> { s with ARTISTTRANSLIT = t }
            | "GENRE", [ t ] -> { s with GENRE = t }
            | "CREDIT", [ t ] -> { s with CREDIT = t }
            | "BANNER", [ t ] -> { s with BANNER = t }
            | "BACKGROUND", [ t ] -> { s with BACKGROUND = t }
            | "CDTITLE", [ t ] -> { s with CDTITLE = t }
            | "MUSIC", [ t ] -> { s with MUSIC = t }
            | "OFFSET", [ v ] -> { s with OFFSET = v |> float }
            | "BPMS", [ bs ] ->
                match run parsePairs bs with
                | Success(result, _, _) -> { s with BPMS = result }
                | Failure(errorMsg, _, _) -> failwith errorMsg
            | "STOPS", [ ss ] ->
                match run parsePairs ss with
                | Success(result, _, _) -> { s with STOPS = result }
                | Failure(errorMsg, _, _) -> failwith errorMsg
            | "SAMPLESTART", [ v ] -> { s with SAMPLESTART = v |> float }
            | "SAMPLELENGTH", [ v ] -> { s with SAMPLELENGTH = v |> float }
            | "DISPLAYBPM", [ "*" ] -> { s with DISPLAYBPM = (0.0, 999.0) }
            | "DISPLAYBPM", [ v ] -> { s with DISPLAYBPM = v |> float |> fun x -> (x, x) }
            | "DISPLAYBPM", [ v1; v2 ] -> { s with DISPLAYBPM = (v1 |> float, v2 |> float) }
            | "SELECTABLE", [ "YES" ] -> { s with SELECTABLE = true }
            | "SELECTABLE", [ "NO" ] -> { s with SELECTABLE = false }
            //Version, Origin, Warps, Delays, TimeSignatures TBI
            | "NOTES", [ chartType; author; difficultyType; footMeter; groove; noteData ] ->
                match run parseMeasures noteData with
                | Success(parsedNotes, _, _) ->
                    { s with
                          Charts =
                              { NOTES = parsedNotes
                                CHARTNAME = difficultyType + " " + footMeter
                                STEPSTYPE = chartType |> readChartType
                                DESCRIPTION = ""
                                CHARTSTYLE = ""
                                DIFFICULTY = difficultyType |> DifficultyType.Parse
                                METER = footMeter |> int
                                CREDIT = author }
                              :: s.Charts }
                | Failure(errorMsg, _, _) -> failwith errorMsg
            | _ -> s
        List.fold f StepmaniaData.Default header

    let parseStepFile = parseHeader |>> readSMData

    let loadStepmaniaFile path: StepmaniaData =
        match runParserOnFile parseStepFile () path System.Text.Encoding.UTF8 with
        | Success(result, _, _) -> result
        | Failure(errorMsg, _, _) -> failwith errorMsg