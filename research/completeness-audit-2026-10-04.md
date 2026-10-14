# Translation completeness audit

## Latest repair (2026-10-05)

Version 1.0.1 defaults to automatic English/Chinese direction, protects recognized technical literals, marks old results after edits and has actionable integrity/length failures. Real services pass 14/14, focused audit 11/11 and GUI 13/13, including actual close-during-model-request cleanup. Portable CRC, verified binary/worker matching and four relocated manual/automatic requests pass. See `integrity-and-auto-direction-2026-10-05.md` and the current status section of `offline-implementation-status.md`.

## Superseding deployment and accuracy repair

After this initial audit, the user's missing-engine repair request authorized isolated runtime/model deployment. Real services now pass 13/13, production GUI passes 11/11 with `accepted=true`, and the finalized portable ZIP passes CRC plus relocated bilingual inference. Accuracy changes pass 16 boundary checks and an inspectable 28-case comparison. Latest delivery evidence and remaining limits are in `offline-implementation-status.md` and `translation-quality-2026-10-04.md`. The historical unavailable/incomplete rows below describe the original audit, not the repaired application.

## Historical verdict

The native capture/input application has verified local behavior. It is **not a complete offline translator or a release-ready package**. The inference runtime and both translation models are absent, so real translation, model quality, full memory/latency measurements and network-isolated inference cannot pass yet. No third-party package or model was installed during this audit.

## Defects reproduced and repaired

| Finding | Reproduction | Repair | Regression proof |
| --- | --- | --- | --- |
| Cancellation did not cover stdin writes | Both short and 5000-character requests remained pending beyond 2000 ms when a controlled child did not read stdin. Cancellation passed after the child read stdin. | Install the linked cancellation/timeout callback before writing; terminate the owned child and drain process tasks on errors. | Short, long and post-read cancellation probes now pass. |
| Failed new requests destroyed the previous output | Seed a previous result, attempt a request with no runtime installed, observe an empty result. | Retain the previous result and visibly label it as the previous output during a new request. | Previous-result preservation and labeling check passes. |
| WinForms DPI configuration was missing | Native DPI was 120 while `MainForm.DeviceDpi` was 96. | Enable `PerMonitorV2` in the .NET Framework application configuration. | Native and WinForms DPI now both equal 120; default/minimum layout checks pass. |
| GUI verification could falsely succeed without translation | The old gate accepted successful error handling while both actual translation flags were false; its cancellation step also assumed a visible button after an immediate missing-runtime error. | Record unavailable checks, require both actual translation flags for success, and save a new report even on failure. | Seven local GUI checks pass; the full gate correctly returns failure with `accepted=false`. |

The DPI configuration follows [Microsoft's .NET Framework guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms). C# 5 compiler compatibility is retained; asynchronous process cleanup preserves and rethrows failures outside a `finally` block.

An earlier preview appeared miniature, so the evidence was checked independently. The actual PNG and native window were 678x597, the window was not minimized, and the image body was opaque. No collapsed-window defect was reproduced. The screenshot gate now rejects minimized/collapsed windows and saves opaque RGB evidence. The GUI status locator was also corrected to support repeat runs against an already-open window with an error status.

## Acceptance matrix

| Area | Status | Scope |
| --- | --- | --- |
| Clean compilation | Pass | Framework compiler, warning level 4, warnings as errors; no new packages. |
| Local service checks | Pass, 11/11 | Input limits, invalid response protocol, missing-runtime feedback, crop bounds, English/Chinese Windows OCR, Unicode paths and OCR cleanup. |
| Focused regression audit | Pass, 10/10 | Process Unicode transport, three cancellation cases, failed-request recovery, previous output, current-DPI/minimum layout, hotkey conflict, Ctrl+Enter/copy fixture and oversize OCR. |
| Actual Python worker input contract | Pass, 4/4 | Empty, over-limit, unsupported direction and missing-model errors. Uses the actual worker with the already-installed Python standard library. |
| External GUI local workflows | Pass, 7 checks | Empty/over-limit input, visible engine failure, physical screenshot/hotkey/OCR, Esc/right-click capture cancellation, blank OCR cleanup. |
| Actual input translation | Unavailable | Runtime and models absent. |
| Screenshot -> OCR -> actual translation | Unavailable | OCR passes; model inference unavailable. |
| Active real-model cancellation and window close | Unavailable | Controlled-child cancellation passes; genuine model worker has not been installed. |
| Static offline boundary | Pass within source scope | No HTTP translation endpoint or explicit network client in product source/worker. This does not prove dependency behavior or network-isolated inference. |
| Clipboard action | Pass for a public fixture | Actual copy performed; original clipboard restored. No private clipboard content logged. |
| Multi-monitor / mixed DPI | Unverified | Only the available 1920x1080 display at 120 DPI was exercised. |
| Deployment / complete distribution | Incomplete | App contains executable, config and worker only; runtime, two models, pinned dependency/model manifest and final distribution notices remain to be supplied. |
| Real quality / long-text fidelity | Unverified | Requires the actual tokenizer and models. Process protocol tests are not translation quality tests. |
| Full resource/latency budget | Unverified | Current GUI idle memory passes the 80 MB target; real inference child peaks, cold latency and full package size remain unknown. |

## Commands and evidence

```powershell
& 'A:\Projects\Translation\scripts\build.ps1'
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode local
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode audit
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode services
& 'A:\Projects\Translation\scripts\verify.ps1' -Mode gui
```

`services` fails on the missing offline engine. `gui` completes its local checks and correctly fails final acceptance. Do not suppress these exits or interpret local checks as end-to-end success.

Evidence files under `A:\Agent\Codex\Translation`:

- `verification/local-service-results.json`
- `audit/audit-probes.json`
- `audit/worker-contract-results.json`
- `verification/gui-results.json`
- `verification/main-window.png`
- `audit/completeness-manifest.json`

The process probes reuse an installed Python standard library in an isolated sandbox, without pip or model downloads. They deliberately use synthetic workers and do not establish model inference. No owned OCR/probe helper remained after verification; OCR temporary directories were empty.

## Remaining work in order

1. Obtain explicit approval for dependencies/model downloads under user AGENTS.md section 9.
2. Deploy the isolated inference runtime and both models with pinned versions, verified hashes, validated layout and applicable distribution notices.
3. Run real English/Chinese short and long input tests and screenshot translation; test true model cancellation and close cleanup.
4. Verify genuine inference with outbound connections unavailable in an isolated test context; do not change the machine's global connectivity.
5. Measure inference-inclusive peak memory, idle memory, cold latency and full package size; package and run from a clean target folder.
6. Validate additional monitors/DPI scales when the necessary display environment is available.
