﻿namespace Interlude.Features.Import.Mounts

open Percyqaz.Common
open Prelude
open Prelude.Data.Library
open Percyqaz.Flux.UI
open Interlude.Content
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.Import

type private EditMountPage(game: MountedGameType, setting: Setting<Imports.MountedChartSource option>) =
    inherit Page()

    let mount = setting.Value.Value
    let import_on_startup = Setting.simple mount.ImportOnStartup
    let mutable import = false

    override this.Content() =
        page_container()
        |+ PageSetting(%"mount.importatstartup", Checkbox import_on_startup)
            .Tooltip(Tooltip.Info("mount.importatstartup"))
            .Pos(0)
        |+ PageButton
            .Once(
                %"mount.import",
                fun () ->
                    import <- true
                    Notifications.action_feedback (Icons.FOLDER_PLUS, %"notification.import_queued", "")
            )
            .Tooltip(Tooltip.Info("mount.import"))
            .Pos(3)
        |+ PageButton
            .Once(
                %"mount.importall",
                fun () ->
                    import <- true
                    mount.LastImported <- System.DateTime.UnixEpoch
                    Notifications.action_feedback (Icons.FOLDER_PLUS, %"notification.import_queued", "")
            )
            .Tooltip(Tooltip.Info("mount.importall"))
            .Pos(5)
        |+
            if
                game = MountedGameType.Osu
                && mount.LastImported <> System.DateTime.UnixEpoch
            then
                PageButton.Once(
                    %"mount.import_osu_scores",
                    fun () ->
                        osu.Scores.import_osu_scores_service.Request(
                            (),
                            fun () ->
                                Notifications.task_feedback (
                                    Icons.FOLDER_PLUS,
                                    %"notification.score_import_success",
                                    ""
                                )
                        )

                        Notifications.action_feedback (
                            Icons.FOLDER_PLUS,
                            %"notification.score_import_queued",
                            ""
                        )
                )
                    .Tooltip(Tooltip.Info("mount.import_osu_scores"))
                    .Pos(8)
            else
                Dummy()
        |+
            if
                game = MountedGameType.Osu
            then
                PageButton(
                    %"mount.import_osu_skins",
                    fun () -> osu.Skins.OsuSkinsListPage().Show()
                )
                    .Tooltip(Tooltip.Info("mount.import_osu_skins"))
                    .Pos(10)
            else
                Dummy()
        :> Widget

    override this.Title = %"mount"

    override this.OnClose() =
        setting.Value <-
            Some
                { mount with
                    ImportOnStartup = import_on_startup.Value
                }

        if import then
            Imports.import_mounted_source.Request(
                (setting.Value.Value, Content.Library),
                fun result ->
                    Notifications.task_feedback (
                        Icons.CHECK, 
                        %"notification.import_success",
                        [result.ConvertedCharts.ToString(); result.SkippedCharts.Length.ToString()] %> "notification.import_success.body"
                    )
                    Content.TriggerChartAdded()
            )