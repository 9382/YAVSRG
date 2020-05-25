﻿namespace Interlude.UI

open System
open System.Collections.Generic
open Prelude.Common
open Interlude.Render
open Interlude.Utils
open Interlude
open Interlude.UI.Animation
open OpenTK

type AnchorPoint(value, anchor) =
    inherit AnimationFade(value)
    let mutable anchor = anchor
    member this.Position(min, max) =  min + base.Value + (max - min) * anchor
    member this.Reposition(value, a) = this.SetValue(value); this.SetTarget(value); anchor <- a
    member this.MoveRelative(min, max, value) = this.SetTarget(value - min - (max - min) * anchor)
    member this.RepositionRelative(min, max, value) = this.MoveRelative(min, max, value); this.SetValue(value - min - (max - min) * anchor)

type WidgetState = Normal = 1uy | Active = 2uy | Disabled = 3uy | Uninitialised = 4uy

type Widget() =

    let left = AnchorPoint(0.f, 0.f)
    let top = AnchorPoint(0.f, 0.f)
    let right = AnchorPoint(0.f, 1.f)
    let bottom = AnchorPoint(0.f, 1.f)

    let animation = AnimationGroup()
    do
        animation.Add(left)
        animation.Add(right)
        animation.Add(top)
        animation.Add(bottom)
    let mutable parent = None
    let mutable bounds = Rect.zero
    let mutable state = (WidgetState.Uninitialised ||| WidgetState.Normal)
    let children = new List<Widget>()

    abstract member Add: Widget -> unit
    default this.Add(c) =
        children.Add(c)
        c.AddTo(this)

    abstract member AddTo: Widget -> unit
    default this.AddTo(c) =
        match parent with
        | None -> parent <- Some c
        | Some _ -> Logging.Error("Tried to add this widget to a container when it is already in one") ""
        
    abstract member Remove: Widget -> unit
    default this.Remove(c) =
        if children.Remove(c) then
            c.RemoveFrom(this)
        else Logging.Error("Tried to remove widget that was not in this container") ""

    member private this.RemoveFrom(c) =
        match parent with
        | None -> Logging.Error("Tried to remove this widget from a container it isn't in one") ""
        | Some p -> if p = c then parent <- None else Logging.Error("Tried to remove this widget from a container when it is in another") ""

    member this.Animation = animation
    member this.Bounds = bounds
    member this.Position = (left, top, right, bottom)
    member this.State with get() = state and set(value) = state <- value
    member this.Children = children :> seq<Widget>
    member this.Initialised = byte (this.State &&& WidgetState.Uninitialised) = 0uy

    //todo: locks on children for thread protection
    abstract member Draw: unit -> unit
    default this.Draw() =
        children
        |> Seq.filter (fun w -> w.State < WidgetState.Disabled)
        |> Seq.iter (fun w -> w.Draw())

    abstract member Update: float * Rect -> unit
    default this.Update(elapsedTime, (l, t, r, b): Rect) =
        animation.Update(elapsedTime)
        this.State <- (this.State &&& WidgetState.Disabled) //removes uninitialised flag
        bounds <- Rect.create <| left.Position(l, r) <| top.Position(t, b) <| right.Position(l, r) <| bottom.Position(t, b)
        seq {
            for i in children.Count - 1 .. -1 .. 0 do
                if (children.[i].State &&& WidgetState.Disabled < WidgetState.Disabled) then yield children.[i]
        }
        |> Seq.iter (fun w -> w.Update(elapsedTime, bounds))

    member this.Reposition(l, la, t, ta, r, ra, b, ba) =
        left.Reposition(l, la)
        top.Reposition(t, ta)
        right.Reposition(r, ra)
        bottom.Reposition(b, ba)
    
    member this.Reposition(l, t, r, b) = this.Reposition(l, 0.f, t, 0.f, r, 1.f, b, 1.f)

    member this.Move(l, t, r, b) =
        left.SetTarget(l)
        top.SetTarget(t)
        right.SetTarget(r)
        bottom.SetTarget(b)

    interface IDisposable with
        member this.Dispose() = ()

type Screen() =
    inherit Widget()
    abstract member OnEnter: Screen -> unit
    default this.OnEnter(prev: Screen) = ()
    abstract member OnExit: Screen -> unit
    default this.OnExit(next: Screen) = ()

//Collection of mutable values to "tie the knot" in mutual dependence
// - Stuff is defined but not inialised here
// - Stuff is then referenced by screen logic
// - Overall screen manager references screen logic AND initialises values, connecting the loop

module Screens =
    let mutable internal addScreen: Screen -> unit = ignore
    let mutable internal popScreen: unit -> unit = ignore
    //add dialog
    //background fbo
    //accent color as animation

    let accentColor = AnimationColorMixer(otkColor Themes.accentColor)
    
    let accentShade(alpha, brightness, white) =
        let accentColor = accentColor.GetColor()
        let rd = float32 (255uy - accentColor.R) * white
        let gd = float32 (255uy - accentColor.G) * white
        let bd = float32 (255uy - accentColor.B) * white
        Color.FromArgb(alpha,
            int ((float32 accentColor.R + rd) * brightness),
            int ((float32 accentColor.G + gd) * brightness),
            int ((float32 accentColor.B + bd) * brightness))