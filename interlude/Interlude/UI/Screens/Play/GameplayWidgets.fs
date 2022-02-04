﻿namespace Interlude.UI.Screens.Play

open OpenTK
open System
open Prelude.Common
open Prelude.ChartFormats.Interlude
open Prelude.Scoring
open Prelude.Scoring.Grading
open Prelude.Data.Themes
open Interlude
open Interlude.Graphics
open Interlude.Options
open Interlude.UI
open Interlude.UI.Components
open Interlude.UI.Animation

(*
    Handful of widgets that directly pertain to gameplay
    They can all be toggled/repositioned/configured using themes
*)

module GameplayWidgets = 

    type Helper = {
        ScoringConfig: Ruleset
        Scoring: IScoreMetric
        HP: HealthBarMetric
        OnHit: IEvent<HitEvent<HitEventGuts>>
        CurrentChartTime: unit -> ChartTime
    }
    
    type AccuracyMeter(conf: WidgetConfig.AccuracyMeter, helper) as this =
        inherit Widget()

        let grades = helper.ScoringConfig.Grading.Grades
        let color = new AnimationColorMixer(if conf.GradeColors then Array.last(grades).Color else Color.White)
        let listener =
            if conf.GradeColors then
                helper.OnHit.Subscribe
                    ( fun _ ->
                        Grade.calculate grades helper.Scoring.State |> helper.ScoringConfig.GradeColor |> color.SetColor
                    )
            else null

        do
            this.Animation.Add(color)
            this.Add(new TextBox(helper.Scoring.FormatAccuracy, (fun () -> color.GetColor()), 0.5f) |> positionWidget(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.7f))
            if conf.ShowName then
                this.Add(new TextBox(Utils.K helper.Scoring.Name, (Utils.K Color.White), 0.5f) |> positionWidget(0.0f, 0.0f, 0.0f, 0.6f, 0.0f, 1.0f, 0.0f, 1.0f))
        
        override this.Dispose() =
            if isNull listener then () else listener.Dispose()

    type HitMeter(conf: WidgetConfig.HitMeter, helper) =
        inherit Widget()
        let hits = ResizeArray<struct (Time * float32 * int)>()
        let mutable w = 0.0f
        let listener =
            helper.OnHit.Subscribe(fun ev ->
                match ev.Guts with
                | Hit e when e.Judgement.IsSome ->
                    hits.Add (struct (ev.Time, e.Delta / helper.Scoring.MissWindow * w * 0.5f, int e.Judgement.Value))
                | Release e when e.Judgement.IsSome ->
                    hits.Add (struct (ev.Time, e.Delta / helper.Scoring.MissWindow * w * 0.5f, int e.Judgement.Value))
                | _ -> ())

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if w = 0.0f then w <- Rect.width this.Bounds
            let now = helper.CurrentChartTime()
            while hits.Count > 0 && let struct (time, _, _) = (hits.[0]) in time + conf.AnimationTime * 1.0f<ms> < now do
                hits.RemoveAt(0)

        override this.Draw() =
            base.Draw()
            let struct (left, top, right, bottom) = this.Bounds
            let centre = (right + left) * 0.5f
            if conf.ShowGuide then
                Draw.rect
                    (Rect.create (centre - conf.Thickness) top (centre + conf.Thickness) bottom)
                    Color.White
                    Sprite.Default
            let now = helper.CurrentChartTime()
            for struct (time, pos, j) in hits do
                Draw.rect
                    (Rect.create (centre + pos - conf.Thickness) top (centre + pos + conf.Thickness) bottom)
                    (let c = helper.ScoringConfig.JudgementColor j in
                        Color.FromArgb(Math.Clamp(255 - int (255.0f * (now - time) / conf.AnimationTime), 0, 255), c))
                    Sprite.Default

        override this.Dispose() =
            listener.Dispose()

    // disabled for now
    type JudgementMeter(conf: WidgetConfig.JudgementMeter, helper) =
        inherit Widget()
        let atime = conf.AnimationTime * 1.0f<ms>
        let mutable tier = 0
        let mutable late = 0
        let mutable time = -Time.infinity
        let texture = Content.getTexture "judgement"
        let listener =
            helper.OnHit.Subscribe
                ( fun ev ->
                    let (judge, delta) =
                        match ev.Guts with
                        | Hit e -> (e.Judgement, e.Delta)
                        | Release e -> (e.Judgement, e.Delta)
                    if
                        judge.IsSome && true
                        //match judge.Value with
                        //| _JType.RIDICULOUS
                        //| _JType.MARVELLOUS -> conf.ShowRDMA
                        //| _ -> true
                    then
                        let j = int judge.Value in
                        if j >= tier || ev.Time - atime > time then
                            tier <- j
                            time <- ev.Time
                            late <- if delta > 0.0f<ms> then 1 else 0
                )
        override this.Draw() =
            if time > -Time.infinity then
                let a = 255 - Math.Clamp(255.0f * (helper.CurrentChartTime() - time) / atime |> int, 0, 255)
                Draw.quad (Quad.ofRect this.Bounds) (Quad.colorOf (Color.FromArgb(a, Color.White))) (Sprite.gridUV (late, tier) texture)

        override this.Dispose() =
            listener.Dispose()

    type ComboMeter(conf: WidgetConfig.Combo, helper) as this =
        inherit Widget()
        let popAnimation = new AnimationFade(0.0f)
        let color = new AnimationColorMixer(Color.White)
        let mutable hits = 0
        let listener =
            helper.OnHit.Subscribe(
                fun _ ->
                    hits <- hits + 1
                    if (conf.LampColors && hits > 50) then
                        Lamp.calculate helper.ScoringConfig.Grading.Lamps helper.Scoring.State
                        |> helper.ScoringConfig.LampColor
                        |> color.SetColor
                    popAnimation.Value <- conf.Pop)

        do
            this.Animation.Add(color)
            this.Animation.Add(popAnimation)

        override this.Draw() =
            base.Draw()
            let combo = helper.Scoring.State.CurrentCombo
            let amt = popAnimation.Value + (((combo, 1000) |> Math.Min |> float32) * conf.Growth)
            Text.drawFill(Content.font, combo.ToString(), Rect.expand(amt, amt)this.Bounds, color.GetColor(), 0.5f)

        override this.Dispose() =
            listener.Dispose()

    type ProgressMeter(conf: WidgetConfig.ProgressMeter, helper) as this =
        inherit Widget()

        let duration = 
            let chart = Gameplay.getColoredChart()
            offsetOf chart.Notes.Last.Value - offsetOf chart.Notes.First.Value

        let pulse = new AnimationCounter(1000.0)

        do
            this.Animation.Add pulse

        override this.Draw() =
            base.Draw()

            let struct (l, t, r, b) = this.Bounds
            let height = b - t - conf.BarHeight
            let pc = helper.CurrentChartTime() / duration

            let bar = Rect.createWH l (t + height * pc) (r - l) conf.BarHeight
            let glowA = (float conf.GlowColor.A) * pulse.Time / 1000.0 |> int
            Draw.rect (Rect.expand (conf.GlowSize, conf.GlowSize) bar) (Color.FromArgb(glowA, conf.GlowColor)) Sprite.Default
            Draw.rect bar conf.BarColor Sprite.Default


    type SkipButton(conf: WidgetConfig.SkipButton, helper) as this =
        inherit Widget()
        
        let firstNote = offsetOf (Gameplay.getColoredChart().Notes.First.Value)
        do this.Add(TextBox(sprintf "Press %O to skip" options.Hotkeys.Skip.Value |> Utils.K, Utils.K Color.White, 0.5f))

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            if helper.CurrentChartTime() < -Audio.LEADIN_TIME * 2.5f then
                if options.Hotkeys.Skip.Value.Tapped() then
                    Audio.pause()
                    Audio.playFrom(firstNote - Audio.LEADIN_TIME)
            else this.Destroy()

    type LifeMeter(conf: WidgetConfig.LifeMeter, helper: Helper) as this =
        inherit Widget()

        let color = AnimationColorMixer(conf.FullColor)
        let slider = AnimationFade(float32 helper.HP.State.Health)

        do
            this.Animation.Add color
            this.Animation.Add slider

        override this.Update(elapsedTime, bounds) =
            slider.Target <- float32 helper.HP.State.Health
            // todo: color nyi
            base.Update(elapsedTime, bounds)

        override this.Draw() =
            let w, h = Rect.width this.Bounds, Rect.height this.Bounds
            if conf.Horizontal then
                let b = this.Bounds |> Rect.sliceLeft (w * float32 helper.HP.State.Health)
                Draw.rect b (color.GetColor 255) Sprite.Default
                Draw.rect (b |> Rect.sliceRight h) conf.EndColor Sprite.Default
            else
                let b = this.Bounds |> Rect.sliceBottom (h * float32 helper.HP.State.Health)
                Draw.rect b (color.GetColor 255) Sprite.Default
                Draw.rect (b |> Rect.sliceTop w) conf.EndColor Sprite.Default

    (*
        These widgets are configured by noteskin, not theme (and do not have positioning info)
    *)

    type ColumnLighting(keys, lightTime, helper) as this =
        inherit Widget()
        let sliders = Array.init keys (fun _ -> new AnimationFade(0.0f))
        let sprite = Content.getTexture "receptorlighting"
        let lightTime = Math.Min(0.99f, lightTime)

        do
            Array.iter this.Animation.Add sliders
            let hitpos = float32 options.HitPosition.Value
            this.Reposition(0.0f, hitpos, 0.0f, -hitpos)

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            Array.iteri (fun k (s: AnimationFade) -> if helper.Scoring.KeyState |> Bitmap.hasBit k then s.Value <- 1.0f) sliders

        override this.Draw() =
            base.Draw()
            let struct (l, t, r, b) = this.Bounds
            let columnwidth = (r - l) / (float32 keys)
            let threshold = 1.0f - lightTime
            let f k (s: AnimationFade) =
                if s.Value > threshold then
                    let p = (s.Value - threshold) / lightTime
                    let a = 255.0f * p |> int
                    Draw.rect
                        (
                            if options.Upscroll.Value then
                                Sprite.alignedBoxX(l + columnwidth * (float32 k + 0.5f), t, 0.5f, 1.0f, columnwidth * p, -1.0f / p) sprite
                            else Sprite.alignedBoxX(l + columnwidth * (float32 k + 0.5f), b, 0.5f, 1.0f, columnwidth * p, 1.0f / p) sprite
                        )
                        (Color.FromArgb(a, Color.White))
                        sprite
            Array.iteri f sliders

    type Explosions(keys, config: WidgetConfig.Explosions, helper) as this =
        inherit Widget()
        let sliders = Array.init keys (fun _ -> new AnimationFade(0.0f))
        let mem = Array.zeroCreate keys
        let holding = Array.create keys false
        let explodeTime = Math.Min(0.99f, config.FadeTime)
        let animation = new AnimationCounter(config.AnimationFrameTime)

        let handleEvent (ev: HitEvent<HitEventGuts>) =
            match ev.Guts with
            | Hit e when (config.ExplodeOnMiss || not e.Missed) ->
                sliders.[ev.Column].Target <- 1.0f
                sliders.[ev.Column].Value <- 1.0f
                holding.[ev.Column] <- true
                mem.[ev.Column] <- ev.Guts
            | Hit e when (config.ExplodeOnMiss || not e.Missed) ->
                sliders.[ev.Column].Value <- 1.0f
                mem.[ev.Column] <- ev.Guts
            | _ -> ()

        do
            this.Animation.Add animation
            Array.iter this.Animation.Add sliders
            let hitpos = float32 options.HitPosition.Value
            this.Reposition(0.0f, hitpos, 0.0f, -hitpos)
            helper.OnHit.Add handleEvent

        override this.Update(elapsedTime, bounds) =
            base.Update(elapsedTime, bounds)
            for k = 0 to (keys - 1) do
                if holding.[k] && helper.Scoring.KeyState |> Bitmap.hasBit k |> not then
                    holding.[k] <- false
                    sliders.[k].Target <- 0.0f

        override this.Draw() =
            base.Draw()
            let struct (l, t, r, b) = this.Bounds
            let columnwidth = (r - l) / (float32 keys)
            let threshold = 1.0f - explodeTime
            let f k (s: AnimationFade) =
                if s.Value > threshold then
                    let p = (s.Value - threshold) / explodeTime
                    let a = 255.0f * p |> int
                    
                    let box =
                        if options.Upscroll.Value then Rect.createWH (l + columnwidth * float32 k) t columnwidth columnwidth
                        else Rect.createWH (l + columnwidth * float32 k) (b - columnwidth) columnwidth columnwidth
                        |> Rect.expand((config.Scale - 1.0f) * columnwidth, (config.Scale - 1.0f) * columnwidth)
                        |> Rect.expand(config.ExpandAmount * (1.0f - p) * columnwidth, config.ExpandAmount * (1.0f - p) * columnwidth)
                    match mem.[k] with
                    | Hit e ->
                        let color = match e.Judgement with Some j -> int j | None -> 0
                        Draw.quad
                            (box |> Quad.ofRect |> Quad.rotateDeg (NoteRenderer.noteRotation keys k))
                            (Quad.colorOf (Color.FromArgb(a, Color.White)))
                            (Sprite.gridUV (animation.Loops, color) (Content.getTexture (if e.IsHold then "holdexplosion" else "noteexplosion")))
                    | _ -> ()
            Array.iteri f sliders

    // Screencover is controlled by game settings, not theme or noteskin

    type ScreenCover() =
        inherit Widget()

        override this.Draw() =
            
            if options.ScreenCover.Enabled.Value then

                let bounds = Rect.expand (0.0f, 2.0f) this.Bounds
                let fadeLength = float32 options.ScreenCover.FadeLength.Value
                let upper (amount: float32) =
                    Draw.rect (bounds |> Rect.sliceTop (amount - fadeLength)) options.ScreenCover.Color.Value Sprite.Default
                    Draw.quad
                        (bounds |> Rect.sliceTop amount |> Rect.sliceBottom fadeLength |> Quad.ofRect)
                        struct (options.ScreenCover.Color.Value, options.ScreenCover.Color.Value, Color.FromArgb(0, options.ScreenCover.Color.Value), Color.FromArgb(0, options.ScreenCover.Color.Value))
                        Sprite.DefaultQuad
                let lower (amount: float32) =
                    Draw.rect (bounds |> Rect.sliceBottom (amount - fadeLength)) options.ScreenCover.Color.Value Sprite.Default
                    Draw.quad
                        (bounds |> Rect.sliceBottom amount |> Rect.sliceTop fadeLength |> Quad.ofRect)
                        struct (Color.FromArgb(0, options.ScreenCover.Color.Value), Color.FromArgb(0, options.ScreenCover.Color.Value), options.ScreenCover.Color.Value, options.ScreenCover.Color.Value)
                        Sprite.DefaultQuad

                let height = Rect.height bounds

                let sudden = float32 options.ScreenCover.Sudden.Value * height
                let hidden = float32 options.ScreenCover.Hidden.Value * height

                if options.Upscroll.Value then upper hidden; lower sudden
                else lower hidden; upper sudden