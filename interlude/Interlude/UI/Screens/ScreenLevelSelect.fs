﻿namespace Interlude.UI

open System
open System.Drawing
open System.Linq
open OpenTK.Mathematics
open OpenTK.Windowing.GraphicsLibraryFramework
open Prelude.Common
open Prelude.Data.ScoreManager
open Prelude.Data.ChartManager
open Prelude.Data.ChartManager.Sorting
open Prelude.Gameplay.Score
open Prelude.Gameplay.Mods
open Prelude.Gameplay.Difficulty
open Interlude.Gameplay
open Interlude.Themes
open Interlude.Utils
open Interlude.Render
open Interlude.Options
open Interlude.Input
open Interlude.UI.Animation
open Interlude.UI.Components
open Interlude.UI.Selection

module private ScreenLevelSelectVars =

    let mutable selectedGroup = ""
    let mutable selectedChart = "" //hash
    let mutable scrollTo = false
    let mutable expandedGroup = ""
    let mutable scrollBy = ignore
    let mutable colorVersionGlobal = 0
    let mutable colorFunc = fun (_, _, _) -> Color.Transparent

    let mutable scoreSystem = "SC+ (J4)"
    //todo: have these update when score system is changed, could be done remotely, exactly when settings are changed
    let mutable hpSystem = "VG"

    type Navigation =
    | Nothing
    | Backward of string * CachedChart
    | Forward of bool

    let mutable navigation = Nothing

    let switchCurrentChart(cc, groupName) =
        match cache.LoadChart(cc) with
        | Some c ->
            changeChart(cc, c)
            selectedChart <- cc.Hash
            expandedGroup <- groupName
            selectedGroup <- groupName
            scrollTo <- true
        | None -> Logging.Error("Couldn't load cached file: " + cc.FilePath) ""

    let playCurrentChart() =
        if currentChart.IsSome then Screens.newScreen(ScreenPlay >> (fun s -> s :> Screen), ScreenType.Play, ScreenTransitionFlag.Default)
        else Logging.Warn("Tried to play selected chart; There is no chart selected") ""

module ScreenLevelSelect =

    //publicly accessible so that other importing can request that the level select is refreshed
    let mutable refresh = false

    open ScreenLevelSelectVars

    [<AutoOpen>]
    module private InfoPanel =

        type ScoreboardSort =
        | Time = 0
        | Performance = 1
        | Accuracy = 2

        type ScoreboardFilter =
        | All = 0
        | CurrentRate = 1
        | CurrentPlaystyle = 2
        | CurrentMods = 3

        type ScoreboardItem(data: ScoreInfoProvider) as this =
            inherit Widget()

            do
                TextBox((fun () -> sprintf "%s / %i" (data.Accuracy.Format()) (let (_, _, _, _, _, cbs) = data.Accuracy.State in cbs)), K (Color.White, Color.Black), 0.0f)
                |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.6f)
                |> this.Add

                TextBox((fun () -> sprintf "%s / %ix" (data.Lamp.ToString()) (let (_, _, _, _, combo, _) = data.Accuracy.State in combo)), K (Color.White, Color.Black), 0.0f)
                |> positionWidget(0.0f, 0.0f, 0.0f, 0.6f, 0.0f, 0.5f, 0.0f, 1.0f)
                |> this.Add

                TextBox(K data.Mods, K (Color.White, Color.Black), 1.0f)
                |> positionWidget(0.0f, 0.5f, 0.0f, 0.6f, 0.0f, 1.0f, 0.0f, 1.0f)
                |> this.Add

                Clickable((fun () -> Screens.newScreen((fun () -> new ScreenScore(data, (PersonalBestType.None, PersonalBestType.None, PersonalBestType.None)) :> Screen), ScreenType.Score, ScreenTransitionFlag.Default)), ignore)
                |> this.Add

                this.Reposition(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 75.0f, 0.0f)

            override this.Draw() =
                Draw.rect this.Bounds (Screens.accentShade(127, 0.8f, 0.0f)) Sprite.Default
                base.Draw()
            member this.Data = data

        type Scoreboard() as this =
            inherit Selectable()

            let flowContainer = new FlowContainer()
            let mutable empty = false
            //todo: store these in options
            let filter = Setting(ScoreboardFilter.All)
            let sort = Setting(ScoreboardSort.Performance)

            let mutable chart = ""
            let mutable scoring = ""
            let ls = new ListSelectable(true)

            do
                LittleButton.FromEnum("Sort", sort, this.Refresh)
                |> positionWidget(20.0f, 0.0f, -35.0f, 1.0f, -20.0f, 0.25f, -5.0f, 1.0f)
                |> ls.Add

                LittleButton.FromEnum("Filter", filter, this.Refresh)
                |> positionWidget(20.0f, 0.25f, -35.0f, 1.0f, -20.0f, 0.5f, -5.0f, 1.0f)
                |> ls.Add

                LittleButton(K "Score System", this.Refresh) //nyi
                |> positionWidget(20.0f, 0.5f, -35.0f, 1.0f, -20.0f, 0.75f, -5.0f, 1.0f)
                |> ls.Add

                LittleButton(K "Local Scores", this.Refresh) //nyi
                |> positionWidget(20.0f, 0.75f, -35.0f, 1.0f, -20.0f, 1.0f, -5.0f, 1.0f)
                |> ls.Add

                ls |> this.Add
                flowContainer
                |> positionWidgetA(0.0f, 10.0f, 0.0f, -40.0f)
                |> this.Add

            override this.OnSelect() =
                base.OnSelect()
                let (left, _, right, _) = this.Anchors
                left.Target <- 0.0f
                right.Target <- 0.0f

            override this.OnDeselect() =
                base.OnSelect()
                let (left, _, right, _) = this.Anchors
                left.Target <- -800.0f
                right.Target <- -800.0f

            member this.Refresh() =
                let h = match Interlude.Gameplay.currentCachedChart with Some c -> c.Hash | None -> ""
                if h <> chart || (match chartSaveData with None -> false | Some d -> d.Scores.Count <> flowContainer.Children.Count)then
                    chart <- h
                    flowContainer.Clear()
                    match chartSaveData with
                    | None -> ()
                    | Some d ->
                        for score in d.Scores do
                            ScoreInfoProvider(score, currentChart.Value, options.AccSystems.Get() |> fst, options.HPSystems.Get() |> fst)
                            |> ScoreboardItem
                            |> flowContainer.Add
                    empty <- flowContainer.Children.Count = 0
                if scoring <> scoreSystem then
                    let s = options.AccSystems.Get() |> fst
                    for c in flowContainer.Children do (c :?> ScoreboardItem).Data.AccuracyType <- s
                    scoring <- scoreSystem

                flowContainer.Sort(
                    match sort.Get() with
                    | ScoreboardSort.Accuracy -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.Accuracy.Value.CompareTo((b :?> ScoreboardItem).Data.Accuracy.Value))
                    | ScoreboardSort.Performance -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.Physical.CompareTo((b :?> ScoreboardItem).Data.Physical))
                    | ScoreboardSort.Time
                    | _ -> Comparison(fun b a -> (a :?> ScoreboardItem).Data.Score.time.CompareTo((b :?> ScoreboardItem).Data.Score.time))
                    )
                flowContainer.Filter(
                    match filter.Get() with
                    | ScoreboardFilter.CurrentRate -> (fun a -> (a :?> ScoreboardItem).Data.Score.rate = rate)
                    | ScoreboardFilter.CurrentPlaystyle -> (fun a -> (a :?> ScoreboardItem).Data.Score.layout = options.Playstyles.[(a :?> ScoreboardItem).Data.Score.keycount - 3])
                    | ScoreboardFilter.CurrentMods// -> (fun a -> (a :?> ScoreboardItem).Data.Score.selectedMods <> null) //nyi
                    | _ -> K true
                    )

            override this.Update(elapsedTime, bounds) =
                base.Update(elapsedTime, bounds)
                if this.Selected && (ls.Selected <> options.Hotkeys.Scoreboard.Get().Pressed()) then ls.Selected <- not ls.Selected

        type ModSelectItem(name: string) as this =
            inherit Selectable()

            do
                TextBox(ModState.getModName name |> K, K (Color.White, Color.Black), 0.0f)
                |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.6f)
                |> this.Add

                TextBox(ModState.getModDesc name |> K, K (Color.White, Color.Black), 0.0f)
                |> positionWidget(0.0f, 0.0f, 0.0f, 0.6f, 0.0f, 1.0f, 0.0f, 1.0f)
                |> this.Add

                Clickable(
                    (fun () -> if this.SParent.Value.Selected then this.Selected <- true),
                    (fun b -> if b && this.SParent.Value.Selected then this.Hover <- true))
                |> this.Add

            override this.Draw() =
                let hi = Screens.accentShade(255, 1.0f, 0.0f)
                let lo = Color.FromArgb(100, hi)
                let e = selectedMods.ContainsKey(name)
                Draw.quad (Quad.ofRect this.Bounds)
                    (struct((if this.Hover then hi else lo), (if e then hi else lo), (if e then hi else lo), if this.Hover then hi else lo))
                    Sprite.DefaultQuad
                base.Draw()

            override this.OnSelect() =
                base.OnSelect()
                ModState.cycleState name selectedMods
                updateChart()
                this.Selected <- false

        type ModSelect() as this =
            inherit ListSelectable(false)
            do
                let mutable i = 0.0f
                for k in modList.Keys do
                    this.Add(ModSelectItem(k) |> positionWidget(0.0f, 0.0f, i * 80.0f, 0.0f, 0.0f, 1.0f, 75.0f + i * 80.0f, 0.0f))
                    i <- i + 1.0f

            override this.OnSelect() =
                base.OnSelect()
                let (left, _, right, _) = this.Anchors
                left.Target <- 0.0f
                right.Target <- 0.0f

            override this.OnDeselect() =
                base.OnSelect()
                let (left, _, right, _) = this.Anchors
                left.Target <- -800.0f
                right.Target <- -800.0f

    type InfoPanel() as this =
        inherit Selectable()

        let mods = ModSelect()
        let scores = Scoreboard()
        let mutable length = ""
        let mutable bpm = ""

        do
            mods
            |> positionWidgetA(-800.0f, 0.0f, -800.0f, -200.0f)
            |> this.Add

            scores
            |> positionWidgetA(0.0f, 0.0f, 0.0f, -200.0f)
            |> this.Add

            new TextBox(
                (fun () -> match difficultyRating with None -> "0.00⭐" | Some d -> sprintf "%.2f⭐" d.Physical),
                (fun () -> Color.White, match difficultyRating with None -> Color.Black | Some d -> physicalColor d.Physical), 0.0f)
            |> positionWidget(10.0f, 0.0f, -190.0f, 1.0f, 0.0f, 0.5f, -120.0f, 1.0f)
            |> this.Add

            new TextBox(
                (fun () -> match difficultyRating with None -> "0.00⭐" | Some d -> sprintf "%.2f⭐" d.Technical),
                (fun () -> Color.White, match difficultyRating with None -> Color.Black | Some d -> technicalColor d.Technical), 0.0f)
            |> positionWidget(10.0f, 0.0f, -120.0f, 1.0f, 0.0f, 0.5f, -50.0f, 1.0f)
            |> this.Add

            new TextBox((fun () -> bpm), K (Color.White, Color.Black), 1.0f)
            |> positionWidget(0.0f, 0.5f, -190.0f, 1.0f, -10.0f, 1.0f, -120.0f, 1.0f)
            |> this.Add

            new TextBox((fun () -> length), K (Color.White, Color.Black), 1.0f)
            |> positionWidget(0.0f, 0.5f, -120.0f, 1.0f, -10.0f, 1.0f, -50.0f, 1.0f)
            |> this.Add

            new TextBox((fun () -> getModString(rate, selectedMods)), K (Color.White, Color.Black), 0.0f)
            |> positionWidget(17.0f, 0.0f, -50.0f, 1.0f, -50.0f, 1.0f, -10.0f, 1.0f)
            |> this.Add

            scores.Selected <- true

        override this.Update(elapsedTime, bounds) =
            if options.Hotkeys.Mods.Get().Tapped() then
                mods.Selected <- true
            elif options.Hotkeys.Scoreboard.Get().Tapped() then
                scores.Selected <- true
            base.Update(elapsedTime, bounds)

        member this.Refresh() =
            length <-
                match currentCachedChart with
                | Some cc -> cc.Length
                | None -> 0.0f<ms>
                |> fun x -> (x / 1000.0f / 60.0f |> int, (x / 1000f |> int) % 60)
                |> fun (x, y) -> sprintf "⌛ %i:%02i" x y
            bpm <-
                match currentCachedChart with
                | Some cc -> cc.BPM
                | None -> (120.0f<ms/beat>, 120.0f<ms/beat>)
                |> fun (b, a) -> (60000.0f<ms> / a |> int, 60000.0f<ms> / b |> int)
                |> fun (a, b) ->
                    if Math.Abs(a - b) < 5 || b > 9000 then sprintf "♬ %i" a
                    elif a > 9000 || b < 0 then sprintf "♬ ∞"
                    else sprintf "♬ %i-%i" a b
            scores.Refresh()

    let CHARTHEIGHT = 85.0f
    let PACKHEIGHT = 65.0f
    let ITEMSPACING = 10.0f

    [<AbstractClass>]
    type LevelSelectItem2() =
        abstract member Bounds: float32 -> Rect
        abstract member IsSelected: bool
        abstract member Navigate: unit -> unit
        abstract member OnDraw: Rect * bool -> unit
        abstract member OnUpdate: Rect * bool * float -> unit

        abstract member Draw: float32 -> float32
        default this.Draw(top: float32) =
            let bounds = this.Bounds(top)
            if top > 70.0f && top < Render.vheight then this.OnDraw(bounds, this.IsSelected)
            top + Rect.height bounds + 10.0f

        abstract member Update: float32 * float -> float32
        default this.Update(top: float32, elapsedTime) =
            this.Navigate()
            let bounds = this.Bounds(top)
            if top > 70.0f && top < Render.vheight then this.OnUpdate(bounds, this.IsSelected, elapsedTime)
            top + Rect.height bounds + 10.0f

    type LevelSelectChartItem(groupName, cc) =
        inherit LevelSelectItem2()

        let hover = new AnimationFade(0.0f)
        let mutable colorVersion = -1
        let mutable color = Color.Transparent
        let mutable chartData = None
        let mutable pbData = (None, None, None)

        override this.Bounds(top) = Rect.create (Render.vwidth * 0.4f) top Render.vwidth (top + CHARTHEIGHT)
        override this.IsSelected = selectedChart = cc.Hash

        override this.Navigate() =
            match navigation with
            | Nothing -> ()
            | Forward b ->
                if b then
                    switchCurrentChart(cc, groupName); navigation <- Nothing
                elif groupName = selectedGroup && this.IsSelected then
                    navigation <- Forward true
            | Backward (groupName2, cc2) ->
                if groupName = selectedGroup && this.IsSelected then
                    switchCurrentChart(cc2, groupName2); navigation <- Nothing
                else navigation <- Backward(groupName, cc)

        override this.OnDraw(bounds, selected) =
            let struct (left, top, right, bottom) = bounds
            Draw.rect bounds (Screens.accentShade(127, 0.8f, 0.0f)) Sprite.Default
            let twidth = Math.Max(Text.measure(font(), cc.Artist + " - " + cc.Title) * 23.0f, Text.measure(font(), cc.DiffName + " // " + cc.Creator) * 20.0f + 40.0f) + 20.0f
            let stripeLength = twidth + (right - left) * 0.3f * hover.Value
            Draw.quad
                (Quad.create <| new Vector2(left, top) <| new Vector2(left + stripeLength, top) <| new Vector2(left + stripeLength - 40.0f, bottom - 25.0f) <| new Vector2(left, bottom - 25.0f))
                (Quad.colorOf <| Screens.accentShade(127, 1.0f, 0.2f))
                (Sprite.gridUV(0, 0) Sprite.Default)
            Draw.rect(Rect.sliceBottom 25.0f bounds)(Screens.accentShade(60, 0.3f, 0.0f)) Sprite.Default
            Text.drawB(font(), cc.Artist + " - " + cc.Title, 23.0f, left, top, (Color.White, Color.Black))
            Text.drawB(font(), cc.DiffName + " // " + cc.Creator, 18.0f, left, top + 30.0f, (Color.White, Color.Black))

            let f (p: PersonalBests<'T> option) (format: 'T -> string) (color: 'T -> Color) =
                match p with
                | None -> ("", Color.Transparent)
                | Some ((p1, r1), (p2, r2)) ->
                    if r1 < rate then (sprintf "%s (%.2fx)" (format p2) r2, if r2 < rate then Color.Silver else color p2)
                    else (sprintf "%s (%.2fx)" (format p1) r1, color p1)
            let (accAndGrades, lamp, clear) = pbData
            let (t, c) = f accAndGrades (fun (x, _) -> sprintf "%.2f%%" (100.0 * x)) (fun (_, g) -> ScoreColor.gradeToColor g) in Text.draw(font(), t, 15.0f, left, top + 60.0f, c)
            let (t, c) = f lamp (fun x -> x.ToString()) ScoreColor.lampToColor in Text.draw(font(), t, 15.0f, left + 200.0f, top + 60.0f, c)
            let (t, c) = f clear (fun x -> if x then "CLEAR" else "FAILED") ScoreColor.clearToColor in Text.draw(font(), t, 15.0f, left + 400.0f, top + 60.0f, c)

            let border = Rect.expand(5.0f, 5.0f) bounds
            let borderColor = if selected then Color.White else color
            if borderColor.A > 0uy then
                Draw.rect(Rect.sliceLeft 5.0f border) borderColor Sprite.Default
                Draw.rect(Rect.sliceTop 5.0f border) borderColor Sprite.Default
                Draw.rect(Rect.sliceRight 5.0f border) borderColor Sprite.Default
                Draw.rect(Rect.sliceBottom 5.0f border) borderColor Sprite.Default

        override this.OnUpdate(bounds, selected, elapsedTime) =
            if colorVersion < colorVersionGlobal then
                let f key (d: Collections.Generic.Dictionary<string, PersonalBests<_>>) =
                    if d.ContainsKey(key) then Some d.[key] else None
                colorVersion <- colorVersionGlobal
                if chartData.IsNone then chartData <- scores.GetScoreData(cc.Hash)
                match chartData with
                | Some d -> pbData <- (f scoreSystem d.Accuracy |> Option.map (PersonalBests.map (fun x -> x, grade x themeConfig.GradeThresholds)), f scoreSystem d.Lamp, f (scoreSystem + "|" + hpSystem) d.Clear)
                | None -> ()
                color <- colorFunc pbData
            if Mouse.Hover(bounds) then
                hover.Target <- 1.0f
                if Mouse.Click(MouseButton.Left) then
                    if selected then playCurrentChart()
                    else switchCurrentChart(cc, groupName)
                elif Mouse.Click(MouseButton.Right) then
                    expandedGroup <- ""
                    scrollTo <- true
            else hover.Target <- 0.0f
            hover.Update(elapsedTime) |> ignore
        override this.Update(top, elapsedTime) =
            if scrollTo && groupName = selectedGroup && this.IsSelected then
                scrollBy(-top + 500.0f)
                scrollTo <- false
            base.Update(top, elapsedTime)

    type LevelSelectPackItem(name, items: LevelSelectChartItem list) =
        inherit LevelSelectItem2()

        override this.Bounds(top) = Rect.create (Render.vwidth * 0.4f) top (Render.vwidth * 0.6f) (top + PACKHEIGHT)
        override this.IsSelected = selectedGroup = name
        member this.Expanded = expandedGroup = name

        override this.Navigate() = () //nyi

        override this.OnDraw(bounds, selected) =
            let struct (left, top, right, bottom) = bounds
            Draw.rect bounds (if selected then Screens.accentShade(127, 1.0f, 0.2f) else Screens.accentShade(127, 0.5f, 0.0f)) Sprite.Default
            Text.drawFillB(font(), name, bounds, (Color.White, Color.Black), 0.5f)
        override this.Draw(top) =
            let b = base.Draw(top)
            if this.Expanded then List.fold (fun t (i: LevelSelectChartItem) -> i.Draw(t)) b items else b

        override this.OnUpdate(bounds, selected, elapsedTime) =
            if Mouse.Hover(bounds) && Mouse.Click(MouseButton.Left) then
                if this.Expanded then expandedGroup <- "" else expandedGroup <- name
        override this.Update(top, elapsedTime) =
            if scrollTo && this.IsSelected && not this.Expanded then
                scrollBy(-top + 500.0f)
                scrollTo <- false
            let b = base.Update(top, elapsedTime)
            if this.Expanded then List.fold (fun t (i: LevelSelectChartItem) -> i.Update(t, elapsedTime)) b items
            else List.iter (fun (i: LevelSelectChartItem) -> i.Navigate()) items; b

open ScreenLevelSelect
open ScreenLevelSelectVars

type ScreenLevelSelect() as this =
    inherit Screen()

    let mutable selection: LevelSelectPackItem list = []
    let mutable lastItem: (string * CachedChart) option = None
    let mutable filter: Filter = []
    let scrollPos = new AnimationFade(300.0f)
    let searchText = new Setting<string>("")
    let infoPanel = new InfoPanel()

    let refresh() =
        scoreSystem <- (options.AccSystems.Get() |> fst).ToString()
        infoPanel.Refresh()
        let groups = cache.GetGroups groupBy.[options.ChartGroupMode.Get()] sortBy.[options.ChartSortMode.Get()] filter
        if groups.Count = 1 then
            let g = groups.Keys.First()
            if groups.[g].Count = 1 then
                let cc = groups.[g].[0]
                if cc.Hash <> selectedChart then
                    match cache.LoadChart(cc) with
                    | Some c -> changeChart(cc, c)
                    | None -> Logging.Error("Couldn't load cached file: " + cc.FilePath) ""
        lastItem <- None
        colorVersionGlobal <- 0
        selection <-
            groups.Keys
            |> Seq.sort
            |> Seq.map
                (fun k ->
                    groups.[k]
                    |> Seq.map (fun cc ->
                        match currentCachedChart with
                        | None -> ()
                        | Some c -> if c.Hash = cc.Hash then selectedChart <- c.Hash; selectedGroup <- k
                        lastItem <- Some (k, cc)
                        LevelSelectChartItem(k, cc))
                    |> List.ofSeq
                    |> fun l -> LevelSelectPackItem(k, l))
            |> List.ofSeq
        scrollTo <- true
        expandedGroup <- selectedGroup

    let changeRate(v) = Interlude.Gameplay.changeRate(v); colorVersionGlobal <- colorVersionGlobal + 1; infoPanel.Refresh()

    do
        if not <| sortBy.ContainsKey(options.ChartSortMode.Get()) then options.ChartSortMode.Set "Title"
        if not <| groupBy.ContainsKey(options.ChartGroupMode.Get()) then options.ChartGroupMode.Set "Pack"
        this.Animation.Add scrollPos
        scrollBy <- fun amt -> scrollPos.Target <- scrollPos.Target + amt

        let sorts = sortBy.Keys |> Array.ofSeq
        new Dropdown(sorts, Array.IndexOf(sorts, options.ChartSortMode.Get()),
            (fun i -> options.ChartSortMode.Set(sorts.[i]); refresh()), "Sort by", 50.0f)
        |> positionWidget(-400.0f, 1.0f, 100.0f, 0.0f, -250.0f, 1.0f, 400.0f, 0.0f)
        |> this.Add

        let groups = groupBy.Keys |> Array.ofSeq
        new Dropdown(groups, Array.IndexOf(groups, options.ChartGroupMode.Get()),
            (fun i -> options.ChartGroupMode.Set(groups.[i]); refresh()), "Group by", 50.0f)
        |> positionWidget(-200.0f, 1.0f, 100.0f, 0.0f, -50.0f, 1.0f, 400.0f, 0.0f)
        |> this.Add

        new SearchBox(searchText, fun f -> filter <- f; refresh())
        |> positionWidget(-600.0f, 1.0f, 20.0f, 0.0f, -50.0f, 1.0f, 80.0f, 0.0f)
        |> this.Add

        new TextBox((fun () -> match currentCachedChart with None -> "" | Some c -> c.Title), K (Color.White, Color.Black), 0.5f)
        |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.4f, 100.0f, 0.0f)
        |> this.Add

        new TextBox((fun () -> match currentCachedChart with None -> "" | Some c -> c.DiffName), K (Color.White, Color.Black), 0.5f)
        |> positionWidget(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.4f, 160.0f, 0.0f)
        |> this.Add

        infoPanel
        |> positionWidget(10.0f, 0.0f, 180.0f, 0.0f, -10.0f, 0.4f, 0.0f, 1.0f)
        |> this.Add

        onChartChange <- infoPanel.Refresh

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        if ScreenLevelSelect.refresh then refresh(); ScreenLevelSelect.refresh <- false

        if options.Hotkeys.Select.Get().Tapped() then playCurrentChart()

        elif options.Hotkeys.UpRateSmall.Get().Tapped() then changeRate(0.01f)
        elif options.Hotkeys.UpRateHalf.Get().Tapped() then changeRate(0.05f)
        elif options.Hotkeys.UpRate.Get().Tapped() then changeRate(0.1f)
        elif options.Hotkeys.DownRateSmall.Get().Tapped() then changeRate(-0.01f)
        elif options.Hotkeys.DownRateHalf.Get().Tapped() then changeRate(-0.05f)
        elif options.Hotkeys.DownRate.Get().Tapped() then changeRate(-0.1f)

        elif options.Hotkeys.Next.Get().Tapped() then
            if lastItem.IsSome then
                let h = (lastItem.Value |> snd).Hash
                navigation <- Navigation.Forward(selectedGroup = fst lastItem.Value && selectedChart = h)
        elif options.Hotkeys.Previous.Get().Tapped() then
            if lastItem.IsSome then
                navigation <- Navigation.Backward(lastItem.Value)

        let struct (left, top, right, bottom)  = this.Bounds
        let bottomEdge =
            selection
            |> List.fold (fun t (i: LevelSelectPackItem) -> i.Update(t, elapsedTime)) scrollPos.Value
        let height = bottomEdge - scrollPos.Value - 320.0f
        if Mouse.Held(MouseButton.Right) then
            scrollPos.Target <- -(Mouse.Y() - (top + 250.0f))/(bottom - top - 250.0f) * height
        scrollPos.Target <- Math.Min(Math.Max(scrollPos.Target + Mouse.Scroll() * 100.0f, -height + 600.0f), 300.0f)

    override this.Draw() =
        let struct (left, top, right, bottom) = this.Bounds
        //level select stuff
        Stencil.create(false)
        Draw.rect(Rect.create 0.0f (top + 170.0f) Render.vwidth bottom) Color.Transparent Sprite.Default
        Stencil.draw()
        let bottomEdge =
            selection
            |> List.fold (fun t (i: LevelSelectPackItem) -> i.Draw(t)) scrollPos.Value
        Stencil.finish()
        //todo: make this render right, is currently bugged
        let scrollPos = (scrollPos.Value / (scrollPos.Value - bottomEdge)) * (bottom - top - 100.0f)
        Draw.rect(Rect.create (Render.vwidth - 10.0f) (top + 225.0f + scrollPos) (Render.vwidth - 5.0f) (top + 245.0f + scrollPos)) Color.White Sprite.Default

        Draw.rect(Rect.create left top right (top + 170.0f))(Screens.accentShade(100, 0.6f, 0.0f)) Sprite.Default
        Draw.rect(Rect.create left (top + 170.0f) right (top + 175.0f))(Screens.accentShade(255, 0.8f, 0.0f)) Sprite.Default
        base.Draw()

    override this.OnEnter(prev) =
        base.OnEnter(prev)
        refresh()

    override this.OnExit(next) =
        base.OnExit(next)
        Input.removeInputMethod()