#!/usr/bin/env bash
# Branch protection for a solo maintainer (SPEC §20.2, ADR-0041). Idempotent: run it as often as you like.
# Usage: build/protect-branch.sh [branch]   (v2 during development, main after the GA rename, §21)
# Needs `gh` authenticated as a repository admin; the repository comes from the current checkout or GH_REPO.
set -euo pipefail
branch="${1:-v2}"

# Squash for single-change PRs (the squash commit takes the PR title, which pr-title.yml checks as a Conventional Commit);
# rebase for milestone PRs, which keep one commit per story (owner decision). History stays linear either way.
# Auto-merge on: Renovate's platformAutomerge merges through it, behind the required checks (§20.2).
gh api -X PATCH 'repos/{owner}/{repo}' --input - >/dev/null <<'JSON'
{
  "allow_auto_merge": true,
  "allow_squash_merge": true,
  "allow_merge_commit": false,
  "allow_rebase_merge": true,
  "squash_merge_commit_title": "PR_TITLE",
  "squash_merge_commit_message": "PR_BODY"
}
JSON

# A full PUT replaces the whole protection, so a second run leaves it unchanged.
# Required checks are exactly the job names that report on a PR, pinned to the GitHub Actions app (15368)
# so that a commit status posted by anything else can't satisfy them.
jq -n '{
  required_status_checks: {
    strict: true,
    checks: [("ci", "ci-cross-windows", "ci-cross-macos", "e2e", "aot", "mutation", "pr-title")
      | {context: ., app_id: 15368}]
  },
  enforce_admins: true,
  required_pull_request_reviews: {
    required_approving_review_count: 0,
    dismiss_stale_reviews: false,
    require_code_owner_reviews: false
  },
  restrictions: null,
  required_linear_history: true,
  allow_force_pushes: false,
  allow_deletions: false,
  required_conversation_resolution: true
}' | gh api -X PUT "repos/{owner}/{repo}/branches/$branch/protection" --input - >/dev/null

ruleset='{
  "name": "release-tags",
  "target": "tag",
  "enforcement": "active",
  "bypass_actors": [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}],
  "conditions": {"ref_name": {"include": ["refs/tags/v*"], "exclude": []}},
  "rules": [{"type": "creation"}, {"type": "update"}, {"type": "deletion"}]
}'
# Rulesets are created by POST, so update the existing one by name instead of adding a duplicate.
# Bypass: repository role 5 (admin), i.e. the maintainer. Reachability of the tagged commit is Publish's job (§19).
id="$(gh api 'repos/{owner}/{repo}/rulesets' --jq '.[] | select(.name == "release-tags" and .target == "tag") | .id')"
if [[ -n "$id" ]]; then
  gh api -X PUT "repos/{owner}/{repo}/rulesets/$id" --input - >/dev/null <<<"$ruleset"
else
  gh api -X POST 'repos/{owner}/{repo}/rulesets' --input - >/dev/null <<<"$ruleset"
fi

echo "Protected $branch and the v* tags."
