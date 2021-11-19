﻿namespace Interlude.UI

open System.Drawing
open Interlude
open Interlude.Utils
open Interlude.UI.Animation

module Style =

    let accentColor = AnimationColorMixer Content.accentColor.Value

    let accentShade (alpha, brightness, white) =
        let accentColor = accentColor.GetColor()
        let r = float32 accentColor.R
        let g = float32 accentColor.G
        let b = float32 accentColor.B
        let rd = (255.0f - r * brightness) * white
        let gd = (255.0f - g * brightness) * white
        let bd = (255.0f - b * brightness) * white
        Color.FromArgb(alpha,
            int ((r + rd) * brightness) |> min 255,
            int ((g + gd) * brightness) |> min 255,
            int ((b + bd) * brightness) |> min 255)

    type private ColorFunc = unit -> Color
    type private ColorFuncF = float32 -> Color

    let alpha a (c: 'T -> Color) = fun x -> Color.FromArgb(a, c x)
    let alphaF (a: AnimationFade) (c: ColorFunc) = fun () -> Color.FromArgb(int (a.Value * 255.0f), c ())

    let white a : ColorFunc = K <| Color.FromArgb(a, Color.White)
    let black a : ColorFunc = K <| Color.FromArgb(a, Color.Black)

    let text : unit -> Color * Color = K (Color.White, Color.Black)

    /// 1.0, 0.0
    let highlight a : ColorFunc = fun () ->
        accentShade (a, 1.0f, 0.0f)
        
    /// 1.0, 0.5
    let highlightL a : ColorFunc = fun () ->
        accentShade (a, 1.0f, 0.5f)

    /// 0.25, 0.0
    let highlightD a : ColorFunc = fun () ->
        accentShade (a, 0.25f, 0.0f)

    /// 1.0f, f
    let highlightF a : ColorFuncF = fun f ->
        accentShade (a, 1.0f, f)
        
    /// 0.9, 0.0
    let main a : ColorFunc = fun () ->
        accentShade (a, 0.9f, 0.0f)
                
    /// 0.9, f
    let mainF a : ColorFuncF = fun f ->
        accentShade (a, 0.9f, f)
    
    /// 0.25, 0.5
    let dark a : ColorFunc = fun () ->
        accentShade (a, 0.25f, 0.5f)
    
    /// 0.25, 0.75
    let darkL a : ColorFunc = fun () ->
        accentShade (a, 0.25f, 0.75f)
    
    /// 0.1, 0.25
    let darkD a : ColorFunc = fun () ->
        accentShade (a, 0.1f, 0.25f)