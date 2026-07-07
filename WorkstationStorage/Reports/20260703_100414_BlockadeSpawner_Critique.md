---
TargetFile: BlockadeSpawner.cs
AuditDate: 2026-07-03 10:04:14
Tags: [architect-1, code-smell-audit]
---

# Audit Interrupted (Token Limit Hit)

The model ran out of token space before printing the final markdown report. However, here is its extracted analysis trace:

The user wants a static analysis audit report for the file `BlockadeSpawner.cs`. I need to evaluate this code against the provided engineering standards (`ENGINEERING_STANDARD.md`, `UNITY_PLATFORM_STANDARD.md` via csharp-style.mdc).

**Key Standards & Rules:**
1.  **Tier 0 (Engineering Standard):**
    *   **80/20 Partition:** Domain/control flow = plain classes, explicit deps; Hot paths only for optimization. This looks like a domain controller/spawner logic, likely fits the "Domain" category unless profiling shows it's hot path heavy. It has loops and state management but isn't raw physics simulation per frame (it ticks).
    *   **State:** One writer per concern; commands via services; notifications via events/bus — subscribers do not change command outcomes. This class manages `activeBlockades` list, which is a single source of truth for the spawner's internal state. It raises an event (`EventBus<BlockadeSpawnedEvent>.Raise`).
    *   **Rule of Three:** Abstractions should only be introduced when needed (interfaces). Here it implements `ITickable`, `IBlockadeSpawner`. This is likely correct as these are seams for DI and testing.
    *   **Orchestrators depend on abstractions** for time, random, persistence, external IO. It uses `RunClock` (time), `IRandom` (randomness). Good. Concrete implementations at composition root only? Yes (`BlockadeSpawner` constructor takes them as dependencies).
    *   **Tests:** Domain rules get Edit Mode/unit tests; `{TypeUnderTest}Tests`; AAA layout. This class is domain logic, so it should be testable without a scene (mostly true, though Unity refs exist in `Tick`).
    *   **Production:** No secrets; validate input; fail fast on wiring only. It uses config objects (`BlockadeSpawnConfig`, etc.) which are likely ScriptableObject or injected configs.

2.  **Tier 1 (Unity C# Style):**
    *   `[SerializeField] private` for MonoBehaviours? This class is NOT a MonoBehaviour! It's an `ITickable`. Wait, it implements interfaces but doesn't inherit from MonoBehaviour directly in the snippet provided (it inherits from nothing). However, looking at usage context (`Tick()` method), this might be used as a component or service. The code uses Unity types like `GameObject`, `Vector3`, `Transform` heavily inside logic that isn't necessarily per-frame update of a scene object but rather spawner logic triggered by events/ticks.
    *   **ScriptableObject assets are never mutated at runtime:** It reads from configs (`config.buildingsPerSideBeforeSpawn`). Looks compliant.
    *   **Private fields: camelCase, types/methods/properties PascalCase.** `activeBlockades` is private field (camelCase). Types like `ActiveBlockade` inside class? Yes. Methods are PascalCase. This looks okay.
    *   **Unity callbacks:** It implements `ITickable`. If this is a component in the scene, it might be called by VContainer or similar DI container during lifecycle events rather than standard Update loop. The snippet shows `Tick()` method which seems to be an interface implementation for scheduling logic (likely not per-frame update).
    *   **No unbounded Instantiate:** It uses ObjectPool (`ObjectPool<BlockadePiece>`), so it's bounded by pool size, good.
    *   **VContainer for DI:** Constructor injection used correctly. `IBlockadeSpawner` interface implemented? Yes.
    *   **Fail fast on missing refs:** No explicit null checks in the snippet (e.g., `ball.BodyPosition.z`). This is a potential risk if `BallController` isn't injected properly or accessed late, but it's passed as dependency so wiring should catch this at startup.
    *   **Do not create enum-only files:** Enums are nested? No enums seen here. Good.

**Analysis of Code Specifics vs Standards:**

1.  **Dependency Injection (DI):** The constructor takes dependencies (`BallController`, `Config`s, `RunClock`, etc.). This is good for composition root wiring.
2.  **State Management:** It holds state in a private list `activeBlockades`. Is this "one writer"? Yes, it's the spawner itself managing its own active blockades. However, `ReleasePiece` and `CleanupBehindPlayer` modify this list directly. This is acceptable for an orchestrator/service owning its internal state.
3.  **Event Bus:** It raises events (`EventBus<BlockadeSpawnedEvent>.Raise`). Standard notification pattern (Tier 0). Subscribers shouldn't change command outcomes, which holds true here as it's a spawn event.
4.  **Interfaces:** Implements `ITickable`, `IBlockadeSpawner`. This is good for DI and testing (`Edit Mode` tests can use mocks or fake clocks if needed).
5.  **Unity Integration:** It uses Unity types directly inside logic (e.g., `Vector3`, `Transform`). Since it's not a MonoBehaviour, this might be okay as long as the objects are owned by VContainer/Scene setup and passed in via dependencies where possible. However, accessing `ball.BodyPosition.z` implies direct access to another controller object. This is generally acceptable for orchestration if `BallController` is injected (it is).
6.  **Performance:** Uses ObjectPool (`ObjectPool<BlockadePiece>`), which avoids unbounded instantiation. Good compliance with "No unbounded Instantiate". It has loops in `NotifyRoadSegmentSpawned`, but these are logic checks, not per-frame physics calculations likely to be hot path unless spawning frequency is high (which it seems designed for).
7.  **Naming:** `activeBlockades` -> camelCase field. Good. Class names PascalCase (`ActiveBlockade`). Methods PascalCase. Namespace matches project structure? Yes (`GhoulSmasher`, `WebCasuals` used in imports).
8.  **Comments/Docs:** Has XML docs for class summary. No dead code visible.

**Potential Issues / Violations to Check:**
*   **Rule of Three:** Are there interfaces that should be concrete? It implements two interfaces (`ITickable`, `IBlockadeSpawner`). These look like seams (Tick interface is likely a generic tick mechanism, Spawner interface for DI). This fits the "Composition root requires replaceable implementation" rule.
*   **State Isolation:** The class holds state in memory (`activeBlockades` list) and modifies it directly via `ReleasePiece`, `CleanupBehindPlayer`. It does not use an external event bus to *change* its own internal state (it owns the command logic). This is acceptable for a service.
*   **Fail Fast:** No explicit null checks on injected dependencies in constructor or methods, relying on wiring to fail fast if missing? The code accesses `ball.BodyPosition.z`. If `BallController` isn't wired correctly, this might crash at runtime rather than startup (if it's not checked). However, since it's a dependency passed via DI (`BlockadeSpawner(...)`), the failure should happen during resolution or first access.
*   **Unity Callbacks:** It does NOT inherit from MonoBehaviour. This is fine for services in VContainer context if they are registered as singletons/components managed by container lifecycle. However, `Tick()` method suggests it's being called periodically (likely via DI timer). If this were a component attached to the scene tree, using Unity types directly (`Transform`, `Vector3`) inside logic that isn't per-frame update is fine *if* objects are owned and valid references exist at runtime.
*   **80/20 Partition:** This looks like domain orchestration (spawning logic). It's not a tight loop physics simulation, so
