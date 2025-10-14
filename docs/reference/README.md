# Reference Documentation

This folder contains important historical documents, phase completion reports, and architectural decisions for the iRacing Telemetry Overlay project.

## 📁 Folder Purpose

This is the **archive for completed phase documentation** and **important technical decisions**. These files provide context for how the project evolved but are not actively maintained.

For current development status, see:
- `/docs/phases/iracing_dev_phases.md` - Main roadmap and current progress
- `/docs/phases/MVP1_Basic_Connection.md` through `MVP6_Advanced_Config.md` - Detailed MVP documentation
- `/PROJECT_STATUS.md` - Quick project overview

## 📄 Document Categories

### MVP Completion Reports
- `MVP1_COMPLETION_REPORT.md` - Initial console telemetry app (January 12, 2025)
- `MVP2_ARCHITECTURE_DECISION.md` - Decision to use WPF instead of React web UI
- `MVP2_KICKOFF.md`, `MVP2_PROGRESS.md`, `MVP2_PROGRESS_UPDATE.md` - MVP2 development process
- `MVP2_HYBRID_ARCHITECTURE_COMPLETE.md` - Hybrid WPF + core architecture finalization

### Phase Completion Documents
- `PHASE_2_COMPLETE.md` - Basic overlay window completion
- `PHASE_3_COMPLETE.md` - First widget implementation
- `PHASE_4_COMPLETE.md` - Multi-widget system
- `PHASE_5_POLISH_COMPLETE.md` - UI polish and refinement
- `PHASE_6_FIXES.md`, `PHASE_6_FIXES_FINAL.md` - Phase 6 bug fixes
- `PHASE_6_YAML_PARSER_COMPLETE.md` - YAML parsing implementation
- `PHASE_7_UI_REDESIGN.md` - Immediate-apply UI pattern (removed Apply buttons)
- `PHASE_7.5_DATAWIDGET_FIXES.md` - DataWidget enhancements (color coding, dynamic sizing)

### Technical Summaries
- `FIXES_AND_MVP2_PREP.md` - Pre-MVP2 bug fixes and preparation
- `QUICK_FIX_SUMMARY.md` - Quick fix documentation
- `TELEMETRY_FIXES_SUMMARY.md` - Telemetry service fixes
- `WPF_STARTUP_FIX.md` - WPF initialization fixes
- `WIDGET_ARCHITECTURE_PROPOSAL.md` - Original widget system design proposal

## 🔍 When to Reference These Files

Use these documents when:
- Understanding the **history** of a design decision
- Researching **how a problem was previously solved**
- Reviewing **lessons learned** from past phases
- Writing **architectural decision records (ADRs)** for new features
- Creating **onboarding documentation** for new contributors

## ⚠️ Important Notes

- **These files are historical records** - Do not update them
- **For current status**, always check `/docs/phases/iracing_dev_phases.md`
- **For active tasks**, check the TODO list or phase tracking documents
- **New phase completions** should be added here when phases finish

## 📊 Project Timeline

| Date | Milestone | Document |
|------|-----------|----------|
| January 12, 2025 | MVP 1 Complete | `MVP1_COMPLETION_REPORT.md` |
| January 2025 | MVP 2 Complete | `MVP2_HYBRID_ARCHITECTURE_COMPLETE.md` |
| February 2025 | MVP 3-5 Complete | `PHASE_3_COMPLETE.md` through `PHASE_5_POLISH_COMPLETE.md` |
| February 2025 | Phase 6 Complete | `PHASE_6_YAML_PARSER_COMPLETE.md` |
| February 2025 | Phase 7 Complete | `PHASE_7_UI_REDESIGN.md` |
| February 2025 | Phase 7.5 Complete | `PHASE_7.5_DATAWIDGET_FIXES.md` |

Last Updated: October 13, 2025
