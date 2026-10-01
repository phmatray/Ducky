#!/usr/bin/env bash
# Local check of build/protect-branch.sh (SPEC §20.2) against a fake, stateful `gh`:
# - running the script twice changes nothing the second time (same GitHub state, one tag ruleset);
# - the payload carries what makes a direct push and a red-check merge impossible.
# VerifyWorkflows runs it, and also checks that the required checks are exactly the pull-request job names.
# Usage: build/protect-branch.test.sh   (needs bash, jq; never talks to GitHub)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin" "$work/state"

cat >"$work/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fake gh: stores each PUT/PATCH/POST body under $FAKE_GH_STATE keyed by endpoint; GET serves it back.
set -euo pipefail
[[ "$1" == api ]] || { echo "fake gh: unsupported $*" >&2; exit 2; }
shift
method=GET endpoint="" input="" jq_expr=""
while (($#)); do
  case "$1" in
    -X | --method) method="$2"; shift 2 ;;
    --input) input="$2"; shift 2 ;;
    --jq | -q) jq_expr="$2"; shift 2 ;;
    -*) echo "fake gh: unsupported flag $1" >&2; exit 2 ;;
    *) endpoint="$1"; shift ;;
  esac
done
echo "$method $endpoint" >>"$FAKE_GH_STATE/../calls"
key="$FAKE_GH_STATE/$(echo "$endpoint" | tr '/{}' '_..')"
body() { [[ "$input" == - ]] && jq -S . || { echo "fake gh: body expected on stdin" >&2; exit 2; }; }
case "$method $endpoint" in
  "GET repos/{owner}/{repo}/rulesets")
    out="$(find "$FAKE_GH_STATE" -name 'ruleset_*' -exec cat {} + | jq -s '[.[] | {id, name, target}]')" ;;
  "POST repos/{owner}/{repo}/rulesets")
    id=$(($(find "$FAKE_GH_STATE" -name 'ruleset_*' | wc -l) + 1))
    out="$(body | jq --argjson id "$id" '. + {id: $id}')"; echo "$out" >"$FAKE_GH_STATE/ruleset_$id" ;;
  "PUT repos/{owner}/{repo}/rulesets/"*)
    id="${endpoint##*/}"; [[ -f "$FAKE_GH_STATE/ruleset_$id" ]] || { echo "fake gh: 404" >&2; exit 1; }
    out="$(body | jq --argjson id "$id" '. + {id: $id}')"; echo "$out" >"$FAKE_GH_STATE/ruleset_$id" ;;
  PUT* | PATCH*) out="$(body)"; echo "$out" >"$key" ;;
  *) echo "fake gh: unexpected $method $endpoint" >&2; exit 2 ;;
esac
if [[ -n "$jq_expr" ]]; then jq -r "$jq_expr" <<<"$out"; else echo "$out"; fi
FAKE
chmod +x "$work/bin/gh"

fail() { echo "FAIL: $*" >&2; exit 1; }
run() { PATH="$work/bin:$PATH" FAKE_GH_STATE="$work/state" "$root/build/protect-branch.sh" "$@" >/dev/null; }

run v2
cp -R "$work/state" "$work/after-first"
run v2
diff -r "$work/after-first" "$work/state" >/dev/null || fail "second run changed GitHub state: $(diff -r "$work/after-first" "$work/state")"
[[ "$(find "$work/state" -name 'ruleset_*' | wc -l | tr -d ' ')" == 1 ]] || fail "second run duplicated the tag ruleset"

protection="$work/state/repos_.owner._.repo._branches_v2_protection"
repo="$work/state/repos_.owner._.repo."
[[ -f "$protection" ]] || fail "no branch protection written for v2"
check() { jq -e "$2" "$1" >/dev/null || fail "$3"; }
check "$protection" '.required_pull_request_reviews.required_approving_review_count == 0' "PR with 0 approvals required"
check "$protection" '.enforce_admins == true' "admins included (no direct push, even by the owner)"
check "$protection" '.required_status_checks.strict == true' "strict required checks"
check "$protection" '.required_linear_history == true and .allow_force_pushes == false and .allow_deletions == false' \
  "linear history, no force-push, no deletion"
check "$protection" '.required_conversation_resolution == true' "conversation resolution"
check "$protection" '[.required_status_checks.checks[].app_id] | unique == [15368]' "checks pinned to the GitHub Actions app"
check "$repo" '.allow_squash_merge == true and .allow_merge_commit == false and .allow_rebase_merge == true' "squash or rebase, never merge commits"
check "$repo" '.allow_auto_merge == true' "platform automerge behind the required checks, for Renovate (§20.2)"
check "$repo" '.squash_merge_commit_title == "PR_TITLE"' "squash commit takes the PR title (pr-title.yml)"
ruleset="$(find "$work/state" -name 'ruleset_*')"
check "$ruleset" '.target == "tag" and .enforcement == "active" and .conditions.ref_name.include == ["refs/tags/v*"]' "v* tag ruleset"
check "$ruleset" '[.rules[].type] | sort == ["creation", "deletion", "update"]' "tag ruleset blocks create, update, delete"
check "$ruleset" '.bypass_actors == [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}]' \
  "only the maintainer (repository admin) bypasses the tag ruleset"

rm -rf "$work/state" "$work/calls" && mkdir "$work/state"
run main
[[ -f "$work/state/repos_.owner._.repo._branches_main_protection" ]] || fail "branch argument ignored"

echo "protect-branch.sh: OK"
