﻿namespace Interlude.Features.Online

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu

type RegisterPage(discord_tag) as this =
    inherit Page()

    let username = Setting.simple ""

    let register () =
        Network.complete_registration (username.Value.Trim())

    let submit_button = PageButton("register.register", register, Enabled = false)

    let agree_tos = Setting.simple false |> Setting.trigger submit_button.set_Enabled

    let handler = Network.Events.successful_login.Subscribe(fun _ -> Menu.Back())

    let info =
        Callout.Normal
            .Icon(Icons.INFO)
            .Title("Complete registration")
            .Body("Choose your username wisely! This is what gets displayed on your profile.")
            .Body("Usernames must be 3-20 characters long, and can have at most 2 special characters")
            .Body(
                "By completing your registration you confirm that:\n You have read, understood and agree to abide by Interlude's Terms of Service and Privacy Policy\n You are 18 years old or older"
            )

    do
        this.Content(
            page_container()
            |+ PageTextEntry("register.username", username).Pos(2)
            |+ Text(
                "Creating an account linked to " + discord_tag,
                Position = pretty_pos(0, 2, PageWidth.Full),
                Align = Alignment.LEFT
            )
            |+ Callout.frame info (fun (w, h) -> pretty_pos(3, 5, PageWidth.Custom w))
            |+ PageButton("register.terms_of_service", (fun () -> open_url ("https://yavsrg.net/terms_of_service")))
                .Pos(9)
            |+ PageButton("register.privacy_policy", (fun () -> open_url ("https://yavsrg.net/privacy_policy")))
                .Pos(11)
            |+ PageSetting("register.confirm_terms_of_service", Selector<_>.FromBool agree_tos)
                .Pos(13)
            |+ submit_button.Pos(16)
        )

    override this.Title = %"register.name"
    override this.OnClose() = handler.Dispose()

type LoginPage() as this =
    inherit Page()

    let mutable waiting_for_browser = false

    let login () =
        if not waiting_for_browser then
            waiting_for_browser <- true
            Network.begin_login ()

    let register () =
        if not waiting_for_browser then
            waiting_for_browser <- true
            Network.begin_registration ()

    let a = Network.Events.successful_login.Subscribe(fun _ -> Menu.Back())

    let b =
        Network.Events.waiting_registration.Subscribe(fun discord_tag ->
            waiting_for_browser <- false
            RegisterPage(discord_tag).Show()
        )

    let c = Network.Events.login_failed.Subscribe(fun _ -> waiting_for_browser <- false)

    let d =
        Network.Events.registration_failed.Subscribe(fun _ -> waiting_for_browser <- false)

    let info = Callout.Small.Icon(Icons.GLOBE).Title(%"login.waiting_for_discord")

    do
        this.Content(
            page_container()
            |+ PageButton("login.login_with_discord", login).Pos(0)
            |+ PageButton("login.register_with_discord", register).Pos(3)
            |+ Conditional(
                (fun () -> waiting_for_browser),
                Callout.frame info (fun (w, h) -> Position.Row(400.0f, h).Margin(100.0f, 0.0f))
            )
        )

    override this.Title = %"login.name"

    override this.OnClose() =
        a.Dispose()
        b.Dispose()
        c.Dispose()
        d.Dispose()
