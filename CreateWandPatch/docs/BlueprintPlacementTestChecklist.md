# Blueprint Placement Minimal Repro Checklist

## 1) Single Player

- [ ] PNG datamap, small (16x16): paste succeeds, no crash.
- [ ] PNG datamap, large (200x120): paste succeeds, no crash.
- [ ] qotstruct, small: paste succeeds, no crash.
- [ ] qotstruct, large: paste succeeds, no crash.
- [ ] cwmap, small, default gate OFF: paste succeeds with legacy-safe downgrade.
- [ ] cwmap, small, gate ON (`CreateWandSelectionState.EnablePreciseCwmapPlacement = true`): precise copy works in single player.

## 2) Multiplayer (Listen/Dedicated)

- [ ] PNG datamap, small: client paste does not crash/kick, server keeps result after relog.
- [ ] PNG datamap, large: client paste does not crash/kick, server keeps result after relog.
- [ ] qotstruct, small: paste does not crash/kick, no server rollback.
- [ ] qotstruct, large: paste does not crash/kick, no server rollback.
- [ ] cwmap, small: paste does not crash/kick, uses legacy-safe downgrade, no rollback.
- [ ] cwmap, large: paste does not crash/kick, uses legacy-safe downgrade, no rollback.

## 3) Fast/Staggered Behavior

- [ ] Fast mode remains fast (no auto switch to staggered queue).
- [ ] Staggered mode still works when user explicitly enables it.

## 4) Offline Verifier Guidance

- [ ] `tools\BlueprintVerifier` reports legacy classification summary for downgraded maps.
- [ ] For precise payloads, verifier reports `MP downgrade risk cells`.
