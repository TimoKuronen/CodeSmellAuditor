# Engineering Standard (universal)

**Version:** 1.0  
**Scope:** All projects, all platforms, all agents.  
**Purpose:** Stable defaults so you do not re-decide architecture on every new repo.

---

## 1. There are no golden rules — there are stable defaults

Senior architects disagree on **tactics** (interfaces everywhere vs YAGNI, microservices vs monolith, ECS vs OOP). They largely agree on **goals**:

| Goal | What it means in practice |
|------|---------------------------|
| **Change should be cheap** | Most edits stay inside one module |
| **Behavior should be verifiable** | Important logic can be tested without manual clicking |
| **Dependencies should be visible** | You can draw a diagram of who calls whom |
| **Failures should be loud and early** | Wiring mistakes surface at startup/build, not silently in prod |
| **Code should be readable** | A new teammate understands intent without archaeology |

This document does **not** claim one true architecture. It defines:

1. **Stable defaults** — follow unless you document why not  
2. **Explicit exceptions** — Rule of Three, platform packs, project overlays  
3. **Precedence** — when docs conflict, which one wins  

If a rule here feels wrong for a specific line of code, use §10 (Exception process). Do not invent ad hoc exceptions in chat.

---

## 2. Document hierarchy (precedence)

Read and apply in this order. **Higher tier wins** when they conflict.

| Tier | Document | Location (WebCasuals) |
|------|----------|------------------------|
| **0 — Core** | This file | `docs/ENGINEERING_STANDARD.md` |
| **1 — Platform** | Unity platform standard | `docs/UNITY_PLATFORM_STANDARD.md` |
| **2 — Project** | Project agent guide | `<Project>/AGENTS.md` |
| **3 — Product** | GDD, mechanics, tuning | `docs/<game>/GDD.md`, etc. |

**Agent rules (`.cursor/rules/`):** Short extracts of tiers 0–1. They point here; they do not duplicate long prose.

**Product docs (tier 3)** define *what* to build. **Engineering docs (tiers 0–2)** define *how* to structure code. Never let GDD override dependency direction or testability seams.

---

## 3. The 80/20 partition

Not every file deserves the same design. Classify before you refactor.

### 80% — Domain, state, control flow

Business rules, session state, orchestration, config interpretation, save/load policy, network message handling.

**Optimize for:** readability, testability, change isolation.  
**Techniques:** plain classes, constructor injection, small side-effect ports, explicit state machines.

### 20% — Performance hot paths

Tight loops, mass transforms, spatial queries, per-frame simulation at scale, serialization hot paths.

**Optimize for:** throughput, allocation, cache locality.  
**Techniques:** flat data, struct buffers, pooled memory, minimal indirection — **only after profiling or known budget**.

**Default:** New code starts in the **80%** style. Promote to 20% only with evidence (profiler, frame budget doc, design target).

### Classification checklist (agents)

Ask in order:

1. Does this run every frame on >100 entities? → candidate for 20%  
2. Is allocation or cache miss the stated bottleneck? → candidate for 20%  
3. Otherwise → **80%**  

---

## 4. Core pillars

### Pillar A — State isolation

- Prefer **pure functions** for calculations: explicit inputs → explicit outputs.  
- **Mutate state in one place** per concern (single writer).  
- **Readers** use read models or queries; do not reach into another module's private fields.  
- Cross-module reactions use **notifications** (events); cross-module **commands** use injected services.

**Command vs notification**

| Kind | Purpose | May mutate core state? | Example |
|------|---------|------------------------|---------|
| **Command** | Apply a business decision | Yes, in the owning service | `scoreService.RegisterKill(context)` |
| **Notification** | Tell others something happened | No — react only | `ScoreChanged` event → HUD refresh |

Domain logic completes **before** notifications fire. Subscribers must not change the outcome of the command they observe.

### Pillar B — High cohesion, low coupling

- One module = one reason to change.  
- Modules talk through **narrow public surfaces** (interfaces, small DTOs, events).  
- Internal refactor of module A must not force edits in module B.

### Pillar C — Deferred abstraction (Rule of Three)

1. First use — straightforward, concrete.  
2. Second similar use — duplicate or specialize; do not abstract yet.  
3. Third similar use — extract shared abstraction.

**Exceptions — interfaces before the third use are allowed when:**

- Composition root requires a replaceable implementation (platform SDK, storage, clock).  
- Edit Mode / unit tests need a fake without a scene.  
- Two or more implementations exist today (e.g. real + null portal service).

Do **not** add interfaces for single-use private helpers.

### Pillar D — Cognitive clarity

- Linear flow beats clever indirection.  
- Names explain **what**; comments explain **why** (non-obvious only).  
- No tutorial step-by-step comments in production code.

---

## 5. Dependency rules (Clean Architecture distilled)

**Dependency Inversion:** high-level policy does not depend on low-level details. Both depend on abstractions at boundaries.

### Mandatory seams at orchestration boundaries

Types that coordinate workflows (managers, controllers, use-case handlers) **must** depend on abstractions for:

| Seam | Why |
|------|-----|
| **Time** | Deterministic tests |
| **Randomness** | Deterministic tests |
| **Persistence / IO** | No hidden globals; swappable stores |
| **External systems** | DB, network, file, platform SDK |

Concrete implementations live at the **composition root** (installer, `Program`, `LifetimeScope`, `main`).

### Side-effect ports (Example A pattern)

Domain/orchestration code receives narrow interfaces:

```csharp
// Illustrative — names vary by project
public interface ITransactionStore { void Save(Transaction t); }
public interface IAuditLog { void Write(string message); }
```

Keep ports **small**. Prefer several focused interfaces over one "god interface."

### Static globals

Avoid mutable static state in domain logic. If a global notification bus exists (platform pack), treat it as **notification only**, resettable in tests — never as the sole place state lives.

---

## 6. Testing contract

| Layer | What to test | How |
|-------|--------------|-----|
| **Domain / planners** | Rules, math, state transitions | Edit Mode / unit tests, no scene |
| **Adapters** | Mapping, serialization | Unit tests with fakes |
| **Integration** | Scene, network, end-to-end | Explicitly labeled; minimal count |

**Rules**

- Test class: `{TypeUnderTest}Tests`.  
- Arrange–Act–Assert.  
- Do not test trivial getters or framework behavior.  
- Reset global notification subscribers between tests (platform pack defines how).

**Definition of done (code change)**

- Behavior change → test for domain rule **or** documented reason why untestable (physics-only glue).  
- No new hidden singletons without a seam.

---

## 7. Production baseline

Applies to every shipped project regardless of platform.

### Errors and validation

| Boundary | Policy |
|----------|--------|
| **Composition / wiring** | Fail fast — misconfiguration throws at startup |
| **User / external input** | Validate; return typed errors — do not assume valid input |
| **Network / IO** | Timeouts, retries where appropriate; log failures with context |

### Configuration and secrets

- Secrets never in source control.  
- Environment-specific values via config layers (env vars, ScriptableObjects, appsettings — platform pack picks mechanism).  
- Document required config keys in project overlay.

### Logging

- Structured, actionable messages at system boundaries.  
- No secrets or PII in logs.  
- Log **why** a operation failed, not only that it failed.

### Dependencies

- New third-party packages require explicit approval and changelog entry in project overlay.  
- Pin versions; avoid drive-by upgrades.

### CI (minimum)

- Build compiles.  
- Domain tests run on every PR.  
- Lint/format if configured.

---

## 8. SOLID — what to actually enforce

Do not treat SOLID as religion. Use this pragmatic reading:

| Principle | Enforce when |
|-----------|--------------|
| **S** Single responsibility | Module has multiple unrelated reasons to change |
| **O** Open/closed | Plugin/ability/mod systems with known extension points |
| **L** Liskov | You use inheritance (prefer composition) |
| **I** Interface segregation | Ports become fat "do everything" interfaces |
| **D** Dependency inversion | Orchestrators, cross-cutting services, platform boundaries |

**Most code:** S + D matter daily. O/L/I matter at extension boundaries.

---

## 9. Readability and clean code (universal)

- One primary type per file; file name matches type.  
- Consistent naming within the repo (platform pack defines casing).  
- Functions do one thing at one abstraction level.  
- Prefer explicit over implicit (clear types over `dynamic` / excessive `var` where clarity suffers).  
- Delete dead code; do not comment it out.  
- Match surrounding style in the folder you edit.

---

## 10. Exception process

When you break a default in this document:

1. **Scope it** — one module or one hot path, not the whole repo.  
2. **Document it** — one sentence in code comment or project overlay: *"20% hot path: flat loop per ENGINEERING_STANDARD §3."*  
3. **Do not contradict tier 0** without lead approval (e.g. no untestable core economy because "it's faster to ship").

Agents: if user message says **prototype / throwaway / quick fix only**, minimal rules apply; state that in the PR or commit message.

---

## 11. What this document intentionally excludes

- Game design, pacing, monetization (tier 3 product docs).  
- Engine-specific APIs (tier 1 platform standard).  
- Company HR, review process, branching model (team process doc if needed).

---

## 12. References (origins)

- Robert C. Martin — SOLID, Clean Architecture (dependency direction).  
- Hunt & Thomas — *The Pragmatic Programmer* (orthogonality, duplication).  
- 80/20 hybrid — internal pragmatic manual (domain vs hot path).  
- Mike Acton — data-oriented design (20% only, evidence-based).

**This file is the single universal source of truth.** Platform and project docs extend it; they do not replace it.
