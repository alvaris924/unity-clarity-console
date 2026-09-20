# Feature ideas

Candidates for the console after the v1.0.0 scope, ranked by how much they help day-to-day game debugging rather than by novelty. Every one of them is designed from public Unity APIs and the data the package already captures; none needs a new dependency. Effort is a rough size for one pull request.

Status legend: `idea` not started, `planned` accepted for a milestone, `in progress`, `shipped` (then it moves to the changelog).

| Rank | Feature | Why it matters in a game project | Builds on | Effort | Status |
|---|---|---|---|---|---|
| 1 | **Log breakpoints**: pause Play mode when a message matches a query, such as `tag:Combat sev:warn` or `/timeout/` | Error Pause generalised. Freezing the game the moment the boss logs *phase 2* is what a gameplay developer wants when inspecting the scene, and nothing built into Unity does it. | Query engine, Error Pause hook | Small | idea |
| 2 | **Frame lens**: `frame:1213` and `frame:1200-1215` in the query, plus a row context menu with "Show this frame" and "±5 frames" | After an error the real question is usually "what else happened that frame?". The frame number is already captured. | Query parser, `LogEntry.Frame` | Small | idea |
| 3 | **Selection filter**: a toolbar toggle that shows only entries whose context object is the selected GameObject or one of its children | "Everything *this* enemy instance logged" in a wave of two hundred. Uses the context ids already stored. | `LogEntry.ContextInstanceId`, `Selection` | Small to medium | idea |
| 4 | **Marks**: a hotkey drops a user marker with an optional note ("Mark 3, 00:41, started boss fight") and the query gains "since last mark" | Repro discipline: press the key when the bug shows, read only what followed. Pairs with the timeline strip. | Session markers, timeline | Small | idea |
| 5 | **Spam meter**: a rate on collapsed rows (`×1200 · 120/s`), a "top talkers" popover and a status-bar warning above a logging rate threshold | `Debug.Log` allocates and is slow in builds; per-frame log spam is one of the most common hidden mobile performance sinks and the stock console never points at it. | Collapse counts, timestamps | Small | idea |
| 6 | **Highlight rules**: a query that tints matching rows, saved per project | Reading dense logs: networking in blue, save and load in green. Falls out of the theme variables. | Query engine, themes, project settings | Small to medium | idea |
| 7 | **Structured detail pane**: pretty-print JSON found in a message, render runs of `key=value` as a table, make URLs and file paths clickable | Backend-heavy games log JSON blobs all day. | Detail view | Medium | idea |
| 8 | **Scene-view badges**: a small badge over GameObjects that logged an error in the last few seconds; clicking it selects the object and filters to it | Very visual and genuinely useful in spawn-heavy games: "which one threw?". Also the natural showpiece for screenshots. | `SceneView.duringSceneGui`, Handles, context ids | Medium | idea |
| 9 | **Unread badge and first-error jump**: `Clarity Console (3)` in the tab title, and a shortcut that jumps to the first error since Play started | The first error is usually the root cause; the cascade after it is noise. | Store events, `titleContent` | Small | idea |
| 10 | **Problems tab**: compiler, shader and Burst errors grouped by file, one click to the line | Compile errors are easy to miss under gameplay logs in a mixed stream. | `CompilationPipeline` messages | Medium | planned (roadmap) |
| 11 | **Test Runner markers**: session markers at each test's start and end through `TestRunnerApi` callbacks | Attributes logs to the test that produced them. | Session markers | Small | idea |
| 12 | **Frame-time spike markers**: a marker whenever a Play-mode frame exceeds a threshold | Correlates hitches with what was logged at that moment; complements the frame lens and the timeline. | Session markers, `Time.unscaledDeltaTime` sampling | Small to medium | idea |
| 13 | **Device logs**: stream `adb logcat` and iOS device logs into the console with the same channels and filters | The first thing a mobile team asks for; deferred until the Editor-side features are complete. | Log file import's logcat parser | Large | deferred |

## Shipped from this list

- Manual tags (rules by text, regex or caller, "Tag as…" on a row, and caller auto-tagging): v1.0.0 scope, September 2026.

## Suggested order

1, 2, 3 and 4 share the same plumbing, are one small pull request each, and together turn the console from a better list into a gameplay-debugging tool. Then 5 and 9 as quick wins, and 8 as the showpiece for screenshots and a recording.

## Checks worth doing first

- Rich text in messages (`<color=red>`, `<b>`) should render in rows and in the detail pane the way the stock console renders it.
- Long messages in a narrow panel: see the wrap option.

## Adding to this list

Keep the table sorted by value, say what the feature builds on so the effort stays honest, and move a row to the changelog when it ships. Ideas that would need a third-party dependency or a non-public Unity API do not belong here; see [ADR-0001](adr/0001-log-source.md) and [ADR-0003](adr/0003-dependency-policy.md).
