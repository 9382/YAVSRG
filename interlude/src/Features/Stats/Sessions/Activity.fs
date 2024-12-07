﻿namespace Interlude.Features.Stats

open System
open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Prelude
open Prelude.Data.User
open Interlude.UI

type YearActivityGrid(year: int, selected: Setting<(DateOnly * Session) option>) =
    inherit StaticWidget(NodeType.Leaf)

    let session_dates =
        Stats.PREVIOUS_SESSIONS
        |> Map.filter (fun day _ -> day.Year = year)
        |> Map.map (fun _ sessions ->
            let day_playtime_hours = (sessions |> List.sumBy (_.PlayTime)) / 3600_000.0
            let color =
                if day_playtime_hours < 0.25 then Colors.cyan.O2
                elif day_playtime_hours < 0.5 then Colors.cyan.O3
                elif day_playtime_hours < 0.75 then Colors.cyan
                else Colors.cyan_accent
            sessions, color
        )

    let mutable hovered_day = None
    let PADDING = 20.0f

    override this.Draw() =
        Render.rect this.Bounds Colors.shadow_2.O2

        let box_size = (this.Bounds.Height - PADDING * 2.0f) / 7.0f
        let weeks_shown = 53
        let x_padding = (this.Bounds.Width - (box_size * float32 weeks_shown)) * 0.5f

        let mutable day = DateOnly(year, 1, 1)
        let mutable i = int day.DayOfWeek
        while day.Year = year do

            let pos = Rect.Box(this.Bounds.Left + x_padding + float32 (i / 7) * box_size, this.Bounds.Top + PADDING + float32 (i % 7) * box_size, box_size, box_size)

            let color =
                match session_dates.TryFind day with
                | Some (_, color) -> color
                | None -> Colors.black

            if Some day = Option.map fst selected.Value then
                Render.rect pos Colors.white.O2
            elif Some day = hovered_day then
                Render.rect pos Colors.yellow_accent.O2

            Render.rect (pos.ShrinkPercent(0.2f)) color

            i <- i + 1
            day <- day.AddDays 1

        match hovered_day with
        | Some d ->
            Text.draw_aligned_b(Style.font, d.ToString("dddd, dd MMMM yyyy"), 20.0f, this.Bounds.Right - 10.0f, this.Bounds.Top - 35.0f, Colors.text_subheading, Alignment.RIGHT)
        | None -> ()
        Text.draw_b(Style.font, year.ToString(), 30.0f, this.Bounds.Left + 15.0f, this.Bounds.Top - 50.0f, Colors.text)

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)
        hovered_day <- None

        let box_size = (this.Bounds.Height - PADDING * 2.0f) / 7.0f
        let weeks_shown = 53
        let x_padding = (this.Bounds.Width - (box_size * float32 weeks_shown)) * 0.5f

        let mutable day = DateOnly(year, 1, 1)
        let mutable i = int day.DayOfWeek
        while day.Year = year do

            let pos = Rect.Box(this.Bounds.Left + x_padding + float32 (i / 7) * box_size, this.Bounds.Top + PADDING + float32 (i % 7) * box_size, box_size, box_size)

            if Mouse.hover pos then
                hovered_day <- Some day

                if Mouse.left_click() then
                    match session_dates.TryFind day with
                    | Some (sessions, _) ->
                        selected.Value <- Some (day, sessions.[0])
                    | _ -> ()

                day <- day.AddYears 1

            i <- i + 1
            day <- day.AddDays 1

type AllYearsActivityPage(selected: Setting<(DateOnly * Session) option>) =
    inherit Page()

    override this.Content() =
        let flow = FlowContainer.Vertical<YearActivityGrid>(200.0f, Spacing = 65.0f)

        for year in Stats.PREVIOUS_SESSIONS |> Seq.map (fun kvp -> kvp.Key.Year) |> Seq.distinct |> Seq.sortDescending do
            flow.Add(YearActivityGrid(year, selected |> Setting.trigger(fun _ -> Menu.Back())))

        ScrollContainer(flow, Position = Position.ShrinkT(120.0f).ShrinkB(80.0f).SliceX(1380.0f), Margin = 50.0f)

    override this.Title = %"stats.activity"
    override this.OnClose() = ()

type RecentActivityGrid(selected: Setting<(DateOnly * Session) option>) =
    inherit Container(NodeType.Leaf)

    let session_dates =
        Stats.PREVIOUS_SESSIONS
        |> Map.map (fun _ sessions ->
            let day_playtime_hours = (sessions |> List.sumBy (_.PlayTime)) / 3600_000.0
            let color =
                if day_playtime_hours < 0.25 then Colors.cyan.O2
                elif day_playtime_hours < 0.5 then Colors.cyan.O3
                elif day_playtime_hours < 0.75 then Colors.cyan
                else Colors.cyan_accent
            sessions, color
        )

    let today, day_of_week =
        let today_datetime = Timestamp.now() |> timestamp_to_local_day
        DateOnly.FromDateTime(today_datetime), today_datetime.DayOfWeek

    let mutable hovered_day = None
    let PADDING = 20.0f

    override this.Init(parent: Widget) =
        this
        |* Button(
            sprintf "%s %s" Icons.ARROW_LEFT (%"stats.activity.view_older"),
            fun () -> AllYearsActivityPage(selected).Show()
            ,
            Floating = true,
            Position = Position.BorderB(30.0f).SliceL(120.0f).Translate(10.0f, 5.0f)
        )
        base.Init parent

    override this.Draw() =
        Render.rect this.Bounds Colors.shadow_2.O2

        let box_size = (this.Bounds.Height - PADDING * 2.0f) / 7.0f
        let weeks_shown = (this.Bounds.Width - PADDING * 2.0f) / box_size |> floor |> int
        let x_padding = (this.Bounds.Width - (box_size * float32 weeks_shown)) * 0.5f

        let mutable day = today.AddDays(- int day_of_week - (weeks_shown - 1) * 7)
        let mutable i = 0
        while day <= today do

            let pos = Rect.Box(this.Bounds.Left + x_padding + float32 (i / 7) * box_size, this.Bounds.Top + PADDING + float32 (i % 7) * box_size, box_size, box_size)

            let color =
                match session_dates.TryFind day with
                | Some (_, color) -> color
                | None -> Colors.black

            if Some day = Option.map fst selected.Value then
                Render.rect pos Colors.white.O2
            elif Some day = hovered_day then
                Render.rect pos Colors.yellow_accent.O2

            Render.rect (pos.ShrinkPercent(0.2f)) color

            i <- i + 1
            day <- day.AddDays 1

        match hovered_day with
        | Some d ->
            Text.draw_aligned_b(Style.font, d.ToString("dddd, dd MMMM yyyy"), 20.0f, this.Bounds.Right - 10.0f, this.Bounds.Top - 35.0f, Colors.text_subheading, Alignment.RIGHT)
        | None -> ()
        Text.draw_b(Style.font, %"stats.activity", 30.0f, this.Bounds.Left + 15.0f, this.Bounds.Top - 50.0f, Colors.text)
        base.Draw()

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)
        hovered_day <- None

        let box_size = (this.Bounds.Height - PADDING * 2.0f) / 7.0f
        let weeks_shown = (this.Bounds.Width - PADDING * 2.0f) / box_size |> floor |> int
        let x_padding = (this.Bounds.Width - (box_size * float32 weeks_shown)) * 0.5f

        let mutable day = today.AddDays(- int day_of_week - (weeks_shown - 1) * 7)
        let mutable i = 0
        while day <= today do

            let pos = Rect.Box(this.Bounds.Left + x_padding + float32 (i / 7) * box_size, this.Bounds.Top + PADDING + float32 (i % 7) * box_size, box_size, box_size)

            if Mouse.hover pos then
                hovered_day <- Some day

                if Mouse.left_click() then
                    match session_dates.TryFind day with
                    | Some (sessions, _) ->
                        selected.Value <- Some (day, sessions.[0])
                    | _ -> ()

                day <- today.AddDays 1

            i <- i + 1
            day <- day.AddDays 1

    member this.EarliestVisibleDay =
        let box_size = (this.Bounds.Height - PADDING * 2.0f) / 7.0f
        let weeks_shown = (this.Bounds.Width - PADDING * 2.0f) / box_size |> floor |> int
        today.AddDays(- int day_of_week - (weeks_shown - 1) * 7)