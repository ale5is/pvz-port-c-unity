// Game-side module for the Simple Voice Chat port. Thin shell over the
// svc_client bundle crate: protocol, session and audio live there; this
// module bridges Horizon's hooks to it and queues outbound packets for
// the net send path.

use std::sync::Mutex;
use std::time::Instant;

pub use svc_client::runtime::{state, Glue};

static OUT: Mutex<Vec<(String, Vec<u8>)>> = Mutex::new(Vec::new());

fn locked() -> std::sync::MutexGuard<'static, Glue> {
    state().lock().unwrap_or_else(|e| e.into_inner())
}

pub fn queue_outbound(channel: String, body: Vec<u8>) {
    OUT.lock()
        .unwrap_or_else(|e| e.into_inner())
        .push((channel, body));
}

pub fn drain_outbound() -> Vec<(String, Vec<u8>)> {
    std::mem::take(&mut *OUT.lock().unwrap_or_else(|e| e.into_inner()))
}

pub fn world_open_ready() {
    let mut g = locked();
    g.world_open();
    let (channel, body) = Glue::handshake();
    queue_outbound(channel, body);
    for note in g.start_audio() {
        eprintln!("{note}");
    }
}

pub fn world_closed() {
    locked().world_close();
}

fn server_host(address: &str) -> Option<String> {
    let address = address.trim();
    if let Ok(socket) = address.parse::<std::net::SocketAddr>() {
        return Some(socket.ip().to_string());
    }
    if let Some(bracketed) = address.strip_prefix('[')
        && let Some(end) = bracketed.find(']')
    {
        return Some(bracketed[..end].to_string());
    }
    if let Some((host, port)) = address.rsplit_once(':')
        && !host.contains(':')
        && port.parse::<u16>().is_ok()
    {
        return Some(host.to_string());
    }
    Some(address.to_string())
}

pub fn on_net_from_game(
    game: &crate::game::Game<'_>,
    channel: &str,
    data: &[u8],
) {
    let hint = game
        .remote
        .as_ref()
        .and_then(|remote| server_host(&remote.session.address));
    locked().on_net(channel, data, hint.as_deref(), Instant::now());
}

static REMOTE_ACTIVE: std::sync::atomic::AtomicBool =
    std::sync::atomic::AtomicBool::new(false);

pub fn on_frame(game: &mut crate::game::Game<'_>, _elapsed: std::time::Duration) {
    let connected = game
        .remote
        .as_ref()
        .is_some_and(|remote| remote.logged_in && remote.reconfiguring.is_none());
    let was_connected = REMOTE_ACTIVE.swap(connected, std::sync::atomic::Ordering::Relaxed);

    if connected && !was_connected {
        world_open_ready();
    } else if !connected && was_connected {
        world_closed();
    }

    let reconnect = {
        let mut g = locked();
        let _ = g.on_frame(Instant::now());
        std::mem::take(&mut g.reconnect_pending)
    };
    if connected && reconnect {
        let (channel, body) = Glue::handshake();
        queue_outbound(channel, body);
    }
}

pub fn on_command(command: &str, ran: &mut bool) {
    let head = command.split_whitespace().next().unwrap_or("");
    if head != "svc" && head != "vcgroup" {
        return;
    }
    let (feedback, outbound) = locked().on_command(command);
    if !feedback.is_empty() {
        eprintln!("{feedback}");
    }
    for (channel, body) in outbound {
        queue_outbound(channel, body);
    }
    *ran = true;
}

