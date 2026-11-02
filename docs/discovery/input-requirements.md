# Minimum private reference request

**Current status: actual game references not yet available.** PC permission has
been granted, but execution service initialization failed before inspection or
transfer. Inspection/transfer is pending, not denied or completed. The blocker
is execution service availability and missing actual files, not another
permission request. A path on the PC is not a file in this cloud repository.
No successful PC inspection, transfer, assembly inspection, loader compatibility,
or runtime behavior is established here.

## 1. Metadata and inventory first (no complete game copy)

Through an authorized upload or an authorized, supported private transfer route,
provide only:

- The installed game version/build identifier and distribution branch, plus the
  platform/architecture and Unity version/runtime/backend if actually known.
  Unknown facts should stay unknown rather than be inferred from public mods.
- A managed-assembly filename inventory, and directly referenced assembly names
  when available from authorized read-only metadata inspection. Include byte
  sizes/hashes if already available; do not transfer all assemblies merely to
  generate an inventory. If the build is not managed/Mono, report that fact and
  stop the assumed integration path for review.
- Existing BepInEx/loader version and plugin filenames if present, using a bounded
  inventory without account data or indiscriminate logs. Do not install a loader
  or alter another mod to satisfy this request.

Stay within the granted PC permission and applicable transfer controls. A
permission grant does not establish that execution or transfer succeeded.

## 2. Select only the necessary assembly files

Use the metadata/inventory to identify the gameplay assembly (often named
`NuclearOption_Data/Managed/Assembly-CSharp.dll`, not assumed for this build),
then request only it, its actually needed direct game dependencies, and the
specific Unity/API reference assemblies required for camera/PiP discovery and
compilation. Prefer official loader reference distributions where suitable;
exact runtime/framework/dependency versions are a later verified decision.
Do not download the cited public mods as substitutes for the installed build.

The first targeted discovery is offline-session detection, player/main-camera
and input lifecycle, native free-camera handoff and recovery, missile lifetime,
rendering, scene exit, and world-origin handling. General possession, RTS orders,
and missile-guidance/retargeting discovery are deferred. Inspect only relevant
metadata/control flow, not a wholesale game decompilation.

## 3. Private cloud placement and handoff

After the execution service is available and the authorized transfer succeeds,
verify that selected files are readable on the cloud executor. Preserve their
relative names beneath `private/refs/`; keep that tree stable while hashing. Use the README CLI to write
`private/discovery/reference-manifest.json`, then compare its hashes with any
transfer manifest. Private manifests can reveal installed-build information and
must remain private along with the references and inspection notes.

Task 2 consumes the deterministic schema-version-1 manifest. It must establish
actual-build contracts, evidence and loader/runtime versions before Tasks 3–5
can start. Static findings remain runtime-unverified. Task 6 additionally needs
working PC execution and actual runtime/cleanup evidence within the granted
permission; fake fixtures cannot pass it.

**Never send or commit** the full game, assets, saves, account data, credentials,
proprietary DLLs, bulk decompiled source, or unredacted runtime logs. Do not modify
original game DLLs. No public push or release upload is part of this intake work.
