﻿namespace Interlude.UI

open System
open System.Drawing
open OpenTK.Mathematics
open Interlude
open Interlude.Render
open Interlude.UI.Animation
open Interlude.UI.Components
open Interlude.Utils
open Interlude.Input

// Menu screen
type MenuButton(onClick, label) as this =
    inherit Widget()

    let color = AnimationFade(0.3f)
    do
        this.Animation.Add(color)
        this.Add(new Clickable(onClick, fun b -> color.Target <- if b then 0.3f else 0.3f))
        this.Add(new TextBox(K label, K (Color.White, Color.Black), 0.5f) |> positionWidget(0.0f, 0.7f, 10.0f, 0.0f, 0.0f, 1.0f, -20.0f, 1.0f))

    override this.Draw() =
        Draw.quad(Quad.parallelogram 0.5f this.Bounds)(Quad.colorOf(Screens.accentShade(200, 1.0f, color.Value)))(Sprite.DefaultQuad)
        base.Draw()

    member this.Pop() =
        let (_, _, r, _) = this.Position
        r.Value <- -Render.vwidth

type ScreenMenu() as this =
    inherit Screen()

    let playFunc() =
        Screens.logo.Move(-Render.vwidth * 0.5f - 600.0f, -300.0f, -Render.vwidth * 0.5f, 300.0f)
        Screens.addScreen(ScreenLevelSelect >> (fun s -> s :> Screen), ScreenTransitionFlag.UnderLogo)
    let play = MenuButton(playFunc, "Play")
    let options = MenuButton(ignore, "Options")
    let quit = MenuButton(ignore, "Quit")

    do
        this.Add(play |> positionWidget(-100.0f, 0.0f, -200.0f, 0.5f, 1200.0f, 0.0f, -100.0f, 0.5f))
        this.Add(options |> positionWidget(-100.0f, 0.0f, -50.0f, 0.5f, 1130.0f, 0.0f, 50.0f, 0.5f))
        this.Add(quit |> positionWidget(-100.0f, 0.0f, 100.0f, 0.5f, 1060.0f, 0.0f, 200.0f, 0.5f))
        Utils.AutoUpdate.checkForUpdates()

    override this.OnEnter(prev: Screen) =
        Screens.logo.Move(-Render.vwidth * 0.5f, -400.0f, 800.0f - Render.vwidth * 0.5f, 400.0f)
        Screens.backgroundDim.Target <- 0.0f
        Screens.setToolbarCollapsed(false)
        play.Pop(); options.Pop(); quit.Pop();

    override this.OnExit(next: Screen) =
        Screens.logo.Move(-Render.vwidth * 0.5f - 600.0f, -300.0f, -Render.vwidth * 0.5f, 300.0f)
        Screens.backgroundDim.Target <- 0.7f

    override this.Draw() =
        let (x, y) = Rect.center this.Bounds
        base.Draw()

    override this.Update(time, bounds) =
        base.Update(time, bounds)
        if (Options.options.Hotkeys.Select.Get().Tapped()) then playFunc()

// Loading screen

type ScreenLoading() as this =
    inherit Screen()

    let mutable closing = false
    let fade = new AnimationFade(1.0f)
    do
        this.Animation.Add(fade)

    override this.OnEnter(prev: Screen) =
        fade.Value <- 0.0f
        Screens.logo.Move(-400.0f, -400.0f, 400.0f, 400.0f)
        Screens.setToolbarCollapsed(true)
        match prev with
        | :? ScreenMenu ->
            closing <- true
            let s = AnimationSequence()
            s.Add(AnimationTimer(1500.0))
            s.Add(AnimationAction(fun () -> Screens.popScreen(ScreenTransitionFlag.Default)))
            this.Animation.Add(s)
        | _ -> 
            let s = AnimationSequence()
            s.Add(AnimationTimer(1500.0))
            s.Add(AnimationAction(fun () -> Screens.addScreen(ScreenMenu >> (fun s -> s :> Screen), ScreenTransitionFlag.UnderLogo)))
            this.Animation.Add(s)

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        Audio.changeVolume(Options.options.AudioVolume.Get() * float (if closing then 1.0f - fade.Value else fade.Value))
        
    override this.Draw() =
        let (x, y) = Rect.center this.Bounds
        Text.drawJust(Themes.font(), (if closing then "Bye o/" else "Loading :)"), 80.f, x, y - 500.0f, Color.White, 0.5f)

// Toolbar widgets

module Notifications =
    open Prelude.Common

    let taskBox(t: BackgroundTask.ManagedTask) = 
        let w = Frame()
        w.Add(
            new TextBox(t.get_Name, K (Color.White, Color.Black), 0.0f)
            |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 50.0f, 0.0f))
        w.Add(
            new TextBox(t.get_Info, K (Color.White, Color.Black), 0.0f)
            |> positionWidget(0.0f, 0.0f, 50.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f))
        w.Add(
            Clickable(
                (fun () ->
                    match t.Status with
                    | Threading.Tasks.TaskStatus.RanToCompletion -> w.RemoveFromParent()
                    | _ -> t.Cancel(); w.RemoveFromParent()), ignore))
        w |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 90.0f, 0.0f)

    type TaskDisplay(f) as this = 
        inherit Widget()
        let WIDTH = 400.0f

        let items = FlowContainer(0.0f, 10.0f)
        let fade = new AnimationFade(0.0f)
        do
            this.Animation.Add(fade)
            this.Reposition(0.0f, 1.0f, -f, 0.0f, WIDTH, 1.0f, f, 1.0f)
            this.Add(items)
            BackgroundTask.Subscribe(fun t -> if t.Visible then items.Add(taskBox(t)))

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds |> Rect.translate(-WIDTH * fade.Value, 0.0f))
            if Options.options.Hotkeys.Tasklist.Get().Pressed() then fade.Target <- 1.0f
            else fade.Target <- 0.0f

        override this.Draw() =
            if fade.Value > 0.01f then
                Draw.rect(this.Bounds)(Screens.accentShade(180, 0.2f, 0.0f))(Sprite.Default)
                base.Draw()

    type NotificationDisplay() as this =
        inherit Widget()
        let items = ResizeArray<Color * string * AnimationFade>()
        let slider = new AnimationFade(0.0f)
        let notifWidth = 400.0f
        let notifHeight = 35.0f

        do
            this.Animation.Add(slider)
            Screens.addNotification <-
                fun (str: string, t: NotificationType) ->
                    let c =
                        match t with
                        | NotificationType.Info -> Color.Blue
                        | NotificationType.System -> Color.Green
                        | NotificationType.Task -> Color.Purple
                        | NotificationType.Error -> Color.Red
                        | _ -> Color.Black
                    slider.Target <- slider.Target + 1.0f
                    let f = new AnimationFade((if items.Count = 0 then 0.0f else 1.0f), Target = 1.0f)
                    this.Animation.Add(f)
                    let i = (c, str, f)
                    items.Add(i)
                    this.Animation.Add(
                        Animation.Serial(
                            AnimationTimer 4000.0,
                            AnimationAction(fun () -> f.Target <- 0.0f),
                            AnimationTimer 1500.0,
                            AnimationAction(fun () -> slider.Target <- slider.Target - 1.0f; slider.Value <- slider.Value - 1.0f; f.Stop(); items.Remove(i) |> ignore)
                        ))

        override this.Draw() =
            if items.Count > 0 then
                Stencil.create(false)
                Draw.rect this.Bounds Color.Transparent Sprite.Default
                Stencil.draw()
                let struct (_, _, _, b) = this.Bounds
                let m = Rect.centerX this.Bounds
                let mutable y = b - notifHeight * slider.Value
                for (c, s, f) in items do
                    let r = Rect.create (m - notifWidth) y (m + notifWidth) (y + notifHeight)
                    let f = f.Value * 255.0f |> int
                    Draw.rect r (Color.FromArgb(f / 2, c)) Sprite.Default
                    Text.drawFill(Themes.font(), s, r, Color.FromArgb(f, Color.White), 0.5f)
                    y <- y + notifHeight
                Stencil.finish()

type Jukebox() as this =
    inherit Widget()
    //todo: right click to seek/tools to pause and play music
    let fade = new AnimationFade(0.0f)
    let slider = new AnimationFade(0.0f)
    do
        this.Animation.Add(fade)
        this.Animation.Add(slider)

    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)
        if Options.options.Hotkeys.Volume.Get().Pressed() then
            fade.Target <- 1.0f
            Options.options.AudioVolume.Set(Options.options.AudioVolume.Get() + float (Mouse.Scroll()) * 0.02)
            Audio.changeVolume(Options.options.AudioVolume.Get())
            slider.Target <- float32 <| Options.options.AudioVolume.Get()
        else
            fade.Target <- 0.0f

    override this.Draw() =
        let r = Rect.sliceBottom(5.0f) this.Bounds
        Draw.rect(r)(Screens.accentShade(int (255.0f * fade.Value), 0.4f, 0.0f))(Sprite.Default)
        Draw.rect(r |> Rect.sliceLeft(slider.Value * (Rect.width r)))(Screens.accentShade(int (255.0f * fade.Value), 1.0f, 0.0f))(Sprite.Default)

// Toolbar

type Toolbar() as this =
    inherit Widget()

    static let height = 70.f

    let barSlider = new AnimationFade(1.0f)
    let notifSlider = new AnimationFade(0.0f)

    let mutable userCollapse = false
    let mutable forceCollapse = true
    
    do
        this.Animation.Add(barSlider)
        this.Animation.Add(notifSlider)
        this.Add(new TextBox(K version, K (Color.White, Color.Black), 1.0f) |> positionWidget(-300.f, 1.f, 0.f, 1.f, 0.f, 1.f, height * 0.5f, 1.f))
        this.Add(new TextBox((fun () -> System.DateTime.Now.ToString()), K (Color.White, Color.Black), 1.0f) |> positionWidget(-300.f, 1.f, height * 0.5f, 1.f, 0.f, 1.f, height, 1.f))
        this.Add(new Button((fun () -> Screens.popScreen(ScreenTransitionFlag.UnderLogo)), "Back", Options.options.Hotkeys.Exit, Sprite.Default) |> positionWidget(0.0f, 0.0f, 0.0f, 1.0f, 200.f, 0.0f, height, 1.0f))
        this.Add(new Button((fun () -> Screens.addDialog(new OptionsMenu())), "Options", Options.options.Hotkeys.Options, Sprite.Default) |> positionWidget(0.0f, 0.0f, -height, 0.0f, 200.f, 0.0f, 0.0f, 0.0f))
        this.Add(new Button((fun () -> (ScreenImport >> (fun s -> s :> Screen), ScreenTransitionFlag.Default) |> Screens.addScreen), "Import", Options.options.Hotkeys.Import, Sprite.Default) |> positionWidget(200.0f, 0.0f, -height, 0.0f, 400.f, 0.0f, 0.0f, 0.0f))
        this.Add(new Button(ignore, "Help", Options.options.Hotkeys.Help, Sprite.Default) |> positionWidget(400.0f, 0.0f, -height, 0.0f, 600.f, 0.0f, 0.0f, 0.0f))
        this.Add(new Jukebox())
        this.Add(new Notifications.NotificationDisplay())
        this.Add(new Notifications.TaskDisplay(height))

        Screens.setToolbarCollapsed <- (fun b -> forceCollapse <- b)

    override this.Draw() = 
        let struct (l, t, r, b) = this.Bounds
        Draw.rect(Rect.create l (t - height) r t) (Screens.accentShade(127, 0.8f, 0.0f)) Sprite.Default
        Draw.rect(Rect.create l b r (b + height)) (Screens.accentShade(127, 0.8f, 0.0f)) Sprite.Default
        if barSlider.Value > 0.01f then
            let s = (r - l) / 48.0f
            for i in 0 .. 47 do
                let level = System.Math.Min((Audio.waveForm.[i] + 0.01f) * barSlider.Value * 0.4f, height)
                Draw.rect(Rect.create (l + float32 i * s + 2.0f) (t - height) (l + (float32 i + 1.0f) * s - 2.0f) (t - height + level))(Screens.accentShade(int level, 1.0f, 0.5f))(Sprite.Default)
                Draw.rect(Rect.create (r - (float32 i + 1.0f) * s + 2.0f) (b + height - level) (r - float32 i * s - 2.0f) (b + height))(Screens.accentShade(int level, 1.0f, 0.5f))(Sprite.Default)
        base.Draw()

    override this.Update(elapsed, bounds) =
        if (not forceCollapse) && Options.options.Hotkeys.Toolbar.Get().Tapped() then
            userCollapse <- not userCollapse
            barSlider.Target <- if userCollapse then 0.0f else 1.0f
        base.Update(elapsed, Rect.expand (0.f, -height * if forceCollapse then 0.0f else barSlider.Value) bounds)

//Screen manager

type ScreenContainer() as this =
    inherit Widget()

    let dialogs = new ResizeArray<Dialog>()
    let mutable current = new ScreenLoading() :> Screen
    let mutable screens = [current]
    let mutable exit = false
    
    let mutable cursor = true

    let transitionTime = 500.0
    let mutable transitionFlags = ScreenTransitionFlag.Default
    let screenTransition = new AnimationSequence()
    let t1 = new AnimationTimer(transitionTime)
    let t2 = new AnimationTimer(transitionTime)

    let toolbar = new Toolbar()

    do
        Screens.addScreen <- this.AddScreen
        Screens.popScreen <- this.RemoveScreen
        Screens.addDialog <- this.AddDialog
        Screens.setCursorVisible <- (fun b -> cursor <- b)
        this.Add(toolbar)
        this.Add(Screens.logo |> Components.positionWidget(-300.0f, 0.5f, 1000.0f, 0.5f, 300.0f, 0.5f, 1600.0f, 0.5f))
        this.Animation.Add(screenTransition)
        this.Animation.Add(Screens.accentColor)
        this.Animation.Add(Screens.parallaxZ)
        this.Animation.Add(Screens.parallaxX)
        this.Animation.Add(Screens.parallaxY)
        this.Animation.Add(Screens.backgroundDim)
        current.OnEnter(current)

    member this.Exit = exit

    member this.AddDialog(d: Dialog) =
        dialogs.Add(d)

    member this.AddScreen(s: unit -> Screen, flags) =
        transitionFlags <- flags
        if screenTransition.Complete then
            this.Animation.Add(screenTransition)
            screenTransition.Add(t1)
            screenTransition.Add(
                new AnimationAction(
                    fun () ->
                        let s = s()
                        if (flags &&& ScreenTransitionFlag.NoBacktrack <> ScreenTransitionFlag.NoBacktrack) then screens <- s :: screens
                        current.OnExit(s)
                        s.OnEnter(current)
                        current <- s))
            screenTransition.Add(t2)
            t2.FrameSkip() //ignore frame lag spike when initialising screen
            screenTransition.Add(new AnimationAction(fun () -> t1.Reset(); t2.Reset()))

    member this.RemoveScreen(flags) =
        transitionFlags <- flags
        if screenTransition.Complete then
            this.Animation.Add(screenTransition)
            screenTransition.Add(t1)
            screenTransition.Add(
                new AnimationAction(
                    fun () ->
                        current.Dispose()
                        let previous = current
                        screens <- List.tail screens
                        match List.tryHead screens with
                        | None -> exit <- true
                        | Some s ->
                            current.OnExit(s)
                            current <- s
                            s.OnEnter(previous)))
            screenTransition.Add(t2)
            screenTransition.Add(new AnimationAction(fun () -> t1.Reset(); t2.Reset()))

    override this.Update(elapsedTime, bounds) =
        if Render.vwidth > 0.0f then
            Screens.parallaxX.Target <- Mouse.X() / Render.vwidth
            Screens.parallaxY.Target <- Mouse.Y() / Render.vheight
        Screens.accentColor.SetColor(Themes.accentColor)
        if dialogs.Count > 0 then
            dialogs.[dialogs.Count - 1].Update(elapsedTime, bounds)
            if dialogs.[dialogs.Count - 1].State = WidgetState.Disabled then
                dialogs.[dialogs.Count - 1].Dispose()
                dialogs.RemoveAt(dialogs.Count - 1)
            Input.absorbAll()
        base.Update(elapsedTime, bounds)
        current.Update(elapsedTime, toolbar.Bounds)

    override this.Draw() =
        Screens.drawBackground(this.Bounds, Color.White, 1.0f)
        Draw.rect this.Bounds (Color.FromArgb(Screens.backgroundDim.Value * 255.0f |> int, 0, 0, 0)) Sprite.Default
        current.Draw()
        base.Draw()
        //TODO: move all this transitional logic somewhere nice and have lots of them
        if not screenTransition.Complete then
            let amount = Math.Clamp((if t1.Elapsed < transitionTime then t1.Elapsed / transitionTime else (transitionTime - t2.Elapsed) / transitionTime), 0.0, 1.0) |> float32

            let s = 150.0f

            let size x =
                let f = Math.Clamp(((if t1.Elapsed < transitionTime then amount else 1.0f - amount) - (x - 2.0f * s) / Render.vwidth) / ((4.0f * s) / Render.vwidth), 0.0f, 1.0f)
                if t1.Elapsed < transitionTime then f * s * 0.5f else (1.0f - f) * s * 0.5f
            let diamond x y =
                let r = size x
                Draw.quad(Quad.create <| new Vector2(x - r, y) <| new Vector2(x, y - r) <| new Vector2(x + r, y) <| new Vector2(x, y + r))(Quad.colorOf Color.Transparent)(Sprite.DefaultQuad)
                
            Stencil.create(false)
            for x in 0 .. (Render.vwidth / s |> float |> Math.Ceiling |> int) do
                for y in 0 .. (Render.vheight / s |> float |> Math.Ceiling |> int) do
                    diamond (s * float32 x) (s * float32 y)
                    diamond (0.5f * s + s * float32 x) (0.5f * s + s * float32 y)
            Stencil.draw()
            Screens.drawBackground(this.Bounds, Screens.accentShade(255.0f * amount |> int, 1.0f, 0.0f), 1.0f)
            Stencil.finish()
            if (transitionFlags &&& ScreenTransitionFlag.UnderLogo = ScreenTransitionFlag.UnderLogo) then Screens.logo.Draw()
        for d in dialogs do
            d.Draw()
        if cursor then Draw.rect(Rect.create <| Mouse.X() <| Mouse.Y() <| Mouse.X() + Themes.themeConfig.CursorSize <| Mouse.Y() + Themes.themeConfig.CursorSize)(Screens.accentShade(255, 1.0f, 0.5f))(Themes.getTexture("cursor"))
