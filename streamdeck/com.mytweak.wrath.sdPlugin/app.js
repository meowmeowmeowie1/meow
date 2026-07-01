// MyTweak (Wrath) Stream Deck plugin — classic HTML/JS SDK.
// Polls MyTweak's local API (127.0.0.1) for the current job's next ST/AoE action
// and burst state, shows them on the keys, and toggles burst on press.
//
// v1 is text-only (action names + ARMED/HELD as key titles). Icons come later.

const ST = "com.mytweak.wrath.st";
const AOE = "com.mytweak.wrath.aoe";
const BURST = "com.mytweak.wrath.burst";

let ws = null;
let apiBase = null;                 // discovered MyTweak base URL, or null
const contexts = {};                // context id -> action UUID (currently visible keys)

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
    if (msg.event === "willAppear") contexts[msg.context] = msg.action;
    else if (msg.event === "willDisappear") delete contexts[msg.context];
    else if (msg.event === "keyDown") onKeyDown(msg);
  };
}

function onKeyDown(msg) {
  if (msg.action === BURST && apiBase) {
    fetch(apiBase + "/burst/toggle", { cache: "no-store" }).catch(() => {});
  }
}

// ---- Stream Deck helpers ----
function setTitle(context, title) {
  if (!ws) return;
  ws.send(JSON.stringify({
    event: "setTitle",
    context: context,
    payload: { title: String(title), target: 0 }
  }));
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

function actionTitle(a) {
  if (!a || !a.has) return "—";
  return a.name || "—";
}

async function loop() {
  try {
    if (!apiBase) await findApi();

    if (apiBase) {
      const r = await fetch(apiBase + "/state", { cache: "no-store" });
      const d = await r.json();
      for (const ctx in contexts) {
        const action = contexts[ctx];
        if (action === ST) setTitle(ctx, actionTitle(d.st));
        else if (action === AOE) setTitle(ctx, actionTitle(d.aoe));
        else if (action === BURST) setTitle(ctx, "Burst\n" + (d.burst || "—"));
      }
    } else {
      for (const ctx in contexts) setTitle(ctx, "FFXIV\noffline");
    }
  } catch (_) {
    apiBase = null; // rediscover next tick
  }
  setTimeout(loop, 200);
}
