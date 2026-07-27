# Unity to vNext 3D Migration — Implementation Spec

Version: 1.0.0  
Tool: Altoura Migration Exporter (`Assets/AltouraMigration/Editor/`)

## Purpose

Export Unity prefabs/scenes into a self-contained **exchange package** (`.altpkg.zip`) containing:

1. GLB geometry with embedded `altouraStableId` on every node (`extras` field)
2. Converted timeline data (vNext OTIO JSON subset)
3. Lossless Unity Timeline capture (future-proof sidecar)
4. Particle, audio, and texture sidecars

SCJSON training logic and the vNext-side importer are **out of scope** for this tool.

## Locked format decisions

| Decision | Choice |
|----------|--------|
| Geometry | GLB via UnityGLTF (`org.khronos.unitygltf`) |
| Stable ID field | `extras.altouraStableId` on glTF nodes |
| ID source | `GlobalObjectId` hashed to UUID string |
| Timeline converted | OTIO JSON matching `apps/sceneManager/src/types/otio/otio.ts` |
| Timeline lossless | Custom `.unity.json` from Timeline Editor API |
| Sample rate | 60 fps (matches `TimelineSettings.asset`) |

## Exchange package layout

```
<name>.altpkg.zip
├── manifest.json
├── models/
│   └── <asset>.glb
├── timelines/
│   ├── <timeline>.otio.json      # vNext-compatible subset
│   └── <timeline>.unity.json     # lossless Unity capture
├── particles/
│   └── <particleSystem>.json
├── audio/
│   └── <clip>.wav / .ogg / ...
└── textures/
    └── <texture>.png / .jpg
```

## manifest.json schema

```json
{
  "packageVersion": "1.0.0",
  "toolVersion": "0.1.0",
  "unityVersion": "2021.3.15f1",
  "exportedAt": "2026-07-01T12:00:00Z",
  "source": {
    "name": "Realscale",
    "type": "prefab",
    "assetPath": "Assets/Prefabs/Combo Room/Realscale.prefab"
  },
  "coordinateSystem": "glTF_Y_UP_RIGHT_HANDED",
  "models": [
    {
      "file": "models/Realscale.glb",
      "name": "Realscale",
      "rootStableId": "<uuid>"
    }
  ],
  "timelines": [
    {
      "name": "door_open_timeline",
      "otioFile": "timelines/door_open_timeline.otio.json",
      "unityFile": "timelines/door_open_timeline.unity.json",
      "directorHierarchyPath": "Root/DoorController",
      "playableAssetPath": "Assets/Animations/door_open_timeline.playable"
    }
  ],
  "particles": [
    {
      "name": "airflow1",
      "file": "particles/airflow1.json",
      "altouraStableId": "<uuid>",
      "hierarchyPath": "Root/Airflow/airflow1"
    }
  ],
  "audio": [
    { "file": "audio/stingComplete3.wav", "originalPath": "Assets/Audio/stingComplete3.wav" }
  ],
  "textures": []
}
```

## Stable ID derivation

1. For each `GameObject` in the export hierarchy, call `GlobalObjectId.GetGlobalObjectIdSlow(go)`.
2. If valid (`identifierType != 0`), hash `gid.ToString()` with SHA-1 into a UUID v5-style string.
3. Fallback for unsaved/temporary objects: hash `instanceId` string.
4. Before GLB export, populate `StableIdRegistry` mapping every `GameObject` to its ID.
5. UnityGLTF `BeforeNodeExport` plugin writes `extras.altouraStableId` on each glTF `Node`.

vNext reads this via three.js `GLTFLoader` → `object.userData.altouraStableId`.  
`packages/asset-processor` preserves pre-existing IDs and does not regenerate.

### Initial visibility

Disabled GameObjects are exported as regular GLB nodes (required for timeline
bindings to resolve). Since glTF has no node visibility, nodes whose
`GameObject.activeSelf` is false additionally carry
`extras.altouraInitiallyActive: false`. The vNext importer must set
`object.visible = false` for these nodes on load; the flag mirrors Unity's
per-object `activeSelf` (not `activeInHierarchy`), so visibility inheritance
through parents behaves the same in three.js as in Unity. Nodes without the
flag are initially active.

Separately, a GameObject may be active while its `MeshRenderer` /
`SkinnedMeshRenderer` is disabled (the mesh exists but is not drawn). Because
`ExportDisabledGameObjects` is enabled, UnityGLTF still exports the mesh, so
such nodes carry `extras.altouraRendererEnabled: false`. The vNext importer
treats this the same as an initially-hidden node (`object.visible = false` /
`isVisible: false` node override). This flag is kept distinct from
`altouraInitiallyActive` because it reflects render state, not active state.
Note: three.js `visible = false` also hides descendants, which matches Unity
only when the disabled renderer is on a leaf node.

## OTIO output format

Matches vNext `OtioSerializableCollection`:

- Root: `OTIO_SCHEMA: "SerializableCollection.1"`
- Each timeline: `OTIO_SCHEMA: "Timeline.1"` with `timelineId` (new UUID per export)
- Tracks: one `Sequence.1` per animated property channel
- Keyframe time: `{ OTIO_SCHEMA: "RationalTime.1", value: <seconds>, rate: 1 }`
- Rotation keyframes: quaternion `[x, y, z, w]` in glTF coordinate space
- Visibility: `type: "visibility"`, `property: "visible"`, step interpolation

### Unity → OTIO mapping

| Unity Timeline | OTIO channel |
|----------------|--------------|
| ActivationTrack | `visible` (step keyframes at clip boundaries) |
| AnimationTrack (transform) | `position`, `rotation`, `scale` (sampled 60fps, linear) |
| AnimationTrack (skinned) | embedded GLB clip reference in `.unity.json`; clip name in manifest |
| AudioTrack | `type: "audio"`, `audioFileName`, clip value in `keyframes[0].value` |
| ControlTrack / sub-timeline | `property: "subTimeline"` when mappable |
| Particle / unsupported | `.unity.json` only |

## Lossless .unity.json schema

```json
{
  "schemaVersion": "1.0.0",
  "timelineName": "door_open_timeline",
  "duration": 2.0,
  "fps": 60,
  "directorHierarchyPath": "...",
  "playableAssetPath": "Assets/Animations/door_open_timeline.playable",
  "tracks": [
    {
      "trackType": "UnityEngine.Timeline.ActivationTrack",
      "trackName": "Activation Track",
      "binding": {
        "hierarchyPath": "Door",
        "altouraStableId": "<uuid>",
        "bindingType": "UnityEngine.GameObject"
      },
      "clips": [
        {
          "displayName": "ActivationClip",
          "start": 0.0,
          "duration": 2.0,
          "clipIn": 0.0,
          "easeInDuration": 0.0,
          "easeOutDuration": 0.0
        }
      ],
      "raw": {}
    }
  ]
}
```

## Coordinate conversion

Unity (left-handed Y-up) → glTF/Three.js (right-handed Y-up):

- Position: `(x, y, z)` → `(x, y, -z)`
- Rotation: `Quaternion` multiplied by axis-flip conversion quaternion
- Scale: `(x, y, z)` unchanged

Applied in `CoordinateConverter` for OTIO keyframe values. Geometry conversion is handled by UnityGLTF.

## Unity Editor usage

1. Install packages (UnityGLTF added to `Packages/manifest.json`).
2. Open **Altoura → Migration Export** window.
3. Assign a prefab (e.g. `Assets/Prefabs/Combo Room/Realscale.prefab`) or select a root in the Hierarchy.
4. Choose export options:
   - **All timelines** — every `PlayableDirector` in hierarchy
   - **Single timeline filter** — vertical slice (e.g. `door_open_timeline`)
5. Click **Export Package** → saves `<name>.altpkg.zip`.

### Vertical slice validation

1. Export `door_open_timeline` from a prefab containing that director.
2. Unzip package; inspect `models/*.glb` in a glTF viewer — confirm `extras.altouraStableId` on nodes.
3. In vNext, upload GLB to asset catalog; confirm `userData.altouraStableId` preserved.
4. Future vNext importer will load `.otio.json` and bind tracks via stable IDs.

## File map (Unity tool)

| File | Responsibility |
|------|----------------|
| `MigrationExportWindow.cs` | Editor UI |
| `MigrationPackageExporter.cs` | Orchestrates full export |
| `StableIdRegistry.cs` | Hierarchy ID map |
| `StableIdService.cs` | GlobalObjectId → UUID |
| `AltouraStableIdExportPlugin.cs` | UnityGLTF node extras |
| `GlbExporter.cs` | GLB export |
| `TimelineCaptureService.cs` | Lossless timeline JSON |
| `TimelineOtioConverter.cs` | OTIO conversion |
| `ParticleCaptureService.cs` | Particle sidecars |
| `PackageManifestBuilder.cs` | manifest.json |
| `ZipPackageWriter.cs` | ZIP assembly |
| `CoordinateConverter.cs` | Unity → glTF coords |
| `JsonFileWriter.cs` | JSON serialization |

## Phase 5 — scale-up notes

`Combo Room/Realscale.prefab` has ~95 `PlayableDirector` components. Full export:

- Instantiate prefab in isolation for export
- Iterate all directors; deduplicate by `TimelineAsset` path
- Batch progress bar in Editor window
- Expect large ZIP (15 MB+ prefab + timelines + audio)
