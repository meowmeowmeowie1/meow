#!/usr/bin/env bash
#
# tools/sync-upstream.sh — pull upstream WrathCombo combat-content updates
# into the MyTweak fork without a full-history merge.
#
# Design: the fork was imported as a single squashed commit (no shared git
# history with upstream), auto-rotation was deleted wholesale, and nearly
# every combo file was edited, so "git merge upstream/main" would drown in
# modify/delete conflicts. Instead we keep a state file (.upstream-base)
# recording the last upstream commit we synced from, and on each run apply
#
#     git diff <last-synced>..upstream/main -- <combat paths>
#
# via "git apply --3way", scoped to the paths where FFXIV-patch rotation
# updates actually land. Identity files and fork-only subsystems are never
# touched. Conflicts are left as normal merge markers for manual resolution;
# the state file only advances after a fully clean apply.
#
# Usage:
#   tools/sync-upstream.sh            # fetch upstream and apply in-scope diff
#   tools/sync-upstream.sh --dry-run  # fetch and show diffstat only
#
set -u -o pipefail

# ---------------------------------------------------------------- constants
UPSTREAM_REMOTE="upstream"
UPSTREAM_URL="https://github.com/PunishXIV/WrathCombo.git"
UPSTREAM_BRANCH="main"
STATE_FILE=".upstream-base"

# Verified import baseline: upstream commit whose tree matches the squashed
# import ee7fcf1a9 on every file the fork did not touch (between upstream
# releases 1.0.4.7 and 1.0.4.8). Used only when $STATE_FILE does not exist.
SEED_BASE="2dc4f708baf8bba73359ddca40ea628f292647bb"

# Where upstream combat/rotation updates land. Everything outside these
# paths is ignored by the sync on purpose. CustomCombo/ and Extensions/ are
# included because upstream's combo updates routinely call new framework
# helpers added there in the same release; excluding them trades a few
# conflicts for post-sync build errors.
SYNC_PATHS=(
    "WrathCombo/Combos"
    "WrathCombo/Data"
    "WrathCombo/CustomCombo"
    "WrathCombo/Extensions"
    "res"
)

# Belt-and-braces excludes. Most of these live outside SYNC_PATHS already,
# but they guarantee that widening SYNC_PATHS later can never clobber the
# fork's identity or fork-only subsystems.
PROTECTED=(
    ":(exclude)WrathCombo/WrathCombo.csproj"
    ":(exclude)pluginmaster.json"
    ":(exclude)WrathCombo/WrathCombo.json"
    ":(exclude)WrathCombo/QoLTweaksKBM.json"
    ":(exclude)WrathCombo/MyTweak.json"
    ":(exclude)WrathCombo/Tweaks"
    ":(exclude)WrathCombo/Services/StreamDeckBridge.cs"
    ":(exclude)WrathCombo/Core/ActionResolution.cs"
    ":(exclude)WrathCombo/Window"
    ":(exclude)streamdeck"
)

# ------------------------------------------------------------------ helpers
log()  { printf '%s\n' "$*"; }
err()  { printf 'sync-upstream: %s\n' "$*" >&2; }
die()  { err "$@"; exit 1; }

usage() {
    sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
}

# ------------------------------------------------------------------- set-up
DRY_RUN=0
for arg in "$@"; do
    case "$arg" in
        -n|--dry-run) DRY_RUN=1 ;;
        -h|--help)    usage ;;
        *)            die "unknown argument: $arg (try --help)" ;;
    esac
done

ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel 2>/dev/null)" \
    || die "not inside a git repository"
cd "$ROOT" || die "cannot cd to repo root $ROOT"

# Refuse to run on top of an unfinished sync/merge.
if [ -n "$(git ls-files -u)" ]; then
    err "the index has unmerged (conflicted) entries — finish the previous"
    err "sync first:"
    err "  1. resolve the <<<<<<< / >>>>>>> markers in the listed files"
    err "  2. git add <each resolved file>"
    err "  3. if this was a sync run, record the base it targeted:"
    err "       echo <upstream-sha> > $STATE_FILE   (see message from that run)"
    err "  4. git commit"
    err "or throw the attempt away with: git reset --merge"
    exit 1
fi

# Require the sync scope to be clean so conflict markers land on committed
# content and a bad run can be reverted with git reset/checkout.
if [ "$DRY_RUN" -eq 0 ] && \
   [ -n "$(git status --porcelain --untracked-files=no -- "${SYNC_PATHS[@]}" "$STATE_FILE" 2>/dev/null)" ]; then
    die "uncommitted changes under ${SYNC_PATHS[*]} (or $STATE_FILE) — commit or stash them first"
fi

# ------------------------------------------------------- remote + fetch
if ! git remote get-url "$UPSTREAM_REMOTE" >/dev/null 2>&1; then
    log "Adding remote '$UPSTREAM_REMOTE' -> $UPSTREAM_URL"
    git remote add "$UPSTREAM_REMOTE" "$UPSTREAM_URL" \
        || die "failed to add remote $UPSTREAM_REMOTE"
fi

log "Fetching $UPSTREAM_REMOTE/$UPSTREAM_BRANCH ..."
# Prefer a blobless fetch (file contents download lazily, only when the
# diff/apply below actually needs them). Fall back to a plain fetch if the
# server or git version refuses the filter.
if ! git fetch --no-tags --filter=blob:none "$UPSTREAM_REMOTE" "$UPSTREAM_BRANCH" 2>/dev/null; then
    git fetch --no-tags "$UPSTREAM_REMOTE" "$UPSTREAM_BRANCH" \
        || die "git fetch $UPSTREAM_REMOTE $UPSTREAM_BRANCH failed (network/proxy?)"
fi
TARGET="$(git rev-parse FETCH_HEAD)" || die "cannot resolve FETCH_HEAD after fetch"

# --------------------------------------------------------------- find base
if [ -f "$STATE_FILE" ]; then
    read -r BASE < "$STATE_FILE" || die "cannot read $STATE_FILE"
else
    BASE="$SEED_BASE"
    log "No $STATE_FILE found — seeding from verified import baseline ${BASE:0:12}"
fi
git rev-parse --verify --quiet "$BASE^{commit}" >/dev/null \
    || die "base commit $BASE not found locally — check $STATE_FILE, then 'git fetch $UPSTREAM_REMOTE'"

if [ "$BASE" = "$TARGET" ]; then
    log "Already in sync with $UPSTREAM_REMOTE/$UPSTREAM_BRANCH (${TARGET:0:12})."
    if [ "$DRY_RUN" -eq 0 ] && [ ! -f "$STATE_FILE" ]; then
        printf '%s\n' "$TARGET" > "$STATE_FILE"
        log "Wrote $STATE_FILE — commit it."
    fi
    exit 0
fi

log "Sync range: ${BASE:0:12} .. ${TARGET:0:12}"

# ------------------------------------- skip files the fork deleted on purpose
# A path that upstream changed, that existed at BASE, but is gone from our
# HEAD was deliberately removed by the fork (stripped subsystem). Applying
# its patch would either error or resurrect it, so exclude it.
SKIPPED=()
EXCLUDES=("${PROTECTED[@]}")
while IFS= read -r f; do
    [ -n "$f" ] || continue
    if git cat-file -e "$BASE:$f" 2>/dev/null && \
       ! git cat-file -e "HEAD:$f" 2>/dev/null; then
        SKIPPED+=("$f")
        EXCLUDES+=(":(exclude)$f")
    fi
done < <(git diff --name-only "$BASE" "$TARGET" -- "${SYNC_PATHS[@]}" "${PROTECTED[@]}")

if [ "${#SKIPPED[@]}" -gt 0 ]; then
    log "Skipping ${#SKIPPED[@]} file(s) deleted by the fork:"
    printf '    %s\n' "${SKIPPED[@]}"
fi

# ------------------------------------------------------------------ dry run
if [ "$DRY_RUN" -eq 1 ]; then
    log ""
    log "Dry run — in-scope upstream changes (${BASE:0:12}..${TARGET:0:12}):"
    git diff --stat "$BASE" "$TARGET" -- "${SYNC_PATHS[@]}" "${EXCLUDES[@]}"
    log ""
    log "Nothing applied. Run without --dry-run to sync; $STATE_FILE unchanged."
    exit 0
fi

# -------------------------------------------------------------------- apply
PATCH="$(mktemp "${TMPDIR:-/tmp}/upstream-sync.XXXXXX.patch")" || die "mktemp failed"

git diff --binary --full-index "$BASE" "$TARGET" -- "${SYNC_PATHS[@]}" "${EXCLUDES[@]}" > "$PATCH" \
    || die "failed to generate patch (blob fetch problem? retry, or check proxy)"

if [ ! -s "$PATCH" ]; then
    rm -f "$PATCH"
    log "No in-scope changes between ${BASE:0:12} and ${TARGET:0:12}."
    printf '%s\n' "$TARGET" > "$STATE_FILE"
    log "Advanced $STATE_FILE to ${TARGET:0:12} — commit it."
    exit 0
fi

log "Applying $(grep -c '^diff --git ' "$PATCH") file diff(s) with 3-way merge ..."
if git apply --3way "$PATCH"; then
    rm -f "$PATCH"
    printf '%s\n' "$TARGET" > "$STATE_FILE"
    log ""
    log "Sync applied cleanly."
    log "  - review:  git status && git diff --cached"
    log "  - build/test, then commit everything INCLUDING $STATE_FILE, e.g."
    log "      git commit -am 'Sync upstream combat data to ${TARGET:0:12}'"
    exit 0
fi

# ------------------------------------------------------------ conflict path
UNMERGED="$(git diff --name-only --diff-filter=U)"
err ""
if [ -n "$UNMERGED" ]; then
    err "3-way apply left conflicts in the files below. This is expected when"
    err "the fork's edits (auto-rotation refs removed, hand-ported hunks)"
    err "overlap upstream's changes:"
    while IFS= read -r f; do err "    $f"; done <<< "$UNMERGED"
    err ""
    err "Next steps:"
    err "  1. open each file, resolve the <<<<<<< ours / >>>>>>> theirs markers"
    err "     (keep the fork's removals of auto-rotation references)"
    err "  2. git add <each resolved file>"
    err "  3. echo $TARGET > $STATE_FILE"
    err "  4. git commit -am 'Sync upstream combat data to ${TARGET:0:12}'"
    err "To abandon instead: git reset --merge   (or: git checkout -- ${SYNC_PATHS[*]})"
    err "The raw patch is kept at: $PATCH"
else
    err "git apply --3way failed without leaving conflict markers (likely a"
    err "missing base blob or a file that changed type). Inspect 'git status',"
    err "revert any partial changes with 'git checkout -- <path>', then retry."
    err "The raw patch is kept at: $PATCH"
fi
err ""
err "$STATE_FILE was NOT updated (still $(printf '%s' "$BASE" | cut -c1-12))."
exit 1
