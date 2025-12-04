# FuelCalculatorService Refactoring Roadmap

**Branch**: `refactor/modular-architecture`  
**Status**: 🔄 IN PROGRESS  
**Started**: December 4, 2025  

---

## Overview

Refactoring `FuelCalculatorService` from a 3,178-line monolith to a clean ~500-line orchestrator with modular sub-services.

### Current State

| Component | Location | Lines | Status |
|-----------|----------|-------|--------|
| `FuelCalculatorService` | `Services/` | 3,178 | 🔴 Needs cleanup |
| `FuelAveragingService` | `Services/Fuel/` | 259 | ✅ Extracted & called |
| `FuelOutlierDetector` | `Services/Fuel/` | 293 | ✅ Extracted & called |
| `PitStrategyService` | `Services/Fuel/` | 527 | ⚠️ Extracted, NOT wired |
| `FuelSavingCalculator` | `Services/Fuel/` | 211 | ✅ Extracted & called |
| `DeltaTrackingService` | `Services/Fuel/` | 278 | ⚠️ Extracted, partial use |
| `DynamicBufferCalculator` | `Services/Fuel/` | 214 | ⚠️ Extracted, NOT wired |
| `LapDeltaTracker` | `Services/Fuel/` | 134 | ✅ Extracted & called |

---

## Duplicate Methods to Remove

These methods in `FuelCalculatorService` are **duplicated** with extracted services:

| Method | Line | ~Lines | Service Replacement | Action |
|--------|------|--------|---------------------|--------|
| `CalculateAverages()` | 869 | 135 | `FuelAveragingService` | ⚠️ Keep (orchestrates services) |
| `CalculateStrategy()` | 1004 | 99 | None (unique logic) | ✅ Keep as-is |
| `CalculateDeltaTracking()` | 1103 | 252 | `DeltaTrackingService` | 🗑️ **REMOVE** - duplicate |
| `CalculateFuelSaving()` | 1355 | 372 | `FuelSavingCalculator` | 🗑️ **REMOVE** - duplicate |
| `CalculateOptimalPitLap()` | 1727 | 331 | `PitStrategyService` | 🗑️ **REMOVE** - duplicate |
| `CalculatePitExitPosition()` | 2058 | 173 | `PitStrategyService` | 🗑️ **REMOVE** - duplicate |
| `CalculateMultiStopStrategy()` | 2231 | 137 | `PitStrategyService` | 🗑️ **REMOVE** - duplicate |
| `CalculatePartialRefuelOptimization()` | 2368 | 185 | `PitStrategyService` | 🗑️ **REMOVE** - duplicate |
| `CalculateDynamicBufferLaps()` | 2553 | 160 | `DynamicBufferCalculator` | 🗑️ **REMOVE** - duplicate |

**Estimated lines to remove**: ~1,610 lines (50% reduction!)

---

## Refactoring Phases

### ✅ Phase 0: Bug Fixes (COMPLETE)
- [x] Grid start lap detection - exclude partial first laps from averages
- [x] Commit: `953d98e`

### 🔄 Phase 1: Wire Up PitStrategyService
**Goal**: Use `PitStrategyService` instead of inline methods

**Tasks**:
- [ ] Update `Update()` to call `_pitStrategyService.Calculate()`
- [ ] Map `PitStrategy` results to `CurrentData`
- [ ] Remove inline methods:
  - [ ] `CalculateOptimalPitLap()` (line 1727)
  - [ ] `CalculatePitExitPosition()` (line 2058)
  - [ ] `CalculateMultiStopStrategy()` (line 2231)
  - [ ] `CalculatePartialRefuelOptimization()` (line 2368)
- [ ] Test pit strategy still works correctly

**Expected reduction**: ~826 lines

### 🔲 Phase 2: Wire Up DynamicBufferCalculator
**Goal**: Use `DynamicBufferCalculator` instead of inline method

**Tasks**:
- [ ] Update `Update()` to call `_bufferCalculator.Calculate()`
- [ ] Map `BufferData` results to `CurrentData`
- [ ] Remove `CalculateDynamicBufferLaps()` (line 2553)
- [ ] Test dynamic buffer calculations

**Expected reduction**: ~160 lines

### 🔲 Phase 3: Remove Duplicate DeltaTracking
**Goal**: Remove inline `CalculateDeltaTracking()`, use service exclusively

**Tasks**:
- [ ] Verify `DeltaTrackingService.Track()` provides all needed data
- [ ] Remove `CalculateDeltaTracking()` (line 1103)
- [ ] Ensure convergence analysis still works

**Expected reduction**: ~252 lines

### 🔲 Phase 4: Remove Duplicate FuelSaving
**Goal**: Remove inline `CalculateFuelSaving()`, use service exclusively

**Tasks**:
- [ ] Verify `FuelSavingCalculator.Calculate()` provides all needed data
- [ ] Remove `CalculateFuelSaving()` (line 1355)
- [ ] Ensure fuel saving mode still works

**Expected reduction**: ~372 lines

### 🔲 Phase 5: Final Cleanup
**Goal**: Clean orchestrator, remove dead code

**Tasks**:
- [ ] Remove unused private fields
- [ ] Simplify `Update()` method to clean orchestration
- [ ] Update XML documentation
- [ ] Run full test suite
- [ ] Verify all widgets still work

**Target**: `FuelCalculatorService` ≤ 500 lines

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
| - | 1 | Wire up PitStrategyService |
| - | 2 | Wire up DynamicBufferCalculator |
| - | 3 | Remove duplicate DeltaTracking |
| - | 4 | Remove duplicate FuelSaving |
| - | 5 | Final cleanup |

---

**Last Updated**: December 4, 2025
