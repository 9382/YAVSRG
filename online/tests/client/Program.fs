﻿open System
open Percyqaz.Common
open Interlude.Web.Shared

Logging.Info "Make sure you have the server running"

type Status =
    | Disconnected
    | Connected
    | LoggedIn
    | InLobby of int

let NUMBER_OF_CLIENTS = 100

type TestClient(i: int) =
    inherit Client(System.Net.IPAddress.Parse("127.0.0.1"), 32767)

    let mutable ready_to_play = false

    let mutable status = Disconnected

    member this.Status = status

    override this.OnConnected() = status <- Connected

    override this.OnDisconnected() = status <- Disconnected

    override this.OnPacketReceived(packet: Downstream) =
        match packet with
        | Downstream.HANDSHAKE_SUCCESS -> this.Send(Upstream.LOGIN(sprintf "Test user %i" i))
        | Downstream.LOGIN_SUCCESS username ->
            Logging.Info(sprintf "%s logged in " username)
            status <- LoggedIn

            if i = 0 then
                this.Send(Upstream.GET_LOBBIES)
        | Downstream.LOBBY_LIST lobbies ->
            if lobbies.Length = 0 then
                this.Send(Upstream.CREATE_LOBBY "Test lobby")
            else
                this.Send(Upstream.JOIN_LOBBY(lobbies.[0].Id))
        | Downstream.YOU_JOINED_LOBBY ps ->
            if i = 0 then
                for j = 1 to NUMBER_OF_CLIENTS - 1 do
                    this.Send(Upstream.INVITE_TO_LOBBY(sprintf "Test user %i" j))

                this.Send(Upstream.INVITE_TO_LOBBY "Percyqaz")

            status <- InLobby 1
        | Downstream.SELECT_CHART _ ->
            this.Send(Upstream.READY_STATUS(if i % 2 = 1 then ReadyFlag.Play else ReadyFlag.NotReady))
            ready_to_play <- i % 2 = 1
        | Downstream.GAME_START ->
            if ready_to_play then
                this.Send(Upstream.BEGIN_PLAYING)
            else
                this.Send(Upstream.BEGIN_SPECTATING)
        | Downstream.LOBBY_SETTINGS s -> Logging.Info(sprintf "%i now in lobby: %A" i s)
        | Downstream.INVITED_TO_LOBBY(inviter, id) ->
            Logging.Info(sprintf "Client %i accepts invite from '%s'" i inviter)
            this.Send(Upstream.JOIN_LOBBY id)
        | Downstream.PLAYER_JOINED_LOBBY(user, _) ->
            match status with
            | InLobby n ->
                status <- InLobby(n + 1)

                if i = 0 && n + 1 = NUMBER_OF_CLIENTS then
                    this.Send(Upstream.LEAVE_LOBBY)
            | _ -> ()
        | Downstream.YOU_ARE_HOST true ->
            if i <> 0 && i <> NUMBER_OF_CLIENTS - 1 then
                this.Send(Upstream.LEAVE_LOBBY)
        | Downstream.YOU_LEFT_LOBBY ->
            Logging.Info(sprintf "%i left lobby" i)
            status <- LoggedIn
        | Downstream.SYSTEM_MESSAGE s -> Logging.Info(sprintf "@~> %i: %s" i s)
        | Downstream.PLAY_DATA("Percyqaz", ts, data) ->
            if ready_to_play then
                this.Send(Upstream.PLAY_DATA (ts, data))
        | _ -> ()

let clients = Array.init NUMBER_OF_CLIENTS TestClient

for c in clients do
    c.Connect()

Console.ReadLine() |> ignore
