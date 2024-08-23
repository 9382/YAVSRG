﻿namespace Interlude.UI

open Percyqaz.Flux.Input
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude
open Interlude.UI

type private HelpInfo =
    {
        Data: Callout
        Size: float32 * float32
        Target: Widget
        mutable Delay: float
        Fade: Animation.Fade
        ByMouse: bool
    }

module HelpOverlay =

    let mutable private current_id: int = 0
    let mutable private current_info: HelpInfo option = None
    let mutable private _keep_alive = false

    type private Display() =
        inherit Overlay(NodeType.None)

        let SPACING = 20.0f

        override this.Update(elapsed_ms, moved) =
            base.Update(elapsed_ms, moved)

            match current_info with
            | None -> ()
            | Some t ->
                if not _keep_alive then
                    t.Fade.Target <- 0.0f

                if t.Delay > 0.0 then 
                    t.Delay <- t.Delay - elapsed_ms
                else 
                    t.Fade.Update elapsed_ms

                let outline = t.Target.Bounds.Expand(20.0f).Intersect(Viewport.bounds)
                let width, height = t.Size

                let x =
                    outline.CenterX - width * 0.5f
                    |> min (Viewport.bounds.Width - width - SPACING)
                    |> max SPACING

                let y =
                    if outline.Top > Viewport.bounds.CenterY then
                        outline.Top - SPACING - height
                    else
                        outline.Bottom + SPACING

                let callout_bounds = Rect.Box(x, y, width, height)
                Callout.update (callout_bounds.Left, callout_bounds.Top, width, height, t.Data)

            _keep_alive <- false

        override this.Draw() =
            match current_info with
            | None -> ()
            | Some t ->
                let outline = t.Target.Bounds.Expand(20.0f).Intersect(Viewport.bounds)
                
                let alpha = t.Fade.Alpha

                if not t.ByMouse then

                    LoadingAnimation.draw_border_piece outline 0.0f (min 0.99999f t.Fade.Value) (Colors.yellow_accent.O3a alpha)

                    // blackout effect
                    Draw.rect (Viewport.bounds.SliceL outline.Left) (Colors.shadow_2.O3a alpha)
                    Draw.rect (Viewport.bounds.ShrinkL outline.Right) (Colors.shadow_2.O3a alpha)

                    Draw.rect
                        (Viewport.bounds
                            .ShrinkL(outline.Left)
                            .SliceL(outline.Width)
                            .SliceT(outline.Top))
                        (Colors.shadow_2.O3a alpha)

                    Draw.rect
                        (Viewport.bounds
                            .ShrinkL(outline.Left)
                            .SliceL(outline.Width)
                            .ShrinkT(outline.Bottom))
                        (Colors.shadow_2.O3a alpha)

                // draw tooltip
                let width, height = t.Size

                let x =
                    outline.CenterX - width * 0.5f
                    |> min (Viewport.bounds.Width - width - SPACING)
                    |> max SPACING

                let y =
                    if outline.Top > Viewport.bounds.CenterY then
                        outline.Top - SPACING - height
                    else
                        outline.Bottom + SPACING

                let callout_bounds = Rect.Box(x, y, width, height)
                Draw.rect callout_bounds (Colors.cyan_shadow.O4a alpha)
                let border = Colors.cyan_accent.O4a alpha
                Draw.rect (callout_bounds.BorderCornersT Style.PADDING) border
                Draw.rect (callout_bounds.BorderCornersB Style.PADDING) border
                Draw.rect (callout_bounds.BorderL Style.PADDING) border
                Draw.rect (callout_bounds.BorderR Style.PADDING) border

                Callout.draw (
                    callout_bounds.Left,
                    callout_bounds.Top,
                    width,
                    height,
                    (Colors.white.O4a alpha, Colors.shadow_2.O4a alpha),
                    t.Data
                )

    let display : Widget = Display()

    let show (by_mouse: bool, w: Widget, body: Callout) =
        let t: HelpInfo =
            {
                Data = body
                Size = Callout.measure body
                Target = w
                ByMouse = by_mouse
                Delay = if by_mouse then 700.0 else 0.0
                Fade = Animation.Fade(0.0f, Target = 1.0f)
            }
        _keep_alive <- true
        current_info <- Some t
        current_id <- current_id + 1
        current_id

    let keep_alive (id: int) =
        if id = current_id then
            _keep_alive <- true

type Help(content: Callout) =
    inherit StaticWidget(NodeType.None)

    let content = content.Icon(Icons.INFO)
    let mutable hover = false
    let mutable by_mouse = false
    let mutable id = -1

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        if this.Parent.Focused && (%%"tooltip").Tapped() then

            id <- HelpOverlay.show (false, this, content)
            by_mouse <- false

        elif id >= 0 && not by_mouse then

            if (%%"tooltip").Pressed() then
                HelpOverlay.keep_alive id
            else
                id <- -1

        let next_hover = Mouse.hover this.Bounds

        if not hover && next_hover && Mouse.moved_recently () then

            id <- HelpOverlay.show (true, this, content)
            by_mouse <- true

        elif id >= 0 && by_mouse then

            if next_hover && (match Selection.get_focused_element() with Some (:? Dropdown.Item) -> false | _ -> true) then
                HelpOverlay.keep_alive id
            else 
                id <- -1

        hover <- next_hover

    override this.Draw() = ()

    static member Info(feature: string) =
        Callout.Normal
            .Title(%feature)
            .Body(%(sprintf "%s.tooltip" feature))

    static member Info(feature: string, hotkey: Hotkey) =
        Callout.Normal
            .Title(%feature)
            .Body(%(sprintf "%s.tooltip" feature))
            .Hotkey(hotkey)

[<AutoOpen>]
module Help =

    type Container with
        member this.Help(content: Callout) = this |+ Help(content)