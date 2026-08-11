#!/usr/bin/env bash
# Pull latest trading skills from upstream into .claude/skills/
set -euo pipefail

REPO="https://github.com/tradermonty/claude-trading-skills/archive/refs/heads/main.tar.gz"
DEST="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/.claude/skills"
SKILLS=(backtest-expert)

mkdir -p "$DEST"

TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TMPDIR"' EXIT

echo "Fetching claude-trading-skills..."
curl -sL "$REPO" | tar -xz --strip-components=1 -C "$TMPDIR"

for skill in "${SKILLS[@]}"; do
  src="$TMPDIR/skills/$skill"
  if [ -d "$src" ]; then
    rm -rf "${DEST:?}/$skill"
    cp -r "$src" "$DEST/$skill"
    echo "  Updated: $skill"
  else
    echo "  WARNING: $skill not found in upstream repo — skipped"
  fi
done

echo "Done."
