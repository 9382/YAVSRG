namespace Interlude.Features.OptionsMenu.Advanced

open Percyqaz.Flux.UI
open Prelude.Data.Library.Caching
open Prelude
open Interlude.Options
open Interlude.Content
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.OptionsMenu

type AdvancedPage() =
    inherit Page()

    override this.Content() =
        page_container ()
        |+ PageSetting(%"advanced.enableconsole", Checkbox options.EnableConsole)
            .Pos(0)
        |+ PageSetting(%"advanced.confirmexit", Checkbox options.ConfirmExit)
            .Tooltip(Tooltip.Info("advanced.confirmexit"))
            .Pos(2)
        |+ PageSetting(%"advanced.holdtogiveup", Checkbox options.HoldToGiveUp)
            .Tooltip(Tooltip.Info("advanced.holdtogiveup"))
            .Pos(4)
        |+ PageSetting(%"advanced.vanishingnotes", Checkbox options.VanishingNotes)
            .Tooltip(Tooltip.Info("advanced.vanishingnotes"))
            .Pos(6)
        |+ PageSetting(%"advanced.automatic_offset", Checkbox options.AutoCalibrateOffset)
            .Tooltip(Tooltip.Info("advanced.automatic_offset"))
            .Pos(8)
        |+ PageButton
            .Once(
                %"advanced.buildpatterncache",
                fun () ->
                    Cache.cache_patterns.Request(
                        (Content.Cache, true),
                        fun () ->
                            Notifications.system_feedback (
                                Icons.ALERT_OCTAGON,
                                %"notification.pattern_cache_complete.title",
                                ""
                            )
                    )

                    Notifications.system_feedback (
                        Icons.ALERT_OCTAGON,
                        %"notification.pattern_cache_started.title",
                        %"notification.pattern_cache_started.body"
                    )
            )
            .Tooltip(Tooltip.Info("advanced.buildpatterncache"))
            .Pos(10)
        |+ PageSetting(%"advanced.advancedrecommendations", Checkbox options.AdvancedRecommendations)
            .Tooltip(Tooltip.Info("advanced.advancedrecommendations"))
            .Pos(12)
        :> Widget

    override this.Title = %"advanced"
    override this.OnClose() = ()