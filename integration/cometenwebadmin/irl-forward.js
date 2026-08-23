(function () {
  "use strict";

  function buildWebSocketUrl() {
    var params = new URLSearchParams(window.location.search);
    var savedHost = localStorage.getItem("cwa_ws_host");
    var savedPort = localStorage.getItem("cwa_ws_port");
    var host =
      params.get("host") ||
      savedHost ||
      ((window.location.protocol === "http:" || window.location.protocol === "https:")
        ? window.location.hostname
        : "127.0.0.1");
    var port = params.get("port") || savedPort || "8081";
    return "ws://" + host + ":" + port + "/";
  }

  var WS_URL = buildWebSocketUrl();
  var ACTION_NAME = "Cometen IRL Notifications - Send";
  var RECONNECT_MS = 3000;

  var TYPE_MAP = {
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

  var SETTINGS_KEY_MAP = {
    follow: "follow",
    sub: "sub",
    resub: "resub",
    gifted: "gifted",
    giftbomb: "giftbomb",
    bits: "bits",
    donation: "charity",
    raid: "raid",
    youtubesub: "yt_sub"
  };

  var SOUND_MAP = {
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

  var socket = null;
  var reconnectTimer = null;
  var pending = [];

  function hasValue(obj, key) {
    return obj &&
      Object.prototype.hasOwnProperty.call(obj, key) &&
      obj[key] !== undefined &&
      obj[key] !== null &&
      String(obj[key]).trim() !== "";
  }

  function firstValue(obj, keys, fallback) {
    var i;
    for (i = 0; i < keys.length; i += 1) {
      if (hasValue(obj, keys[i])) return obj[keys[i]];
    }
    return fallback;
  }

  function cleanType(value) {
    var key = String(value || "")
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .replace(/_alert$/, "");

    return TYPE_MAP[key] || "";
  }

  function currentIrlSettings() {
    try {
      if (typeof adminSettings !== "undefined" &&
          adminSettings &&
          typeof adminSettings === "object") {
        return adminSettings.irl || null;
      }
    } catch (error) {}

    return null;
  }

  function irlAlertIsEnabled(eventType) {
    var settings = currentIrlSettings();
    var typeSettings;
    var settingsKey;

    if (!settings) return true;
    if (settings.enabled === false) return false;

    typeSettings =
      settings.alerts && typeof settings.alerts === "object"
        ? settings.alerts
        : {};

    settingsKey = SETTINGS_KEY_MAP[eventType] || eventType;
    return typeSettings[settingsKey] !== false;
  }

  function scheduleReconnect() {
    if (reconnectTimer !== null) return;

    reconnectTimer = window.setTimeout(function () {
      reconnectTimer = null;
      connect();
    }, RECONNECT_MS);
  }

  function flushPending() {
    while (socket &&
           socket.readyState === WebSocket.OPEN &&
           pending.length > 0) {
      try {
        socket.send(JSON.stringify(pending.shift()));
      } catch (error) {
        break;
      }
    }
  }

  function connect() {
    if (socket &&
        (socket.readyState === WebSocket.OPEN ||
         socket.readyState === WebSocket.CONNECTING)) {
      return;
    }

    try {
      socket = new WebSocket(WS_URL);

      socket.addEventListener("open", function () {
        console.log("[CometenIRL] legacy forward connected");
        flushPending();
      });

      socket.addEventListener("close", scheduleReconnect);

      socket.addEventListener("error", function () {
        try {
          socket.close();
        } catch (error) {}
      });
    } catch (error) {
      scheduleReconnect();
    }
  }

  function sendRequest(request) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      try {
        socket.send(JSON.stringify(request));
        return;
      } catch (error) {}
    }

    pending.push(request);
    connect();
  }

  function forwardAlert(payload) {
    var eventType;
    var amount;
    var userName;
    var message;

    if (!payload || typeof payload !== "object") return;

    eventType = cleanType(
      firstValue(payload, ["alert", "alertType", "type"], "")
    );

    if (!eventType || !irlAlertIsEnabled(eventType)) return;

    amount = firstValue(
      payload,
      ["amount", "count", "viewers", "bits", "months"],
      0
    );

    userName = firstValue(
      payload,
      ["user", "userName", "displayName"],
      ""
    );

    message = firstValue(
      payload,
      ["message", "text"],
      ""
    );

    sendRequest({
      request: "DoAction",
      id: "cometen-irl-" + Date.now() + "-" +
          Math.random().toString(16).slice(2),
      action: { name: ACTION_NAME },
      args: {
        alertType: String(firstValue(payload, ["alert"], "")),
        eventType: String(eventType),
        userName: String(userName || ""),
        amount: String(amount || 0),
        message: String(message || ""),
        sound: SOUND_MAP[eventType] || "test.wav"
      }
    });
  }

  function installHook() {
    var original = window.enqueueAlert;

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

    console.log("[CometenIRL] legacy hook installed");
  }

  connect();
  installHook();
}());
