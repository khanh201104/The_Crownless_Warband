---
trigger: always_on
---

# Flash Model Coding Protocol (Strict Execution Mode)

When generating or editing C# code for **The Crownless Warband**, you MUST obey these execution constraints to prevent errors:

## 1. Small, Atomic File Generation
- NEVER modify more than 3 scripts in a single step unless explicitly instructed by a specification file.
- Before writing code, list the exact file paths and namespaces you will touch.
- Wait for user confirmation or compile check if a task spans across both `CrownlessWarband.Core` and `CrownlessWarband.Presentation`.

## 2. Exact C# Signatures & Conventions
- **Namespaces MUST match folder structure:**
  - `CrownlessWarband.Core.Battle`
  - `CrownlessWarband.Core.AI`
  - `CrownlessWarband.Core.Events`
  - `CrownlessWarband.Data`
  - `CrownlessWarband.Presentation.BattleView`
  - `CrownlessWarband.Presentation.UI`
- **No LINQ in tight loops if simple `for`/`foreach` works**, and always `using System; using System.Collections.Generic;`.
- **Every AI Evaluation MUST output an `AIDecisionRecord` or score breakdown** (never return just a raw `UnitState` without logging the score components for F3 AI Debug Mode).

## 3. Core vs Presentation Boundary
- In `Scripts/Core/`: Strictly NO `MonoBehaviour`, NO `ScriptableObject`, NO `Debug.Log` (use `BattleState.AppendLog()` or `CombatEvent` instead).
- In `Scripts/Data/`: Only `ScriptableObject` definitions that convert to or feed pure data structs into `Core`.
- In `Scripts/Presentation/`: Only `MonoBehaviour` classes that read `BattleState` and play animations from `CombatEventQueue`.