# Altoura Migration Export (UPM package)

Unity Editor tool that exports prefabs/scenes into `.altpkg.zip` exchange
packages for import into the Altoura vNext Scene Manager ("Import Package"
in the File ribbon tab).

## Developing and testing

The repository includes a self-contained Unity project under `TestProject~`.
Open that folder in Unity Hub with Unity 2022.3.67f2. It references the package
source at the repository root, so code changes under `Editor/` are compiled
directly without publishing or refreshing a Git package.

After the project opens, run
**Altoura → Migration Tests → Create Test Fixture**. See
`TestProject~/README.md` for the export and verification flow.

## Installing into a content project

### Unity Package Manager UI (recommended)

1. In Unity, open **Window → Package Manager**.
2. Click **+** in the upper-left and select **Add package from git URL…**.
3. Install UnityGLTF first:

   ```text
   https://github.com/KhronosGroup/UnityGLTF.git#release/2.14.1
   ```

4. Repeat **Add package from git URL…** and install the exporter:

   ```text
   https://github.com/altoura-org/unity-migration-export.git#v0.2.0
   ```

5. Wait for Unity to compile. **Altoura → Migration Export** should then
   appear in the editor menu.

Both repositories are public, so installation does not require GitHub
authentication.

### Packages/manifest.json alternative

The Package Manager UI writes these entries into the consuming project's
`Packages/manifest.json`; they can also be added manually:

```json
"org.khronos.unitygltf": "https://github.com/KhronosGroup/UnityGLTF.git#release/2.14.1",
"com.altoura.migration-export": "https://github.com/altoura-org/unity-migration-export.git#v0.2.0"
```

`com.unity.timeline` is declared as a normal dependency and resolves
automatically.

## Publishing a public release

Development remains in the private Azure DevOps repository. Tagged releases
are mirrored to the public `altoura-org/unity-migration-export` GitHub repository
by `.azuredevops/release.yml`.

1. Configure an Azure Pipeline using `.azuredevops/release.yml`.
2. Add a secret pipeline variable named `GITHUB_TOKEN`. Use a fine-grained
   GitHub token with Contents read/write permission for only the public repo.
3. Update the version in `package.json`, commit, and verify `TestProject~`.
4. Create and push a matching tag, for example `v0.2.0`.

The pipeline excludes `TestProject~` and its own pipeline file from the public
release. The public repository contains only the installable package, contract,
validation tools, and documentation.

## Usage

1. Menu: **Altoura → Migration Export**
2. Assign a prefab or select a Hierarchy root.
3. Click **Export Package**.

Output ZIP contains:

- `manifest.json`
- `models/*.glb` — node `extras` carry `altouraStableId`, plus
  `altouraInitiallyActive: false` (inactive GameObjects) and
  `altouraRendererEnabled: false` (disabled Mesh/SkinnedMeshRenderers)
- `timelines/*.otio.json` — vNext-compatible OTIO (sampled at 60 fps)
- `timelines/*.unity.json` — lossless Unity Timeline capture (sidecar)
- `particles/*.json`
- `audio/*` (when referenced by timelines)

After export, validate with **Altoura → Validate GLB Stable IDs**.

Optional Draco compression requires Node.js on the machine (runs
`npx @gltf-transform/cli`). Note: the vNext asset processor re-exports GLBs
uncompressed, so Draco only reduces package transfer size.

## Docs

- `Documentation~/unity-vnext-migration-spec.md` — package format spec
- `Documentation~/asset-migration-walkthrough.md` — end-to-end walkthrough

The vNext-side importer spec lives in the Altoura_vNext repo:
`apps/sceneManager/docs/altoura-package-importer-spec.md`.
