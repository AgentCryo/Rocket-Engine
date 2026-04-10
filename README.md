# Rocket Engine

API Documentation: [RocketEngineDocs](https://agentcryo.github.io/RocketEngineDocs/).

## Dev
A sandbox environment used for testing new features.  
This is the user-side/game layer built on top of Rocket Engine.

---

# Current Rocket Engine Libraries:

## RCS — Rocket Control System v0.2
RCS is the Entity Component System (ECS) used by Rocket Engine. It also acts as the engine core.

**Done:**
- Component interface
- Entity with component handling
- Scene with entity handling
- Transform listeners (position & direction)
- Arbitrary entity data storage
- Engine-wide logger (LOG, WRN, ERR)

**Planned:**
- Better global error logger
- Additional ECS utilities and helpers

---

## RERL — Rocket Engine Rendering Library v0.2
A standalone rendering library built for Rocket Engine.

**Done:**
- GLTF model loading (hierarchy, transforms, .gltf + .glb)
- Albedo texture and color material support
- Lighting system (point, spot, directional)
- Smooth edge clamping and global lights
- Cluster shading optimizations
- Improved G-Buffer (position, normal, albedo, depth)
- Shader system improvements (shader types, helper includes, auto-uniforms)
- Post-processing pipeline refinements
- Compute shader support
- RCS-integrated CameraComponent
- XML documentation for most public API members

**Removed:**
- OBJ model loading (replaced by GLTF)

**Planned:**
- PBR materials
- Additional texture maps (normal, roughness/metallic, etc.)
- Transparency / translucency
- Voxel global illumination
- More advanced post-processing effects

---
