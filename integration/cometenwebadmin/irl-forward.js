(() => {
  "use strict";

  const WS_URL = "ws://127.0.0.1:8081/";
  const ACTION_NAME = "Cometen IRL Notifications - Send";
  const RECONNECT_MS = 3000;

  const TYPE_MAP = {
    follow: "follow",
    sub: "sub",
    resub: "resub",
    gifted: "gifted",
    giftsub: "gifted",
    gift_sub: "gifted",
    gifted_sub: "gifted",
    giftbomb: "giftbomb",
    gift_bomb: "giftbomb",
    community_gift: "giftbomb",
    bits: "bits",
    cheer: "bits",
    donation: "donation",
    charity: "donation",
    raid: "raid",
    yt_sub: "youtubesub",
    youtube_sub: "youtubesub",
    youtubesub: "youtubesub"
  };

  const SOUND_MAP = {
    follow: "follow.wav",
    sub: "sub.wav",
    resub: "resub.wav",
    gifted: "gifted.wav",
    giftbomb: "giftbomb.wav",
    bits: "bits.wav",
    donation: "donation.wav",
    raid: "raid.wav",
    youtubesub: "sub.wav"
  };

  let socket = null;
  let reconnectTimer = null;

  function cleanType(value) {
    const key = String(value || "")
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .replace(/_alert$/, "");

    return TYPE_MAP[key] || "";
  }

  function scheduleReconnect() {
    if (reconnectTimer !== null) return;
    reconnectTimer = window.setTimeout(() => {
      reconnectTimer = null;
      connect();
    }, RECONNECT_MS);
  }

  function connect() {
    if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
      return;
    }

    try {
      socket = new WebSocket(WS_URL);
      socket.addEventListener("close", scheduleReconnect);
      socket.addEventListener("error", () => {
        try { socket.close(); } catch (_) {}
      });
    } catch (_) {
      scheduleReconnect();
    }
  }

  function forwardAlert(payload) {
    if (!payload || typeof payload !== "object") return;

    const eventType = cleanType(payload.alert || payload.alertType || payload.type);
    if (!eventType) return;

    if (!socket || socket.readyState !== WebSocket.OPEN) {
      connect();
      return;
    }

    const amount = payload.amount ?? payload.count ?? payload.viewers ?? payload.bits ?? payload.months ?? 0;
    const userName = payload.user ?? payload.userName ?? payload.displayName ?? "";
    const message = payload.message ?? payload.text ?? "";

    socket.send(JSON.stringify({
      request: "DoAction",
      id: `cometen-irl-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      action: { name: ACTION_NAME },
      args: {
        alertType: String(payload.alert || ""),
        eventType,
        userName: String(userName || ""),
        amount: String(amount || 0),
        message: String(message || ""),
        sound: SOUND_MAP[eventType] || "test.wav"
      }
    }));
  }

  function installHook() {
    const original = window.enqueueAlert;

    if (typeof original !== "function") {
      window.setTimeout(installHook, 250);
      return;
    }

    if (original.__cometenIrlWrapped === true) return;

    function wrappedEnqueueAlert(payload) {
      try {
        forwardAlert(payload);
      } catch (error) {
        console.warn("CometenIRL forwarding failed", error);
      }

      return original.apply(this, arguments);
    }

    wrappedEnqueueAlert.__cometenIrlWrapped = true;
    window.enqueueAlert = wrappedEnqueueAlert;
  }

  connect();
  installHook();
})();
