﻿namespace Interlude.Features.Noteskins.Edit

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Skinning.Noteskins
open Interlude.Content
open Interlude.UI.Menu
open Interlude.Options
open Interlude.Features.Play.HUD

type AccuracyPage(on_close: unit -> unit) =
    inherit Page()

    let user_options = options.HUD.Value
    let noteskin_options = Content.NoteskinConfig.HUD

    let pos = Setting.simple noteskin_options.AccuracyPosition

    let grade_colors = Setting.simple user_options.AccuracyGradeColors
    let show_name = Setting.simple user_options.AccuracyShowName

    let use_font = Setting.simple noteskin_options.AccuracyUseFont
    let font_spacing = Setting.simple noteskin_options.AccuracyFontSpacing |> Setting.bound -1.0f 1.0f
    let font_dot_spacing = Setting.simple noteskin_options.AccuracyDotExtraSpacing |> Setting.bound -1.0f 1.0f
    let font_percent_spacing = Setting.simple noteskin_options.AccuracyPercentExtraSpacing |> Setting.bound -1.0f 1.0f

    let texture = Content.Texture "accuracy-font"
    let preview =
        { new ConfigPreviewNew(pos.Value) with
            override this.DrawComponent(bounds) =
                if use_font.Value then
                    Accuracy.draw_accuracy_centered(
                        texture,
                        bounds.TrimBottom(bounds.Height * 0.4f),
                        Color.White,
                        0.9672,
                        font_spacing.Value,
                        font_dot_spacing.Value,
                        font_percent_spacing.Value
                    )
                else
                    Text.fill (Style.font, "96.72%", bounds.TrimBottom(bounds.Height * 0.3f), Color.White, 0.5f)

                if show_name.Value then
                    Text.fill (Style.font, "SC J4", bounds.SliceBottom(bounds.Height * 0.4f), Color.White, 0.5f)
        }

    override this.Content() =
        page_container()
        |+ PageSetting(%"hud.accuracy.gradecolors", Checkbox grade_colors)
            .Tooltip(Tooltip.Info("hud.accuracy.gradecolors"))
            .Pos(0)
        |+ PageSetting(%"hud.accuracy.showname", Checkbox show_name)
            .Tooltip(Tooltip.Info("hud.accuracy.showname"))
            .Pos(2)
        |+ ([
            PageSetting(%"hud.generic.use_font", Checkbox use_font)
                .Tooltip(Tooltip.Info("hud.generic.use_font"))
                .Pos(7)
            PageSetting(%"hud.generic.font_spacing", Slider.Percent(font_spacing))
                .Tooltip(Tooltip.Info("hud.generic.font_spacing"))
                .Pos(9)
                .Conditional(use_font.Get)
            PageSetting(%"hud.generic.dot_spacing", Slider.Percent(font_dot_spacing))
                .Tooltip(Tooltip.Info("hud.generic.dot_spacing"))
                .Pos(11)
                .Conditional(use_font.Get)
            PageSetting(%"hud.generic.percent_spacing", Slider.Percent(font_percent_spacing))
                .Tooltip(Tooltip.Info("hud.generic.percent_spacing"))
                .Pos(13)
                .Conditional(use_font.Get)
        ] |> or_require_noteskin)
        |>> Container
        |+ preview
        :> Widget

    override this.Title = %"hud.accuracy"

    override this.OnClose() =
        options.HUD.Set
            { options.HUD.Value with
                AccuracyGradeColors = grade_colors.Value
                AccuracyShowName = show_name.Value
            }

        Noteskins.save_hud_config
            { Content.NoteskinConfig.HUD with
                AccuracyPosition = pos.Value

                AccuracyUseFont = use_font.Value
                AccuracyFontSpacing = font_spacing.Value
                AccuracyDotExtraSpacing = font_dot_spacing.Value
                AccuracyPercentExtraSpacing = font_percent_spacing.Value
            }

        on_close ()