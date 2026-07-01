# MyTweak — Stream Deck plugin

Shows your FFXIV **next Single-Target action**, **next AoE action**, and **burst
state (ARMED / HELD)** on your Stream Deck, and lets you **toggle burst** with a
key press. Everything lives on the deck, so it never shows up in your game
capture / stream.

It talks to the MyTweak game plugin's local API on `127.0.0.1` (loopback only).

---

## Requirements

- **MyTweak game plugin v1.0.5.31 or newer** (the version that added the local API).
- **Stream Deck software 6.0+**.

## Install

1. Make sure the MyTweak game plugin is updated to **1.0.5.31+** and you're logged
   into a character.
2. **Quit the Stream Deck app** (right-click the tray icon → Quit).
3. Copy the folder **`com.mytweak.wrath.sdPlugin`** into your Stream Deck plugins
   folder:
   ```
   %appdata%\Elgato\StreamDeck\Plugins\
   ```
   (paste `%appdata%\Elgato\StreamDeck\Plugins\` into the Explorer address bar).
   The result should be:
   `%appdata%\Elgato\StreamDeck\Plugins\com.mytweak.wrath.sdPlugin\manifest.json`
4. **Start the Stream Deck app** again.
5. In the Stream Deck app you'll now have a **"MyTweak"** category with three
   actions — drag them onto keys:
   - **Next ST Action**
   - **Next AoE Action**
   - **Burst Toggle** (press to toggle burst; the key shows ARMED / HELD)

## Using it

- Log into a character. The keys update a few times per second.
- If a key shows **"FFXIV offline"**, the game plugin isn't reachable yet — make
  sure you're logged in and MyTweak is running. The plugin auto-finds MyTweak on
  ports 9876–9885.
- The **Burst** key toggles `/mytweak burst` (hold ↔ resume) when pressed.

## Notes / current limitations (v1)

- **Text only for now** — the keys show the action *name* and burst state as
  text. Action *icons* are a planned follow-up.
- Loopback only: the API is bound to `127.0.0.1` and isn't reachable from your
  network.
- If you run multiple game clients, each MyTweak grabs the next free port in
  9876–9885; the plugin connects to the first one it finds.

## Troubleshooting

- **Nothing shows / "offline":** confirm you're logged in, and that MyTweak is
  the 1.0.5.31+ build. In-game, `/xllog` should have a line like
  `[MyTweak] Stream Deck bridge on http://127.0.0.1:9876`.
- **Actions don't appear in Stream Deck:** double-check the folder is named
  exactly `com.mytweak.wrath.sdPlugin` and sits directly in the `Plugins` folder,
  then fully restart the Stream Deck app.
