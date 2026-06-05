#!/usr/bin/env python3
"""
spotify_organizer.py
====================

Reorganize your Spotify library into clean per-genre and per-mood playlists.

What it does
------------
1. Reads every playlist you OWN and collects all unique tracks.
2. Looks up each track's artist genres and (if your app still has access)
   audio features, then sorts every track into:
       - a GENRE bucket  (Rock, Hip-Hop, Electronic, Pop, ...)
       - a MOOD  bucket  (Happy/Upbeat, Chill/Mellow, Intense, Sad, ...)
3. Creates fresh playlists named "Genre: Rock", "Mood: Chill", etc.
4. (Optional, separate, double-confirmed step) deletes the OLD playlists.

Safety
------
- Runs as a DRY RUN by default. It only reads and prints a plan.
- `--apply`       actually creates the new playlists.
- `--delete-old`  deletes (unfollows) your old playlists -- only allowed
                  together with --apply, and only after you type "DELETE".
- Your old playlists are listed to a backup file before anything is removed.

Setup
-----
1. Create an app at https://developer.spotify.com/dashboard
       - Add Redirect URI exactly:  http://127.0.0.1:8888/callback
       - Copy the Client ID and Client Secret.
2. pip install spotipy
3. Export your credentials (keep them out of the code / out of any chat):
       export SPOTIPY_CLIENT_ID=xxxxxxxx
       export SPOTIPY_CLIENT_SECRET=xxxxxxxx
       export SPOTIPY_REDIRECT_URI=http://127.0.0.1:8888/callback
4. Run:
       python3 spotify_organizer.py                 # dry run, shows the plan
       python3 spotify_organizer.py --apply         # create the new playlists
       python3 spotify_organizer.py --apply --delete-old   # also remove old ones

The first run opens a browser so you can log in and approve. A token is
cached in .spotify_cache so later runs don't prompt again.
"""

import argparse
import json
import os
import sys
import time
from collections import defaultdict
from datetime import datetime

try:
    import spotipy
    from spotipy.oauth2 import SpotifyOAuth
except ImportError:
    sys.exit("Missing dependency. Run:  pip install spotipy")

SCOPE = (
    "playlist-read-private "
    "playlist-read-collaborative "
    "playlist-modify-private "
    "playlist-modify-public "
    "user-library-read "          # Liked Songs
    "user-top-read "              # most-played tracks
    "user-read-recently-played"  # heavy-rotation recent plays
)

# --- New-playlist naming -----------------------------------------------------
GENRE_PREFIX = "Genre: "
MOOD_PREFIX = "Mood: "

# --- Genre bucketing ---------------------------------------------------------
# Spotify returns very granular artist genres ("melodic dubstep", "uk drill").
# We map any genre string containing a keyword to a broad bucket. First match
# in this ordered list wins.
GENRE_BUCKETS = [
    ("Hip-Hop / Rap", ["hip hop", "hip-hop", "rap", "drill", "trap", "grime"]),
    ("Electronic", ["house", "techno", "edm", "electro", "dubstep", "trance",
                    "dnb", "drum and bass", "garage", "synthwave", "ambient",
                    "idm", "downtempo", "electronica"]),
    ("R&B / Soul", ["r&b", "rnb", "soul", "funk", "neo soul", "motown"]),
    ("Metal", ["metal", "metalcore", "djent", "hardcore"]),
    ("Punk", ["punk", "emo", "screamo"]),
    ("Rock", ["rock", "grunge", "shoegaze", "britpop", "post-rock"]),
    ("Indie / Alt", ["indie", "alt ", "alternative", "bedroom pop", "dream pop"]),
    ("Jazz", ["jazz", "bebop", "swing"]),
    ("Classical", ["classical", "orchestra", "baroque", "opera", "romanticism"]),
    ("Country / Folk", ["country", "folk", "americana", "bluegrass", "singer-songwriter"]),
    ("Latin", ["latin", "reggaeton", "salsa", "bachata", "cumbia", "bossa"]),
    ("Reggae", ["reggae", "dancehall", "ska", "dub"]),
    ("Pop", ["pop"]),  # broad -- kept late so more specific buckets win first
]
GENRE_FALLBACK = "Other / Uncategorized"

# When audio-features is available, map (valence, energy) to a mood.
def mood_from_features(feat):
    """Classify mood from Spotify audio features. Returns a bucket name."""
    if not feat:
        return None
    valence = feat.get("valence")
    energy = feat.get("energy")
    dance = feat.get("danceability")
    if valence is None or energy is None:
        return None
    if dance is not None and dance >= 0.7 and energy >= 0.6:
        return "Party / Danceable"
    if valence >= 0.5 and energy >= 0.5:
        return "Happy / Upbeat"
    if valence < 0.5 and energy >= 0.6:
        return "Intense / Aggressive"
    if valence >= 0.5 and energy < 0.5:
        return "Chill / Mellow"
    return "Sad / Melancholy"

# Fallback when audio-features is unavailable: infer a coarse mood from genre.
GENRE_TO_MOOD = {
    "Hip-Hop / Rap": "Energetic / Hype",
    "Electronic": "Party / Danceable",
    "R&B / Soul": "Chill / Mellow",
    "Metal": "Intense / Aggressive",
    "Punk": "Intense / Aggressive",
    "Rock": "Energetic / Hype",
    "Indie / Alt": "Chill / Mellow",
    "Jazz": "Chill / Mellow",
    "Classical": "Calm / Focus",
    "Country / Folk": "Mellow / Reflective",
    "Latin": "Party / Danceable",
    "Reggae": "Chill / Mellow",
    "Pop": "Happy / Upbeat",
    GENRE_FALLBACK: "Mixed",
}


def bucket_for_genres(genres):
    """Return the broad genre bucket for a list of granular genre strings."""
    joined = " ".join(genres).lower()
    for bucket, keywords in GENRE_BUCKETS:
        for kw in keywords:
            if kw in joined:
                return bucket
    return GENRE_FALLBACK


def chunked(seq, n):
    for i in range(0, len(seq), n):
        yield seq[i:i + n]


def get_client():
    for var in ("SPOTIPY_CLIENT_ID", "SPOTIPY_CLIENT_SECRET", "SPOTIPY_REDIRECT_URI"):
        if not os.environ.get(var):
            sys.exit(f"Environment variable {var} is not set. See the setup notes at the top of this file.")
    auth = SpotifyOAuth(scope=SCOPE, cache_path=".spotify_cache", open_browser=True)
    return spotipy.Spotify(auth_manager=auth, requests_timeout=30, retries=5)


def fetch_owned_playlists(sp, me_id):
    playlists = []
    results = sp.current_user_playlists(limit=50)
    while results:
        for pl in results["items"]:
            if pl and pl["owner"]["id"] == me_id:
                playlists.append(pl)
        results = sp.next(results) if results.get("next") else None
    return playlists


def fetch_playlist_tracks(sp, playlist_id):
    """Yield (track_uri, primary_artist_ids) for every real track in a playlist."""
    fields = "items(track(uri,is_local,type,artists(id))),next"
    results = sp.playlist_items(playlist_id, fields=fields, additional_types=["track"], limit=100)
    while results:
        for item in results["items"]:
            t = item.get("track")
            if not t or t.get("is_local") or t.get("type") != "track" or not t.get("uri"):
                continue
            artist_ids = [a["id"] for a in t.get("artists", []) if a.get("id")]
            yield t["uri"], artist_ids
        results = sp.next(results) if results.get("next") else None


def fetch_liked_tracks(sp):
    """Yield (track_uri, primary_artist_ids) for every Liked Song."""
    results = sp.current_user_saved_tracks(limit=50)
    while results:
        for item in results["items"]:
            t = item.get("track")
            if not t or t.get("is_local") or not t.get("uri"):
                continue
            artist_ids = [a["id"] for a in t.get("artists", []) if a.get("id")]
            yield t["uri"], artist_ids
        results = sp.next(results) if results.get("next") else None


def fetch_most_played(sp):
    """Yield (track_uri, primary_artist_ids) for top + recently-played tracks."""
    seen = set()

    def emit(track):
        if not track or track.get("is_local") or not track.get("uri"):
            return
        if track["uri"] in seen:
            return
        seen.add(track["uri"])
        artist_ids = [a["id"] for a in track.get("artists", []) if a.get("id")]
        return track["uri"], artist_ids

    for term in ("short_term", "medium_term", "long_term"):
        results = sp.current_user_top_tracks(limit=50, time_range=term)
        for t in results.get("items", []):
            r = emit(t)
            if r:
                yield r

    recent = sp.current_user_recently_played(limit=50)
    for item in recent.get("items", []):
        r = emit(item.get("track"))
        if r:
            yield r


def fetch_artist_genres(sp, artist_ids):
    """Return {artist_id: [genres]} for all given artist ids (batched by 50)."""
    genres = {}
    ids = list(artist_ids)
    for batch in chunked(ids, 50):
        resp = sp.artists(batch)
        for art in resp.get("artists", []):
            if art:
                genres[art["id"]] = art.get("genres", [])
    return genres


def fetch_audio_features(sp, track_uris):
    """Return {track_uri: features}. Empty dict if the endpoint is unavailable."""
    track_ids = [u.split(":")[-1] for u in track_uris]
    features = {}
    try:
        for batch_ids, batch_uris in zip(chunked(track_ids, 100), chunked(track_uris, 100)):
            resp = sp.audio_features(batch_ids)
            for uri, feat in zip(batch_uris, resp):
                if feat:
                    features[uri] = feat
            time.sleep(0.1)
    except spotipy.SpotifyException as e:
        if e.http_status in (401, 403, 404):
            print(f"  audio-features unavailable for this app ({e.http_status}); "
                  "falling back to genre-based mood.")
            return {}
        raise
    return features


def main():
    ap = argparse.ArgumentParser(description="Reorganize Spotify playlists by genre and mood.")
    ap.add_argument("--apply", action="store_true", help="Actually create the new playlists.")
    ap.add_argument("--delete-old", action="store_true",
                    help="After creating, delete (unfollow) your old playlists. Requires --apply.")
    ap.add_argument("--public", action="store_true", help="Make new playlists public (default: private).")
    ap.add_argument("--min-tracks", type=int, default=3,
                    help="Skip creating a bucket playlist with fewer than this many tracks (default 3).")
    args = ap.parse_args()

    if args.delete_old and not args.apply:
        sys.exit("--delete-old requires --apply.")

    sp = get_client()
    me = sp.current_user()
    me_id = me["id"]
    print(f"Logged in as {me.get('display_name') or me_id} ({me_id})\n")

    print("Reading your playlists...")
    playlists = fetch_owned_playlists(sp, me_id)
    print(f"  Found {len(playlists)} playlists you own.\n")

    # Collect unique tracks and all artist ids.
    track_artists = {}        # uri -> [artist_ids]
    all_artist_ids = set()
    for pl in playlists:
        if pl["name"].startswith(GENRE_PREFIX) or pl["name"].startswith(MOOD_PREFIX):
            continue  # don't re-ingest playlists we made on a previous run
        print(f"  - {pl['name']} ({pl['tracks']['total']} tracks)")
        for uri, artist_ids in fetch_playlist_tracks(sp, pl["id"]):
            if uri not in track_artists:
                track_artists[uri] = artist_ids
                all_artist_ids.update(artist_ids)
    in_playlists = len(track_artists)

    print("\nReading your Liked Songs...")
    for uri, artist_ids in fetch_liked_tracks(sp):
        if uri not in track_artists:
            track_artists[uri] = artist_ids
            all_artist_ids.update(artist_ids)
    print(f"  Library now at {len(track_artists)} unique tracks "
          f"(+{len(track_artists) - in_playlists} new from Liked Songs).")
    after_liked = len(track_artists)

    print("Reading your most-played tracks (not already counted)...")
    for uri, artist_ids in fetch_most_played(sp):
        if uri not in track_artists:
            track_artists[uri] = artist_ids
            all_artist_ids.update(artist_ids)
    print(f"  +{len(track_artists) - after_liked} heavily-played tracks that "
          "weren't in any playlist or your Liked Songs.")

    print(f"\nCollected {len(track_artists)} unique tracks "
          f"from {len(all_artist_ids)} artists.\n")

    if not track_artists:
        sys.exit("No tracks found to organize.")

    print("Looking up artist genres...")
    artist_genres = fetch_artist_genres(sp, all_artist_ids)

    print("Looking up audio features (for mood)...")
    audio_features = fetch_audio_features(sp, list(track_artists.keys()))
    use_features = bool(audio_features)
    print(f"  Mood source: {'audio features' if use_features else 'genre inference'}\n")

    # Bucketize.
    genre_buckets = defaultdict(list)
    mood_buckets = defaultdict(list)
    for uri, artist_ids in track_artists.items():
        genres = []
        for aid in artist_ids:
            genres.extend(artist_genres.get(aid, []))
        gbucket = bucket_for_genres(genres)
        genre_buckets[gbucket].append(uri)

        if use_features:
            mbucket = mood_from_features(audio_features.get(uri)) or GENRE_TO_MOOD.get(gbucket, "Mixed")
        else:
            mbucket = GENRE_TO_MOOD.get(gbucket, "Mixed")
        mood_buckets[mbucket].append(uri)

    # Show the plan.
    def show(title, buckets):
        print(f"=== {title} ===")
        for name in sorted(buckets, key=lambda k: -len(buckets[k])):
            print(f"  {name:28s} {len(buckets[name])} tracks")
        print()
    show("GENRE PLAYLISTS", genre_buckets)
    show("MOOD PLAYLISTS", mood_buckets)

    if not args.apply:
        print("DRY RUN -- nothing was changed. Re-run with --apply to create these playlists.")
        return

    # Create playlists.
    public = args.public
    def create_bucket_playlists(prefix, buckets):
        for name in sorted(buckets):
            uris = buckets[name]
            if len(uris) < args.min_tracks:
                print(f"  skip '{prefix}{name}' (only {len(uris)} tracks)")
                continue
            pl = sp.user_playlist_create(
                me_id, f"{prefix}{name}", public=public,
                description="Auto-organized by spotify_organizer.py")
            for batch in chunked(uris, 100):
                sp.playlist_add_items(pl["id"], batch)
            print(f"  created '{prefix}{name}' with {len(uris)} tracks")

    print("Creating genre playlists...")
    create_bucket_playlists(GENRE_PREFIX, genre_buckets)
    print("Creating mood playlists...")
    create_bucket_playlists(MOOD_PREFIX, mood_buckets)
    print("\nDone creating playlists.\n")

    # Optionally delete old playlists -- backed up + double confirmed.
    if args.delete_old:
        backup = f"playlists_backup_{datetime.now():%Y%m%d_%H%M%S}.json"
        old = [{"name": p["name"], "id": p["id"], "tracks": p["tracks"]["total"]}
               for p in playlists
               if not (p["name"].startswith(GENRE_PREFIX) or p["name"].startswith(MOOD_PREFIX))]
        with open(backup, "w") as f:
            json.dump(old, f, indent=2)
        print(f"Backed up {len(old)} old playlists to {backup}")
        print("These playlists will be REMOVED (unfollowed) from your library:")
        for p in old:
            print(f"  - {p['name']} ({p['tracks']} tracks)")
        confirm = input('\nType DELETE to confirm removal of the old playlists: ')
        if confirm.strip() == "DELETE":
            for p in old:
                sp.current_user_unfollow_playlist(p["id"])
                print(f"  deleted '{p['name']}'")
            print("\nOld playlists removed.")
        else:
            print("Skipped deletion. Your old playlists are untouched.")


if __name__ == "__main__":
    main()
