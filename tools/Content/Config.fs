﻿namespace YAVSRG.CLI.Features.Backbeat

open System.IO
open Percyqaz.Common
open Percyqaz.Data
open Percyqaz.Data.Sqlite
open Prelude
open Prelude.Data.Library
open Interlude.Web.Shared
open YAVSRG.CLI

[<AutoOpen>]
module Config =
    
    let BACKBEAT_SETTINGS_PATH = Path.Combine(Utils.YAVSRG_PATH, "backbeat", "settings.json")

    [<Json.AutoCodec>]
    type Config =
        {
            InterludePath: string
            S3ApiKey: string
            S3ApiKeyID: string
        }
        static member Default =
            {
                InterludePath = "C:/Interlude/dev"
                S3ApiKey = ""
                S3ApiKeyID = ""
            }

    let backbeat_config: Config =
        match JSON.FromFile BACKBEAT_SETTINGS_PATH with
        | Ok c -> c
        | Error e ->
            Logging.Error("Error loading settings (using default): ", e)
            Config.Default

    [<Json.AutoCodec>]
    type LoginCredentials = { Api: string; Token: string } with static member Default = { Api = "api.yavsrg.net"; Token = "" }

    do 
        try
            JSON.ToFile (BACKBEAT_SETTINGS_PATH, true) backbeat_config
            let credentials : LoginCredentials = JSON.FromFile(Path.Combine(backbeat_config.InterludePath, "Data", "login.json")) |> expect
            API.Client.init("https://" + credentials.Api)
            API.Client.authenticate(credentials.Token)
        with err -> printfn "%O" err; failwith "Error initialising backbeat utils"

    let interlude_chart_db =
        Directory.SetCurrentDirectory(backbeat_config.InterludePath)
        let db_file = Path.Combine(get_game_folder "Songs", "charts.db")
        if not (File.Exists db_file) then failwith "Couldn't find your interlude charts.db"
        let db = Database.from_file db_file
        ChartDatabase.create true db

    let INTERLUDE_SKINS_PATH = Path.Combine(backbeat_config.InterludePath, "Skins")