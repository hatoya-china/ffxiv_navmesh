# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Dalamud plugin for Final Fantasy XIV that provides autonomous navigation and pathfinding. It builds navigation meshes from game scene geometry and enables automated character movement through zones, avoiding obstacles. The system supports both ground-based pathfinding (using Recast/Detour) and 3D flying navigation (using voxel-based pathfinding).

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build debug version
dotnet build --configuration Debug

# Build release version
dotnet build --configuration Release

# Build with specific version
dotnet build --configuration Release vnavmesh/vnavmesh.csproj -p:AssemblyVersion=1.2.3.4
```

## Development Environment

- Target Framework: .NET 10.0 (Windows)
- Platform: x64 only
- Requires Dalamud development environment
  - Windows: `%APPDATA%\XIVLauncher\addon\Hooks\dev\`
  - Linux: `$DALAMUD_HOME/`
- Uses git submodules (DotRecast library)

## Core Architecture

### Plugin Lifecycle
- **Entry Point**: `Plugin.cs` implements `IDalamudPlugin`
- **Initialization**: Sets up NavmeshManager, FollowPath, AsyncMoveRequest, UI systems
- **Update Loop**: Driven by Dalamud's Framework.Update event
- **Commands**: `/vnav` and `/vnavmesh` for user interaction

### Navmesh Management (`NavmeshManager.cs`)
Central orchestrator that:
- Detects zone changes by monitoring territory, layout state, and festival layers
- Manages async mesh loading/building pipeline
- Handles caching in `meshcache/` directory (Brotli compressed)
- Queues pathfinding requests (single concurrent task)
- Exposes `QueryPath()` for pathfinding and `OnNavmeshChanged` event

### Navmesh Data Structure (`Navmesh.cs`)
- `DtNavMesh Mesh`: Ground navigation (from DotRecast)
- `VoxelMap? Volume`: Optional 3D voxel map for flying
- `CustomizationVersion`: For cache invalidation
- Binary serialization with magic `0x444D564E` and version 22

### Pathfinding (`NavmeshQuery.cs`)
- `PathfindMesh()`: Ground pathfinding using DotRecast's DtNavMeshQuery
- `PathfindVolume()`: Flying pathfinding using custom voxel-based A*
- Query utilities: FindNearestMeshPoly, FindPointOnFloor, FindReachableMeshPolys

### Mesh Building Pipeline (`NavmeshBuilder.cs`)
1. Extract `SceneDefinition` from game scene
2. Apply pre-build customizations
3. Divide world into tiles (default 4x4 grid)
4. Build tiles in parallel: Voxelize → Heightfield → Contours → PolyMesh → DetailMesh
5. Apply post-build customizations
6. Optionally build voxel volume for flying
7. Save to cache

### Customization System (`NavmeshCustomization.cs`)
Per-territory customizations using registry pattern:
- Auto-discovered via reflection
- Mapped using `[CustomizationTerritory(territoryID)]` attribute
- Three hooks:
  - `CustomizeScene()`: Modify geometry before building
  - `CustomizeSettings()`: Adjust Recast parameters
  - `CustomizeMesh()`: Post-process final mesh (add off-mesh connections, etc.)
- Located in `vnavmesh/Customizations/` directory (40+ zone-specific files)

### Movement System (`Movement/`)
- `FollowPath.cs`: Core controller that advances along waypoint queue each frame
- `OverrideMovement.cs`: Injects movement input into game
- `OverrideCamera.cs`: Optionally aligns camera to movement direction
- `OverrideAfk.cs`: Prevents AFK timeout
- Publishes shared state via Dalamud's shared data cache

### IPC/API (`IPCProvider.cs`)
Exposes functionality to other plugins via Dalamud IPC with namespace `vnavmesh.`:
- `Nav.*`: Navmesh management (IsReady, BuildProgress, Reload, Rebuild, Pathfind)
- `Query.Mesh.*`: Mesh queries (NearestPoint, PointOnFloor)
- `Path.*`: Movement control (MoveTo, Stop, IsRunning, SetTolerance)
- `SimpleMove.*`: High-level movement (PathfindAndMoveTo)
- `Window.*`: UI control
- `DTR.*`: Status bar control

### Voxel-Based Flying (`NavVolume/`)
- `VoxelMap.cs`: Hierarchical voxel grid (octree-like structure)
- `VoxelPathfind.cs`: 3D A* pathfinding in voxel space
- `Voxelizer.cs`: Converts geometry to voxels
- `VoxelStraighten.cs`: Path optimization

### Scene Extraction (`SceneExtractor.cs`)
Extracts collision geometry from game scene:
- Mesh types: Terrain, FileMesh, CylinderMesh, AnalyticShape
- Primitive flags: ForceUnwalkable, FlyThrough, Unlandable, ForceWalkable, Fishable
- Supports instance transforms and bounds

## Key Patterns

- **Async/Await**: Uses Dalamud's `Framework.Run()` to ensure tasks complete on main thread
- **Cancellation Tokens**: Linked cancellation for clean shutdown
- **Registry Pattern**: Auto-discovery of customizations via reflection
- **Lazy Initialization**: Voxel maps only built for flyable zones
- **Tile-Based Streaming**: Navmesh divided into tiles for parallel building
- **Cache Invalidation**: Version tracking prevents stale cache usage
- **Event-Driven Updates**: Framework update loop drives all systems

## Adding Zone Customizations

1. Create new file in `vnavmesh/Customizations/` named `Z{territoryID}{ZoneName}.cs`
2. Inherit from `NavmeshCustomization`
3. Add `[CustomizationTerritory(territoryID)]` attribute
4. Override one or more hooks:
   - `CustomizeScene()`: Add/remove colliders before building
   - `CustomizeSettings()`: Adjust mesh building parameters
   - `CustomizeMesh()`: Add off-mesh connections or modify final mesh
5. Use helper methods: `InsertAABoxCollider()`, `InsertCylinderCollider()`, `AddOffMeshConnection()`

## External Dependencies

- **DotRecast**: Recast & Detour C# port (git submodule in `DotRecast/`)
- **Dalamud**: FFXIV plugin framework (referenced from DalamudLibPath)
- **FFXIVClientStructs**: Game memory structures
- **Lumina**: FFXIV data file access
- **SharpDX**: DirectX rendering

## Publishing

- Main branch: `master`
- Development branch: `cn-dev`
- Releases triggered by pushing tags matching `v*.*.*.*`
- GitHub Actions workflow builds and publishes to Dynamis plugin repository
- Plugin ID: 48
