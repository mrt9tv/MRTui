# FuelCalculatorService Refactoring Roadmap

**Branch**: `refactor/modular-architecture`  
**Status**: 🔄 IN PROGRESS  
**Started**: December 4, 2025  
**Last Updated**: December 4, 2025

---

## Overview

Refactoring `FuelCalculatorService` from a 3,178-line monolith to a clean ~500-line orchestrator with modular sub-services.

### Current State (After Phase 4)

| Component | Location | Lines | Status |
|-----------|----------|-------|--------|
| `FuelCalculatorService` | `Services/` | **1,848** | 🟡 Phase 4 complete (-1,330 lines, 42% reduced) |
| `FuelAveragingService` | `Services/Fuel/` | 259 | ✅ Extracted & called |
| `FuelOutlierDetector` | `Services/Fuel/` | 293 | ✅ Extracted & called |
| `PitStrategyService` | `Services/Fuel/` | 527 | ✅ Extracted & wired (Phase 1) |
| `FuelSavingCalculator` | `Services/Fuel/` | 211 | ✅ Extracted & called |
| `DeltaTrackingService` | `Services/Fuel/` | 278 | ✅ Extracted & wired |
| `DynamicBufferCalculator` | `Services/Fuel/` | 214 | ✅ Extracted & wired |
| `LapDeltaTracker` | `Services/Fuel/` | 134 | ✅ Extracted & called |

---

## Duplicate Methods - Status

These methods in `FuelCalculatorService` were **duplicated** with extracted services:

| Method | Original Line | ~Lines | Service Replacement | Status |
|--------|---------------|--------|---------------------|--------|
| `CalculateAverages()` | 869 | 135 | `FuelAveragingService` | ✅ Keep (orchestrates) |
| `CalculateStrategy()` | 1004 | 99 | None (unique logic) | ✅ Keep as-is |
| ~~`CalculateDeltaTracking()`~~ | ~~1112~~ | ~~261~~ | `DeltaTrackingService` | ✅ REMOVED (Phase 3) |
| ~~`CalculateFuelSaving()`~~ | ~~1364~~ | ~~184~~ | `FuelSavingCalculator` | ✅ REMOVED (Phase 4) |
| ~~`CalculateOptimalPitLap()`~~ | ~~1727~~ | ~~303~~ | `PitStrategyService` | ✅ REMOVED (Phase 1) |
| ~~`CalculatePitExitPosition()`~~ | ~~2058~~ | ~~172~~ | `PitStrategyService` | ✅ REMOVED (Phase 1) |
| ~~`CalculateMultiStopStrategy()`~~ | ~~2231~~ | ~~137~~ | `PitStrategyService` | ✅ REMOVED (Phase 1) |
| ~~`CalculatePartialRefuelOptimization()`~~ | ~~2368~~ | ~~186~~ | `PitStrategyService` | ✅ REMOVED (Phase 1) |
| ~~`CalculateDynamicBufferLaps()`~~ | ~~1600~~ | ~~153~~ | `DynamicBufferCalculator` | ✅ REMOVED (Phase 2) |

**Lines removed**: 1,330 lines (Phases 1-4)
**Current size**: 1,848 lines (target: ~500)

---

## Refactoring Phases

### ✅ Phase 0: Bug Fixes (COMPLETE)
- [x] Grid start lap detection - exclude partial first laps from averages
- [x] Commit: `953d98e`

### ✅ Phase 1: Wire Up PitStrategyService (COMPLETE)
**Goal**: Use `PitStrategyService` instead of inline methods

**Tasks**:
- [x] Update `Update()` to call `_pitStrategyService.Calculate()`
- [x] Add `ApplyPitStrategyToCurrentData()` helper to map results
- [x] Pass `SessionPersistenceService` to constructor for pit time predictions
- [x] Remove inline methods:
  - [x] `CalculateOptimalPitLap()` (~303 lines)
  - [x] `CalculatePitExitPosition()` (~172 lines)
  - [x] `CalculateMultiStopStrategy()` (~137 lines)
  - [x] `CalculatePartialRefuelOptimization()` (~186 lines)
- [x] Build verified: 0 errors, 0 warnings

**Result**: 733 lines removed (3179 → 2446)
**Commit**: `22b4366`

### ✅ Phase 2: Remove Dead DynamicBufferCalculator Code (COMPLETE)
**Goal**: Remove inline method - service already wired

**Tasks**:
- [x] Verified `_bufferCalculator.Calculate()` already called in Update()
- [x] Removed `CalculateDynamicBufferLaps()` (153 lines, never called = dead code)
- [x] Build verified

**Result**: 153 lines removed (2,446 → 2,293)

### ✅ Phase 3: Remove Dead DeltaTracking Code (COMPLETE)
**Goal**: Remove inline `CalculateDeltaTracking()` - never called = dead code

**Tasks**:
- [x] Searched for call sites - found only definition at line 1112
- [x] Removed `CalculateDeltaTracking()` (261 lines)
- [x] Build verified

**Result**: 261 lines removed

### ✅ Phase 4: Remove Dead FuelSaving Code (COMPLETE)
**Goal**: Remove inline `CalculateFuelSaving()` - never called = dead code

**Tasks**:
- [x] Searched for call sites - found only definition at line 1364
- [x] Removed `CalculateFuelSaving()` (184 lines)
- [x] Build verified

**Result**: 184 lines removed
**Combined Commit (Phase 2-4)**: `009a28b` - 598 lines removed (2,446 → 1,848)

### 🔄 Phase 5: Final Cleanup (IN PROGRESS)
**Goal**: Clean orchestrator, remove dead code, approach 500 lines

**Tasks**:
- [x] Identified unused fields via build warnings: `_lastDelta`, `_sessionStatsLoaded`, `_currentCarClassId`
- [ ] Remove unused private fields
- [ ] Review `Update()` method for simplification
- [ ] Remove any remaining dead code paths
- [ ] Clean up overly complex helper methods
- [ ] Run full test suite
- [ ] Verify all widgets still work

**Target**: `FuelCalculatorService` ≤ 500 lines (currently: 1,848)

---

## Success Criteria

### Technical
- [ ] `FuelCalculatorService` reduced to ~500 lines (from 3,178)
- [ ] All services properly integrated
- [ ] Build passes with 0 errors, 0 warnings
- [ ] Public API unchanged

### Functional
- [ ] Fuel averaging works correctly
- [ ] Pit strategy calculations match previous behavior
- [ ] Fuel saving mode works correctly
- [ ] Delta tracking displays correctly
- [ ] Grid start laps excluded from averages

### Quality
- [ ] Each service follows Single Responsibility Principle
- [ ] Clear separation of concerns
- [ ] Improved testability

---

## Rollback Plan

If issues are found:
1. Revert to `master` branch (commit `e8e660e`)
2. Cherry-pick bug fixes (grid start lap fix)
3. Investigate failing functionality

---

## Commits

| Commit | Phase | Description |
|--------|-------|-------------|
| `953d98e` | 0 | Grid start lap detection fix |
| `22b4366` | 1 | Wire up PitStrategyService, remove 733 lines |
| `009a28b` | 2-4 | Remove dead code methods, 598 lines |
| - | 5 | Final cleanup (in progress) |

---

**Last Updated**: December 4, 2025
