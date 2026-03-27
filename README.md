# Rocket Engine

Reference the GitHub [wiki](https://github.com/AgentCryo/Rocket-Engine/wiki) for docs.

## Dev
A sandbox environment used for testing new features.  
My test area that acts as the user-side/game layer built on top of Rocket Engine.

---

# Current Rocket Engine Libraries:

## RCS — Rocket Control System v0.1.1
RCS is the Entity Component System (ECS) used by Rocket Engine. Will also take the place of an "engine core".

**Done:**
- Component Interface.
- Entity with component handling.
- Scene with entity handling.

**Planned:**
- Better global error logger.

---

## RERL — Rocket Engine Rendering Library v0.1.1
A standalone rendering library built for Rocket Engine.

**Done:**
- Core mesh loading (OBJ) and shader loading (GLSL).
- Basic post‑processing pipeline.
- Batched mesh rendering grouped by shader to reduce shader switching.
- RCS‑integrated Camera Component.
- XML documentation for most public API members.

**Planned:**
- Full deferred lighting.
- Textures (Color, Normal, Specular/Metallic).
- PBR materials with said textures.
- Transparency/Translucency.
- Voxel Global Illumination.

---
