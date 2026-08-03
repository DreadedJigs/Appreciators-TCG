# Appreciators Pack Opening Prototype

This folder contains the Unity-ready exports from the editable Blender source at
`blender/pack_opening_prototype/appreciators_pack_opening_prototype.blend`.

- `appreciators_pack_opening_prototype.fbx` is the primary Unity import. It contains the staged pack, tear strip, portal glow, five card rigs, camera, lights, and the complete 144-frame animation at 24 fps.
- `appreciators_pack_opening_prototype.glb` is the portable interchange version for external review and future glTF workflows.

The current Appreciation Ritual reward and inventory flow remains authoritative in Unity. This scene is a presentation asset: animation events should call the existing `PackOpeningFlow` transitions rather than grant or select cards.

Timing reference:

- Frames 1-38: pack entrance and hold
- Frames 39-68: tear strip
- Frames 69-96: portal opening and first card extraction
- Frames 97-132: remaining card reveals
- Frames 133-144: final five-card fan

The five card-face planes deliberately share the approved full-card reference in this prototype. Runtime integration should assign the five secured reward textures after the backend response is finalized.
