//! The client's voice session: the handshake, the keep-alive clock, and the
//! mic and voice streams, driven one call at a time by the mod's mixins.

use std::collections::HashMap;
use std::net::{SocketAddr, UdpSocket};
use std::time::{Duration, Instant};

use crate::protocol::{client_datagram, server_datagram, Cipher, FromServer, ToServer};

pub const KEEP_ALIVE_EVERY: Duration = Duration::from_millis(1000);
/// Without server traffic for this long the connection is dead.
pub const TIMEOUT_AFTER: Duration = Duration::from_millis(15_000);

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum State {
    Idle,
    /// The secret request went out over the mod channel.
    RequestingSecret,
    /// Authenticate sent, waiting for the ack.
    Connecting,
    Connected,
}

#[derive(Debug, Clone, PartialEq)]
pub enum Event {
    Connected,
    /// The UDP connection dropped; the mixin re-requests the secret to reconnect.
    Dropped(&'static str),
    /// A voice packet, Opus-encoded; the audio layer decodes it. Volume is 0..=1.
    Voice {
        sender: u128,
        group: bool,
        whispering: bool,
        data: Vec<u8>,
        volume: f32,
    },
}

pub struct Session {
    player: u128,
    rng: ring::rand::SystemRandom,
    cipher: Option<Cipher>,
    socket: Option<UdpSocket>,
    server: Option<SocketAddr>,
    state: State,
    mic_seq: i64,
    muted: bool,
    whispering: bool,
    listener: [f64; 3],
    positions: HashMap<u128, [f64; 3]>,
    max_distance: f64,
    pub keep_alive_every: Duration,
    pub timeout_after: Duration,
    last_keep_alive: Instant,
    last_seen: Instant,
    events: Vec<Event>,
    udp_tx_count: u64,
    udp_rx_count: u64,
    udp_invalid_count: u64,
    last_udp_error: Option<String>,
}

impl Session {
    pub fn new(player: u128) -> Self {
        Self {
            player,
            rng: ring::rand::SystemRandom::new(),
            cipher: None,
            socket: None,
            server: None,
            state: State::Idle,
            mic_seq: 0,
            muted: false,
            whispering: false,
            listener: [0.0; 3],
            positions: HashMap::new(),
            max_distance: 48.0,
            keep_alive_every: KEEP_ALIVE_EVERY,
            timeout_after: TIMEOUT_AFTER,
            last_keep_alive: Instant::now(),
            last_seen: Instant::now(),
            events: Vec::new(),
            udp_tx_count: 0,
            udp_rx_count: 0,
            udp_invalid_count: 0,
            last_udp_error: None,
        }
    }

    pub fn state(&self) -> State {
        self.state
    }
    pub fn udp_counts(&self) -> (u64, u64, u64) {
        (self.udp_tx_count, self.udp_rx_count, self.udp_invalid_count)
    }
    pub fn last_udp_error(&self) -> Option<&str> {
        self.last_udp_error.as_deref()
    }
    pub fn muted(&self) -> bool {
        self.muted
    }
    pub fn set_muted(&mut self, muted: bool) {
        self.muted = muted;
    }
    pub fn set_whispering(&mut self, whispering: bool) {
        self.whispering = whispering;
    }
    pub fn set_listener(&mut self, pos: [f64; 3]) {
        self.listener = pos;
    }
    pub fn set_position(&mut self, who: u128, pos: [f64; 3]) {
        self.positions.insert(who, pos);
    }
    pub fn set_positions(
        &mut self,
        listener: [f64; 3],
        positions: impl IntoIterator<Item = (u128, [f64; 3])>,
    ) {
        self.listener = listener;
        self.positions.clear();
        self.positions.extend(positions);
    }
    pub fn forget_position(&mut self, who: u128) {
        self.positions.remove(&who);
    }
    pub fn set_max_distance(&mut self, d: f64) {
        self.max_distance = d.max(1.0);
    }

    /// The mixin sends this over the mod channel; the reply comes to on_secret.
    pub fn request_secret(&mut self) {
        self.state = State::RequestingSecret;
    }

    /// The server's secret reply arrived; open the UDP socket and authenticate.
    pub fn on_secret(&mut self, secret: [u8; 16], server: SocketAddr) -> std::io::Result<()> {
        let socket = UdpSocket::bind(if server.is_ipv4() { "0.0.0.0:0" } else { "[::]:0" })?;
        socket.set_nonblocking(true)?;
        self.socket = Some(socket);
        self.cipher = Some(Cipher::new(&secret));
        self.server = Some(server);
        self.send(&ToServer::Authenticate { player: self.player, secret });
        self.state = State::Connecting;
        self.last_seen = Instant::now();
        Ok(())
    }

    /// Left the world: drop the socket and everything tied to it.
    pub fn close(&mut self) {
        self.socket = None;
        self.cipher = None;
        self.server = None;
        self.state = State::Idle;
        self.events.clear();
    }

    fn send(&mut self, packet: &ToServer) {
        let (Some(cipher), Some(server)) = (self.cipher.as_ref(), self.server) else {
            return;
        };
        let Some(socket) = &self.socket else { return };
        let Some(sealed) = cipher.seal(&self.rng, &packet.write()) else {
            return;
        };
        let datagram = client_datagram(self.player, &sealed);
        match socket.send_to(&datagram, server) {
            Ok(_) => self.udp_tx_count = self.udp_tx_count.saturating_add(1),
            Err(error) => self.last_udp_error = Some(format!("UDP send_to: {error}")),
        }
    }

    /// One 20 ms Opus frame from the mic; an empty frame stops the stream.
    pub fn push_mic(&mut self, frame: &[u8]) {
        if self.state != State::Connected || self.muted {
            return;
        }
        self.mic_seq += 1;
        self.send(&ToServer::Mic {
            data: frame.to_vec(),
            sequence: self.mic_seq,
            whispering: self.whispering,
        });
    }

    /// The keep-alive and ping clocks; call once per frame while connected.
    pub fn tick(&mut self) {
        if self.state == State::Connecting {
            if self.last_keep_alive.elapsed() >= self.keep_alive_every {
                self.last_keep_alive = Instant::now();
                self.send(&ToServer::ConnectionCheck);
            }
            return;
        }
        if self.state != State::Connected {
            return;
        }
    }

    /// Read what arrived and return this frame's events; call once per frame.
    pub fn pump(&mut self) -> Vec<Event> {
        let mut datagrams = Vec::new();
        if let Some(socket) = &self.socket {
            let mut buf = [0u8; 4096];
            loop {
                match socket.recv_from(&mut buf) {
                    Ok((n, _)) => datagrams.push(buf[..n].to_vec()),
                    Err(_) => break,
                }
            }
        }
        for d in &datagrams {
            self.udp_rx_count = self.udp_rx_count.saturating_add(1);
            self.ingest(d);
        }
        if self.state == State::Connecting && self.last_seen.elapsed() >= self.timeout_after {
            self.state = State::Idle;
            self.last_udp_error = Some("timeout esperando AuthenticateAck".into());
            self.events.push(Event::Dropped("authenticate timeout"));
        }
        if self.state == State::Connected && self.last_seen.elapsed() >= self.timeout_after {
            self.state = State::Idle;
            self.events.push(Event::Dropped("timeout"));
        }
        std::mem::take(&mut self.events)
    }

    fn ingest(&mut self, datagram: &[u8]) {
        let plain = {
            let Some(cipher) = self.cipher.as_ref() else { return };
            let sealed = match server_datagram(datagram) {
                Ok(s) => s,
                Err(_) => {
                    self.udp_invalid_count = self.udp_invalid_count.saturating_add(1);
                    self.last_udp_error = Some("datagrama UDP con cabecera invalida".into());
                    return;
                }
            };
            match cipher.open(sealed) {
                Some(p) => p,
                None => {
                    self.udp_invalid_count = self.udp_invalid_count.saturating_add(1);
                    self.last_udp_error = Some("fallo de autenticacion AES-GCM en UDP".into());
                    return;
                }
            }
        };
        let packet = match FromServer::read(&plain) {
            Ok(p) => p,
            Err(_) => {
                self.udp_invalid_count = self.udp_invalid_count.saturating_add(1);
                self.last_udp_error = Some("paquete UDP no reconocido".into());
                return;
            }
        };
        self.last_seen = Instant::now();
        match packet {
            FromServer::AuthenticateAck => {
                if self.state == State::Connecting {
                    self.last_keep_alive = Instant::now() - self.keep_alive_every;
                }
            }
            FromServer::ConnectionCheckAck => {
                if self.state == State::Connecting {
                    self.state = State::Connected;
                    self.events.push(Event::Connected);
                }
            }
            FromServer::KeepAlive => self.send(&ToServer::KeepAlive),
            FromServer::Ping { id, timestamp } => {
                self.send(&ToServer::Ping { id, timestamp });
            }
            FromServer::Positional {
                sender,
                data,
                whispering,
                ..
            } => {
                let d = self
                    .positions
                    .get(&sender)
                    .map(|p| dist(p, &self.listener))
                    .unwrap_or(f64::INFINITY);
                let volume = volume(d, self.max_distance);
                if volume > 0.0 {
                    self.events.push(Event::Voice {
                        sender,
                        group: false,
                        whispering,
                        data: data.to_vec(),
                        volume,
                    });
                }
            }
            FromServer::Group { sender, data, .. } => self.events.push(Event::Voice {
                sender,
                group: true,
                whispering: false,
                data: data.to_vec(),
                volume: 1.0,
            }),
        }
    }
}

fn dist(a: &[f64; 3], b: &[f64; 3]) -> f64 {
    ((a[0] - b[0]).powi(2) + (a[1] - b[1]).powi(2) + (a[2] - b[2]).powi(2)).sqrt()
}

/// Linear falloff over the server's advertised max distance.
fn volume(d: f64, max: f64) -> f32 {
    (1.0 - d / max).clamp(0.0, 1.0) as f32
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::{In, MAGIC, MAX_BODY, Out};

    /// A stand-in relay that speaks the same wire: sealed packets in and out.
    struct MockServer {
        socket: UdpSocket,
        cipher: Cipher,
        rng: ring::rand::SystemRandom,
        from: Option<SocketAddr>,
    }

    impl MockServer {
        fn bind(secret: [u8; 16]) -> (Self, SocketAddr) {
            let socket = UdpSocket::bind("127.0.0.1:0").unwrap();
            socket
                .set_read_timeout(Some(Duration::from_secs(2)))
                .unwrap();
            let addr = socket.local_addr().unwrap();
            (
                Self {
                    socket,
                    cipher: Cipher::new(&secret),
                    rng: ring::rand::SystemRandom::new(),
                    from: None,
                },
                addr,
            )
        }
        fn recv_packet(&mut self) -> ToServer {
            let mut buf = [0u8; 4096];
            let (n, from) = self.socket.recv_from(&mut buf).unwrap();
            self.from = Some(from);
            let mut r = In::new(&buf[..n]);
            assert_eq!(r.byte().unwrap(), MAGIC);
            let player = r.uuid().unwrap();
            assert_eq!(player, 11);
            let sealed = r.bytes(MAX_BODY).unwrap();
            let plain = self.cipher.open(sealed).unwrap();
            match ToServer::read(&plain) {
                Ok(p) => p,
                Err(e) => panic!("the mock server could not read a packet: {e:?}"),
            }
        }
        fn send_packet(&self, packet: &FromServer) {
            let sealed = self.cipher.seal(&self.rng, &packet.write()).unwrap();
            let datagram = Out::default().byte(MAGIC).bytes(&sealed).done();
            self.socket
                .send_to(&datagram, self.from.unwrap())
                .unwrap();
        }
    }

    #[test]
    fn refreshed_positions_drop_departed_players() {
        let mut session = Session::new(1);
        session.set_positions([1.0, 2.0, 3.0], [(2, [4.0, 5.0, 6.0])]);
        assert_eq!(session.positions.len(), 1);
        session.set_positions([2.0, 3.0, 4.0], []);
        assert!(session.positions.is_empty());
        assert_eq!(session.listener, [2.0, 3.0, 4.0]);
    }

    #[test]
    fn handshake_keep_alive_mic_and_voice() {
        let secret = [7u8; 16];
        let player = 11u128;
        let (mut srv, addr) = MockServer::bind(secret);
        let mut s = Session::new(player);
        s.request_secret();
        assert_eq!(s.state(), State::RequestingSecret);
        s.on_secret(secret, addr).unwrap();
        assert_eq!(s.state(), State::Connecting);

        // The server reads the sealed Authenticate and answers with the ack.
        match srv.recv_packet() {
            ToServer::Authenticate { player: p, secret: got } => {
                assert_eq!(p, player);
                assert_eq!(got, secret);
            }
            other => panic!("expected Authenticate, got {other:?}"),
        }
        srv.send_packet(&FromServer::AuthenticateAck);
        assert!(s.pump().is_empty());
        assert_eq!(s.state(), State::Connecting);
        s.tick();
        assert!(matches!(srv.recv_packet(), ToServer::ConnectionCheck));
        srv.send_packet(&FromServer::ConnectionCheckAck);
        assert_eq!(s.pump(), vec![Event::Connected]);
        assert_eq!(s.state(), State::Connected);

        srv.send_packet(&FromServer::KeepAlive);
        assert!(s.pump().is_empty());
        assert!(matches!(srv.recv_packet(), ToServer::KeepAlive));
        srv.send_packet(&FromServer::Ping { id: 33, timestamp: 44 });
        assert!(s.pump().is_empty());
        assert!(matches!(
            srv.recv_packet(),
            ToServer::Ping { id: 33, timestamp: 44 }
        ));

        // The client answers server keep-alive and ping packets above; it does not send them periodically.

        // The mic goes out sealed, sequence starting at one.
        s.push_mic(&[1, 2, 3]);
        match srv.recv_packet() {
            ToServer::Mic { data, sequence, whispering } => {
                assert_eq!(data, vec![1, 2, 3]);
                assert_eq!(sequence, 1);
                assert!(!whispering);
            }
            other => panic!("expected Mic, got {other:?}"),
        }

        // A nearby player is heard; a far one is not.
        s.set_position(22, [3.0, 4.0, 0.0]);
        srv.send_packet(&FromServer::Positional {
            sender: 22,
            data: &[9, 9],
            sequence: 5,
            distance: 4.0,
            whispering: false,
        });
        let events = s.pump();
        match events.as_slice() {
            [Event::Voice { sender, group, whispering, data, volume }] => {
                assert_eq!(*sender, 22);
                assert!(!*group);
                assert!(!*whispering);
                assert_eq!(data, &vec![9, 9]);
                let want = (1.0 - 5.0_f64 / 48.0) as f32;
                assert!((volume - want).abs() < 1e-5, "{volume} vs {want}");
            }
            other => panic!("expected one Voice event, got {other:?}"),
        }
        s.set_position(22, [1000.0, 0.0, 0.0]);
        srv.send_packet(&FromServer::Positional {
            sender: 22,
            data: &[9, 9],
            sequence: 6,
            distance: 4.0,
            whispering: false,
        });
        assert!(s.pump().is_empty(), "a far player's voice is dropped");

        // A datagram sealed with the wrong key is dropped without a crash.
        let wrong = Cipher::new(&[8u8; 16]);
        let sealed = wrong.seal(&ring::rand::SystemRandom::new(), &[1, 2, 3]).unwrap();
        let bad = Out::default().byte(MAGIC).bytes(&sealed).done();
        srv.socket.send_to(&bad, srv.from.unwrap()).unwrap();
        assert!(s.pump().is_empty());
        assert_eq!(s.state(), State::Connected);

        // Muting silences the mic.
        s.set_muted(true);
        s.push_mic(&[4, 5, 6]);
        // (No packet follows; nothing to read on the server within the timeout.)
    }
}
