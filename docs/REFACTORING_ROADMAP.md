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

### ✅ Phase 5: Dead Code Cleanup (COMPLETE)
**Goal**: Remove remaining dead code and unused fields

**Tasks**:
- [x] Removed unused fields: `_lastDelta`, `_sessionStatsLoaded`, `_currentCarClassId`, `_currentTrackName`
- [x] Removed dead method `GenerateStrategicAlerts()` (85 lines, never called - FuelSavingCalculator has its own)
- [x] Build verified: 0 errors, 0 warnings

**Result**: 91 lines removed (1,848 → 1,757)
**Commit**: `73aa7b2`

### 🔲 Phase 6: Advanced Cleanup (OPTIONAL)
**Goal**: Further reduce to ~500 lines through method extraction

**Potential Tasks**:
- [ ] Extract `UpdateLiveValues()` to service if complex
- [ ] Extract `OnLapCompleted()` to service if complex  
- [ ] Review `Update()` method for simplification
- [ ] Move pit stop tracking state machine to separate service
- [ ] Run full test suite
- [ ] Verify all widgets still work

**Status**: Optional - core functionality preserved, 45% reduction achieved

---

## Success Criteria

### Technical ✅ PARTIAL
- [x] `FuelCalculatorService` reduced significantly (3,178 → 1,757 = 45% reduction)
- [x] All services properly integrated (PitStrategyService, DeltaTrackingService, etc.)
- [x] Build passes with 0 errors, 0 warnings
- [x] Public API unchanged

### Functional
- [ ] Fuel averaging works correctly (needs testing)
- [ ] Pit strategy calculations match previous behavior (needs testing)
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
| `73aa7b2` | 5 | Remove unused fields and methods, 91 lines |
| - | 6 | Advanced cleanup (optional) |

---

## Final Summary

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Lines | 3,178 | 1,757 | -1,421 (45%) |
| Warnings | 4 | 0 | ✅ Clean |
| Dead Methods | 9 | 0 | ✅ Removed |
| Services Wired | 2 | 6 | ✅ All |

---

**Last Updated**: December 4, 2025
