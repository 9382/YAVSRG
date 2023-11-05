﻿namespace Interlude.Web.Server.API.Charts.Scores

open NetCoreServer
open Interlude.Web.Shared.Requests
open Interlude.Web.Server.API
open Interlude.Web.Server.Domain

module Leaderboard =

    let handle
        (
            body: string,
            query_params: Map<string, string array>,
            headers: Map<string, string>,
            response: HttpResponse
        ) =
        async {
            if not (query_params.ContainsKey "chart") then
                response.MakeErrorResponse(400, "'chart' is required") |> ignore
            else if not (query_params.ContainsKey "ruleset") then
                response.MakeErrorResponse(400, "'ruleset' is required") |> ignore
            else

            let _, _ = authorize headers

            let hash = query_params.["chart"].[0].ToUpper()
            let ruleset = query_params.["ruleset"].[0]

            let ruleset =
                if
                    ruleset <> Score.RULESETS.[0]
                    && not (Leaderboard.exists hash ruleset)
                then
                    Score.RULESETS.[0]
                else
                    ruleset

            if Leaderboard.exists hash ruleset then

                let info = Leaderboard.get_top_20_info hash ruleset

                let scores: Charts.Scores.Leaderboard.Score array =
                    info
                    |> Array.indexed
                    |> Array.choose (
                        function
                        | i, (Some username, Some found_score) ->
                            Some
                                {
                                    Username = username
                                    Rank = i + 1
                                    Replay = found_score.Replay
                                    Rate = found_score.Rate
                                    Mods = found_score.Mods
                                    Timestamp =
                                        System.DateTimeOffset
                                            .FromUnixTimeMilliseconds(found_score.Timestamp)
                                            .UtcDateTime
                                }
                        | _ -> None
                    )

                response.ReplyJson({ Scores = scores; RulesetId = ruleset }: Charts.Scores.Leaderboard.Response)

            else
                response.MakeErrorResponse(404) |> ignore
        }
