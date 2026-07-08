// MyTweak Stream Deck plugin — classic HTML/JS SDK.
// Polls MyTweak's local API (127.0.0.1) for the current job's next ST/AoE action
// and burst state, renders the real game action icons on the keys, and toggles
// burst on press.

const ST = "com.mytweak.wrath.st";
const AOE = "com.mytweak.wrath.aoe";
const BURST = "com.mytweak.wrath.burst";

let ws = null;
let apiBase = null;                 // discovered MyTweak base URL, or null
const contexts = {};                // context id -> action UUID (visible keys)
const shown = {};                   // context id -> {title, icon} last sent

// ---- Stream Deck registration entrypoint (called by the Stream Deck app) ----
function connectElgatoStreamDeckSocket(inPort, inUUID, inRegisterEvent, inInfo) {
  ws = new WebSocket("ws://127.0.0.1:" + inPort);

  ws.onopen = () => {
    ws.send(JSON.stringify({ event: inRegisterEvent, uuid: inUUID }));
    findApi().then(loop);
  };

  ws.onmessage = (e) => {
    let msg;
    try { msg = JSON.parse(e.data); } catch (_) { return; }
    if (msg.event === "willAppear") { contexts[msg.context] = msg.action; delete shown[msg.context]; }
    else if (msg.event === "willDisappear") { delete contexts[msg.context]; delete shown[msg.context]; }
    else if (msg.event === "keyDown") onKeyDown(msg);
  };
}

function onKeyDown(msg) {
  if (msg.action === BURST && apiBase) {
    fetch(apiBase + "/burst/toggle", { cache: "no-store" }).catch(() => {});
  }
}

// ---- Stream Deck helpers (deduped: only send on change) ----
function show(context, title, icon) {
  if (!ws) return;
  const prev = shown[context] || {};
  if (prev.title !== title) {
    ws.send(JSON.stringify({ event: "setTitle", context, payload: { title: String(title), target: 0 } }));
  }
  if (prev.icon !== icon) {
    // icon is a data URL, or "" to restore the action's default image.
    ws.send(JSON.stringify({ event: "setImage", context, payload: { image: icon || "", target: 0 } }));
  }
  shown[context] = { title, icon };
}

// ---- Icon cache: iconId -> Promise<{url} | {err}> ----
// On failure the short err code is rendered on the key so problems are
// visible instead of silently falling back to text.
const iconCache = {};

function iconDataUrl(id) {
  if (!iconCache[id]) {
    iconCache[id] = (async () => {
      let r;
      try {
        r = await fetch(apiBase + "/icon/" + id);
      } catch (_) {
        delete iconCache[id]; // allow retry after transient failure
        return { err: "net" };
      }
      if (!r.ok) return { err: "s" + r.status };
      try {
        // Manual base64 instead of FileReader: guarantees a clean
        // "data:image/png;base64," prefix regardless of blob quirks.
        const bytes = new Uint8Array(await r.arrayBuffer());
        let bin = "";
        for (let i = 0; i < bytes.length; i += 0x8000)
          bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        return { url: "data:image/png;base64," + btoa(bin) };
      } catch (_) {
        return { err: "dec" };
      }
    })();
  }
  return iconCache[id];
}

// ---- MyTweak API discovery + polling ----
async function findApi() {
  for (let p = 9876; p <= 9885; p++) {
    try {
      const r = await fetch("http://127.0.0.1:" + p + "/state", { cache: "no-store" });
      if (r.ok) { apiBase = "http://127.0.0.1:" + p; return; }
    } catch (_) { /* port not listening */ }
  }
  apiBase = null;
}

async function renderAction(ctx, a) {
  if (!a || !a.has) { show(ctx, "—", ""); return; }
  if (a.icon > 0) {
    const res = await iconDataUrl(a.icon);
    if (res.url) { show(ctx, "", res.url); return; } // icon only, XIVDeck-style
    show(ctx, (a.name || "—") + "\n[" + res.err + "]", ""); // visible failure code
    return;
  }
  show(ctx, (a.name || "—") + "\n[i0]", "");  // state reported icon id 0
}

async function loop() {
  try {
    if (!apiBase) await findApi();

    if (apiBase) {
      const r = await fetch(apiBase + "/state", { cache: "no-store" });
      const d = await r.json();
      for (const ctx in contexts) {
        const action = contexts[ctx];
        if (action === ST) await renderAction(ctx, d.st);
        else if (action === AOE) await renderAction(ctx, d.aoe);
        else if (action === BURST) show(ctx, "Burst\n" + (d.burst || "—"), "");
      }
    } else {
      for (const ctx in contexts) show(ctx, "FFXIV\noffline", "");
    }
  } catch (_) {
    apiBase = null; // rediscover next tick
  }
  setTimeout(loop, 200);
}
