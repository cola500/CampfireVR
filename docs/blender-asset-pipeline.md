---
title: Blender Asset Pipeline
description: Workflow for creating original stylized Quest-friendly companion assets in Blender via the Blender MCP server and importing them into CampfireVR through Unity.
category: workflow
status: active
last_updated: 2026-08-21
sections: [Goal, Pipeline, Lessons learned, Folder structure recommendation, Checklist for every new asset]
---

# Blender Asset Pipeline for CampfireVR

## Goal

Describe how we create original stylized Quest-friendly assets using Blender MCP and Unity.

## Pipeline

```
Concept
  ↓
Blender MCP blockout
  ↓
Mesh refinement
  ↓
Unity import
  ↓
Material assignment
  ↓
Quest validation
  ↓
Animation
  ↓
Prefab
```

## Lessons learned

### 1. Start with a blockout

Do not chase perfection.

Focus on:
- silhouette
- proportions
- personality
- Quest scale

### 2. Validate early in Unity

Import the model before spending hours polishing.

VR reveals proportion issues much earlier than Blender renders.

### 3. Keep materials simple

Use project-native Unity materials.

Avoid relying on imported Blender materials until necessary.

### 4. Blender render rig

Important discovery:

The Blender file contained:
- `Key_Sun`
- `Fill_Area`
- Cameras

Unity imported those, making the dinosaur appear self-lit.

Permanent recommendation:
- Import Lights = OFF
- Import Cameras = OFF

or keep render rigs in separate Blender scenes/collections.

### 5. Quest optimization

Current target:
- 2k–4k triangles
- one material
- simple topology
- clean hierarchy
- no unnecessary shaders

### 6. Character philosophy

CampfireVR companions should feel:
- cozy
- friendly
- stylized
- low-poly
- calm
- believable

They are companions, not gameplay mechanics.

### 7. Behaviour philosophy

Prefer subtle idle behaviours:
- breathing
- blinking
- looking around
- occasional attention

Avoid:
- AI
- pathfinding
- complex behaviour trees

### 8. Future companions

Potential additions:
- Fox
- Owl
- Turtle
- Hedgehog
- More dinosaurs

Reuse a shared `CompanionIdleBehaviour` where possible.

## Folder structure recommendation

```
Assets/
    Art/
        Blender/
        FBX/
        Materials/
        Prefabs/
        Textures/
```

## Checklist for every new asset

- [ ] Blender scene clean
- [ ] Apply transforms
- [ ] No imported lights
- [ ] No imported cameras
- [ ] Unity material created
- [ ] Quest test completed
- [ ] Performance acceptable
- [ ] Changelog updated
