# FuelCalculatorService Refactoring Roadmap

**Branch**: `refactor/modular-architecture`  
**Status**: ✅ PHASE 6 COMPLETE  
**Started**: December 4, 2025  
**Last Updated**: December 6, 2025

---

## Overview

Refactoring `FuelCalculatorService` from a 3,178-line monolith to a clean ~500-line orchestrator with modular sub-services.

### Current State (After Phase 6)

| Component | Location | Lines | Status |
|-----------|----------|-------|--------|
| `FuelCalculatorService` | `Services/` | **1,538** | ✅ Phase 6 complete (-1,640 lines, 52% reduced) |
| `FuelAveragingService` | `Services/Fuel/` | 258 | ✅ Extracted & called |
| `FuelOutlierDetector` | `Services/Fuel/` | 293 | ✅ Extracted & called |
| `PitStrategyService` | `Services/Fuel/` | 526 | ✅ Extracted & wired (Phase 1) |
| `FuelSavingCalculator` | `Services/Fuel/` | 210 | ✅ Extracted & called |
| `DeltaTrackingService` | `Services/Fuel/` | 278 | ✅ Extracted & wired |
| `DynamicBufferCalculator` | `Services/Fuel/` | 214 | ✅ Extracted & wired |
| `LapDeltaTracker` | `Services/Fuel/` | 134 | ✅ Extracted & called |
| `PitStopTracker` | `Services/Fuel/` | 203 | ✅ Extracted (Phase 6) |
| `LiveFuelCalculator` | `Services/Fuel/` | 173 | ✅ Extracted (Phase 6) |

---

## Dead Code Removed

| Item | Type | Lines | Status |
|------|------|-------|--------|
| `CalculateDeltaTracking()` | Method | 261 | ✅ REMOVED |
| `CalculateFuelSaving()` | Method | 184 | ✅ REMOVED |
| `CalculateOptimalPitLap()` | Method | 303 | ✅ REMOVED |
| `CalculatePitExitPosition()` | Method | 172 | ✅ REMOVED |
| `CalculateMultiStopStrategy()` | Method | 137 | ✅ REMOVED |
| `CalculatePartialRefuelOptimization()` | Method | 186 | ✅ REMOVED |
| `CalculateDynamicBufferLaps()` | Method | 153 | ✅ REMOVED |
| `GenerateStrategicAlerts()` | Method | 85 | ✅ REMOVED |
| `GetDeltaHistory()` | Method | 4 | ✅ REMOVED |
| `_deltaHistory` | Field | 2 | ✅ REMOVED |
| `_virtualLapsCompleted` | Field (buggy) | 1 | ✅ REMOVED |
| `_lastDelta`, `_sessionStatsLoaded`, etc. | Fields | 5 | ✅ REMOVED |
| `DeltaHistoryRecord.cs` | Model class | 42 | ✅ REMOVED |
| Duplicate `FuelAverages` creation | Code dup | 10 | ✅ REMOVED |
| `UpdateLiveValues()` | Method | 107 | ✅ EXTRACTED (Phase 6) |
| `UpdatePitStopTracking()` | Method | 136 | ✅ EXTRACTED (Phase 6) |

**Total lines removed/extracted**: 1,640 lines (Phases 1-6)
**Current size**: 1,538 lines (target: ~500-800)

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

### ✅ Phase 6: Advanced Cleanup (COMPLETE)
**Goal**: Further reduce through method extraction

**Completed Tasks**:
- [x] Extract `UpdateLiveValues()` to `LiveFuelCalculator` service (107 lines)
- [x] Extract `UpdatePitStopTracking()` to `PitStopTracker` service (136 lines)
- [x] Wire up new services in constructor
- [x] Replace method calls with service calls
- [x] Build verified: 0 errors, 0 warnings

**Not Pursued** (deemed unnecessary after analysis):
- OnLapCompleted() - Complex but well-structured, not worth extracting
- Update() method - Main orchestrator, should remain in FuelCalculatorService

**Result**: 243 lines removed/extracted (1,745 → 1,538)
**Status**: ✅ Complete - 52% reduction achieved (3,178 → 1,538)

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
| `6859c4c` | 6 | Fix _virtualLapsCompleted bug, remove dead code |

---

## Final Summary

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Lines | 3,178 | 1,538 | -1,640 (52%) |
| Warnings | 4 | 0 | ✅ Clean |
| Dead Methods | 9 | 0 | ✅ Removed |
| Dead Models | 1 | 0 | ✅ Removed |
| Services Wired | 2 | 8 | ✅ All (Phase 6: +2) |
| Bugs Fixed | 0 | 1 | ✅ _virtualLapsCompleted |

---

## Next Refactoring Opportunities

### ⏸️ Recommended: Pause Major Refactoring
**Phase 6 has achieved excellent results** - 52% reduction (3,178 → 1,538 lines)

**Remaining large methods** (in descending complexity):
1. **OnLapCompleted** (~129 lines) - Lap history tracking with complex validation logic
   - **Recommendation**: Keep as-is - well-structured with clear sections
   - Extraction would create awkward dependencies on many fields
   
2. **CalculateAverages** (~135 lines) - Fuel averaging with outlier detection
   - **Recommendation**: Keep as-is - mostly uses services already
   - Further extraction provides diminishing returns
   
3. **CalculateStrategy** (~105 lines) - Race strategy calculation
   - **Status**: Already uses PitStrategyService for heavy lifting
   - This method mostly orchestrates and applies results

### Phase 7: Optional Fine-Tuning (COMPLETED) ✅
**Completed**: December 2025

Further extraction to improve testability and single responsibility:

1. ✅ **LapValidator** service - Extracted lap validation logic from OnLapCompleted
   - Created `LapValidator.cs` (145 lines)
   - Handles formation/pace/pit/out-lap/grid-start detection
   - Result: OnLapCompleted reduced from 130 → 30 lines
   - Commit: `58e791e`

2. ✅ **TemperatureCompensationService** - Extracted temperature correction logic
   - Created `TemperatureCompensationService.cs` (95 lines)
   - Handles ±10% fuel correction based on air temp delta
   - Result: ApplyTemperatureCorrection simplified to delegation
   - Commit: `f90fab3`

3. ✅ **Fuel Pressure Cleanup** - Removed dead code (not available in iRacing SDK)
   - Deleted commented-out fields, method calls, and UpdateFuelPressureTracking method
   - Result: 58 line cleanup
   - Commit: `eaa44f6`

**Phase 7 Results**:
- **Before**: 1,550 lines (Phase 6 result)
- **After**: 1,309 lines
- **Reduction**: 241 lines (15.5% improvement over Phase 6)
- **Total Reduction from Original**: 3,178 → 1,309 = **58.8% reduction** ✨

**Benefits**:
- Improved testability (validation logic isolated)
- Better single responsibility adherence
- Cleaner codebase with dead code removed
- Enhanced maintainability

### Testing Priority
Before further refactoring:
- [ ] Run full test suite
- [ ] Verify all widgets still work
- [ ] Test fuel averaging calculations match previous behavior
- [ ] Test pit strategy calculations
- [ ] Test delta tracking
- [ ] Test fuel saving mode

---

**Last Updated**: December 6, 2025
