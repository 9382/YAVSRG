﻿namespace Interlude.Web.Shared.Requests

open Percyqaz.Data
open Interlude.Web.Shared.API

module Health =

    module Status =

        let ROUTE = (GET, "/health")

        [<Json.AutoCodec>]
        type Response = { Status: string }

        let get (callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE, callback)

module Auth =

    /// url parameters:
    ///  code - given by discord auth to identify you
    ///  state - state token previously provided by server to identify which Interlude client is logging in
    module Discord =

        let ROUTE = (GET, "/auth/discord")

module Charts =

    /// url parameters:
    ///  chart - hash of chart to get details for
    module Identify =

        let ROUTE = (GET, "/charts/identify")

        open Prelude.Backbeat.Archive

        [<Json.AutoCodec>]
        type Info =
            {
                Song: Song
                Chart: Chart
            }

        [<Json.AutoCodec>]
        type Response = { Info: Info option }

        let get (chart: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?chart=" + chart, callback)

    module Scores =

        /// requires login token as Authorization header
        module Save =

            let ROUTE = (POST, "/charts/scores")

            [<Json.AutoCodec>]
            type Request =
                {
                    ChartId: string
                    Replay: string
                    Rate: float32
                    Mods: Prelude.Gameplay.Mods.ModState
                    Timestamp: System.DateTime
                }

            [<Json.AutoCodec>]
            type LeaderboardChange =
                {
                    RulesetId: string
                    OldRank: int64 option
                    NewRank: int64
                }

            [<Json.AutoCodec>]
            type TableChange =
                {
                    Table: string
                    OldPosition: (int64 * float) option
                    NewPosition: (int64 * float)
                }

            [<Json.AutoCodec>]
            type ScoreEffect =
                {
                    LeaderboardChanges: LeaderboardChange list
                    TableChanges: TableChange list
                }

            type Response = ScoreEffect option

            let post (request: Request, callback: Response option -> unit) =
                Client.post_return<Request, Response> (snd ROUTE, request, callback)

        /// requires login token as Authorization header
        /// url parameters:
        ///  chart - hash of chart to get details for
        ///  ruleset - id of ruleset to get details for
        module Leaderboard =

            let ROUTE = (GET, "/charts/scores")

            [<Json.AutoCodec>]
            type Score =
                {
                    Username: string
                    Rank: int
                    Replay: string
                    Rate: float32
                    Mods: Prelude.Gameplay.Mods.ModState
                    Timestamp: System.DateTime
                }

            [<Json.AutoCodec>]
            type Response =
                {
                    RulesetId: string
                    Scores: Score array
                }

            let get (chart: string, ruleset: string, callback: Response option -> unit) =
                Client.get<Response> (snd ROUTE + "?chart=" + chart + "&ruleset=" + escape ruleset, callback)

module Tables =

    /// requires login token as Authorization header
    /// url parameters:
    ///  user - name of user to get records for
    ///  table - id of table to get records for e.g 'crescent' or 'mizu'
    module Records =

        let ROUTE = (GET, "/tables/records")

        [<Json.AutoCodec>]
        type Score =
            {
                Id: string
                Hash: string
                Score: float
                Grade: int
            }

        [<Json.AutoCodec>]
        type Response = { Scores: Score array }

        let get (user: string, table: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?user=" + escape user + "&table=" + escape table, callback)

    /// requires login token as Authorization header
    /// url parameters:
    ///  table - id of table to get ranking for e.g 'crescent' or 'mizu'
    module Leaderboard =

        let ROUTE = (GET, "/tables/ranking")

        [<Json.AutoCodec>]
        type Player =
            {
                Username: string
                Color: int32
                Rank: int
                Rating: float
            }

        [<Json.AutoCodec>]
        type Response = { Players: Player array }

        let get (table: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?table=" + escape table, callback)
            
    module List =

        let ROUTE = (GET, "/tables")

        open Prelude.Data.Charts.Tables.NewTables

        [<Json.AutoCodec>]
        type Table =
            {
                Id: string
                Info: TableInfo
            }

        [<Json.AutoCodec>]
        type Response = { Tables: Table array }

        let get (callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE, callback)

    /// url parameters:
    ///  table - id of table to get charts for e.g 'crescent' or 'mizu'
    ///  section - OPTIONAL: name of section to get charts for
    module Charts =
        
        let ROUTE = (GET, "/tables/charts")
        
        open Prelude.Backbeat.Archive
        
        [<Json.AutoCodec>]
        type ChartInfo =
            {
                Hash: string
                Level: int
                Song: Song
                Chart: Chart
            }
        
        [<Json.AutoCodec>]
        type Response = { Charts: ChartInfo array }

        let get (table: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?table=" + table, callback)
        
        let get_section (table: string, section: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?table=" + table + "&section=" + section, callback)

    module Suggestions =

        /// requires login token as Authorization header
        module Vote =

            let ROUTE = (POST, "/tables/suggestions")

            [<Json.AutoCodec>]
            type Request =
                {
                    ChartId: string
                    TableId: string
                    Level: int
                }

            type Response =
                | Ok
                | OkDetailsRequired
                | Rejected

            let post (request: Request, callback: Response option -> unit) =
                Client.post_return<Request, Response> (snd ROUTE, request, callback)
                
        /// requires login token as Authorization header
        module ProvideDetails =
            
            let ROUTE = (POST, "/tables/suggestions/details")

            [<Json.AutoCodec>]
            type Request =
                {
                    ChartId: string
                    TableId: string
                    OsuBeatmapId: int
                    EtternaPackId: int
                    Artist: string
                    Title: string
                    Creator: string
                    Difficulty: string
                }
                
            let post (request: Request, callback: bool option -> unit) =
                Client.post<Request> (snd ROUTE, request, callback)

        /// requires login token as Authorization header
        /// url parameters:
        ///  table - id of table to get suggestions for e.g 'crescent' or 'mizu'
        module List =

            let ROUTE = (GET, "/tables/suggestions")

            open Prelude.Backbeat.Archive

            [<Json.AutoCodec>]
            type Suggestion =
                {
                    ChartId: string
                    Votes: Map<int, string array>
                    BackbeatInfo: (Song * Chart) option
                }

            [<Json.AutoCodec>]
            type Response = { Suggestions: Suggestion array }

            let get (table: string, callback: Response option -> unit) =
                Client.get<Response> (snd ROUTE + "?table=" + escape table, callback)

        /// url parameters:
        ///  table - id of table to get suggestions for e.g 'crescent' or 'mizu'
        module Missing =

            let ROUTE = (GET, "/tables/suggestions/missing")

            [<Json.AutoCodec>]
            type Suggestion =
                {
                    ChartId: string
                    OsuBeatmapId: int
                    EtternaPackId: int
                    Artist: string
                    Title: string
                    Creator: string
                    Difficulty: string
                }

            [<Json.AutoCodec>]
            type Response = { Suggestions: Suggestion array }

            let get (table: string, callback: Response option -> unit) =
                Client.get<Response> (snd ROUTE + "?table=" + escape table, callback)

        /// requires login token as Authorization header
        /// requires 'table-editor' badge for permission
        module Accept =

            let ROUTE = (POST, "/tables/suggestions/accept")

            [<Json.AutoCodec>]
            type Request = 
                { 
                    TableId: string
                    ChartId: string
                    Level: int
                }

            let post (request: Request, callback: bool option -> unit) =
                Client.post<Request> (snd ROUTE, request, callback)
        
        /// requires login token as Authorization header
        /// requires 'table-editor' badge for permission
        module Reject =
        
            let ROUTE = (POST, "/tables/suggestions/reject")
        
            [<Json.AutoCodec>]
            type Request = 
                { 
                    TableId: string
                    ChartId: string
                    Reason: string
                }
        
            let post (request: Request, callback: bool option -> unit) =
                Client.post<Request> (snd ROUTE, request, callback)

module Players =

    /// requires login token as Authorization header
    module Online =

        let ROUTE = (GET, "/players/online")

        [<Json.AutoCodec>]
        type Player = { Username: string; Color: int }

        [<Json.AutoCodec>]
        type Response = { Players: Player array }

        let get (callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE, callback)

    /// requires login token as Authorization header
    /// url parameters:
    ///  query - partial name of user to find matches for
    module Search =

        let ROUTE = (GET, "/players/search")

        [<Json.AutoCodec>]
        type Player = { Username: string; Color: int }

        [<Json.AutoCodec>]
        type Response = { Matches: Player array }

        let get (query: string, callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE + "?query=" + escape query, callback)

    module Profile =

        /// requires login token as Authorization header
        /// url parameters:
        ///  user - OPTIONAL: name of user to view profile of (not case sensitive)
        module View =
            let ROUTE = (GET, "/players/profile")

            [<Json.AutoCodec>]
            type Badge = { Name: string; Colors: int32 list }

            [<Json.AutoCodec>]
            type RecentScore =
                {
                    Artist: string
                    Title: string
                    Difficulty: string
                    Score: float
                    Lamp: string
                    Mods: string
                    Timestamp: int64
                }

            [<Json.AutoCodec>]
            type Response =
                {
                    Username: string
                    Color: int
                    Badges: Badge array
                    RecentScores: RecentScore array
                    DateSignedUp: int64
                    IsFriend: bool
                    IsMutualFriend: bool
                }

            let get_me (callback: Response option -> unit) =
                Client.get<Response> (snd ROUTE, callback)

            let get (user: string, callback: Response option -> unit) =
                Client.get<Response> (snd ROUTE + "?user=" + escape user, callback)

        /// requires login token as Authorization header
        module Options =

            let ROUTE = (POST, "/players/profile/options")

            [<Json.AutoCodec>]
            type Request = { Color: int }

            let post (request: Request, callback: bool option -> unit) =
                Client.post<Request> (snd ROUTE, request, callback)

module Friends =

    /// requires login token as Authorization header
    module List =

        let ROUTE = (GET, "/friends")

        [<Json.AutoCodec>]
        type Friend =
            {
                Username: string
                Color: int
                Online: bool
            }

        [<Json.AutoCodec>]
        type Response = { Friends: Friend array }

        let get (callback: Response option -> unit) =
            Client.get<Response> (snd ROUTE, callback)

    /// requires login token as Authorization header
    module Add =

        let ROUTE = (POST, "/friends")

        [<Json.AutoCodec>]
        type Request = { User: string }

        let post (request: Request, callback: bool option -> unit) =
            Client.post<Request> (snd ROUTE, request, callback)

    /// requires login token as Authorization header
    /// url parameters:
    ///  user - name of user to add as friend (not case sensitive)
    module Remove =

        let ROUTE = (DELETE, "/friends")

        let delete (user: string, callback: bool option -> unit) =
            Client.delete (snd ROUTE + "?user=" + escape user, callback)
