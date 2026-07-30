#!/usr/bin/env bash
set -euo pipefail

MODE="${1:---dry-run}"
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

echo "Repository: $ROOT"
echo "Branch: $(git branch --show-current)"
echo "SHA: $(git rev-parse HEAD)"
echo

echo "Tracked .DS_Store / .utmp:"
git ls-files | grep -E '(^|/)\.DS_Store$|(^|/)\.utmp(/|$)|\.utmp$' || true
echo

echo "All local .DS_Store / .utmp:"
find . \
  \( -name .git -o -name Library -o -name Temp -o -name Logs -o -name Obj \) -prune \
  -o \( -name .DS_Store -o -name .utmp -o -name '*.utmp' \) -print
echo

if [[ "$MODE" != "--apply" ]]; then
  echo "DRY RUN ONLY."
  echo "Для удаления запустите:"
  echo "bash Tools/Integration/cleanup_repo_hygiene.sh --apply"
  exit 0
fi

echo "Removing tracked and untracked .DS_Store files..."
while IFS= read -r -d '' file; do
  relative="${file#./}"
  git rm -f --ignore-unmatch -- "$relative" >/dev/null 2>&1 || true
  rm -f -- "$file"
done < <(
  find . \
    \( -name .git -o -name Library -o -name Temp -o -name Logs -o -name Obj \) -prune \
    -o -type f -name .DS_Store -print0
)

echo "Removing .utmp directories..."
while IFS= read -r -d '' directory; do
  relative="${directory#./}"
  git rm -r -f --ignore-unmatch -- "$relative" >/dev/null 2>&1 || true
  rm -rf -- "$directory"
done < <(
  find . \
    \( -name .git -o -name Library -o -name Temp -o -name Logs -o -name Obj \) -prune \
    -o -type d -name .utmp -print0
)

echo "Removing *.utmp files..."
while IFS= read -r -d '' file; do
  relative="${file#./}"
  git rm -f --ignore-unmatch -- "$relative" >/dev/null 2>&1 || true
  rm -f -- "$file"
done < <(
  find . \
    \( -name .git -o -name Library -o -name Temp -o -name Logs -o -name Obj \) -prune \
    -o -type f -name '*.utmp' -print0
)

echo
echo "Remaining matching files:"
find . \
  \( -name .git -o -name Library -o -name Temp -o -name Logs -o -name Obj \) -prune \
  -o \( -name .DS_Store -o -name .utmp -o -name '*.utmp' \) -print

echo
echo "Git status:"
git status --short
echo
echo "Done. Review every deletion before commit."
