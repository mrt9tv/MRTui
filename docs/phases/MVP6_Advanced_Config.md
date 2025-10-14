# MVP 6: Advanced Configuration

**Duration:** 2-3 weeks (Actual: Partially complete)  
**Goal:** Advanced widget configuration and customization  
**Status:** ⏳ **IN PROGRESS** (Phase 7 & 7.5 complete)  
**Prerequisite:** MVP 1-5 must be complete  
**Note:** Focused on immediate-apply UX and field customization instead of drag-drop

---

## 🎯 What You've Built So Far

- **Immediate-apply configuration** (Phase 7: removed all Apply buttons)
- **Field-level customization** (DataWidget: 6 customizable cells)
- **Real-time visual feedback** (color coding, dynamic sizing)
- **Unit conversion system** (Metric/Imperial with live updates)
- **Widget-specific settings** (size sliders, visibility toggles, field selectors)

---

## ✅ Success Criteria (Revised for WPF Desktop)

- ✅ Widget configuration with instant updates (no Apply buttons)
- ✅ Live preview in overlay as settings change
- ⏳ Additional themes/color schemes (default theme implemented)
- ❌ Pre-built layouts (deferred - manual positioning works)
- ⏳ Export/import configuration (JSON save/load works, no UI for import)
- ✅ Immediate feedback on all setting changes
- ✅ Professional, functional UI (3-tab design)

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay.Web/
├── ClientApp/src/
│   ├── components/
│   │   ├── DragDropEditor/
│   │   │   ├── DragDropEditor.tsx
│   │   │   ├── DraggableWidget.tsx
│   │   │   └── DropZone.tsx
│   │   ├── LivePreview/
│   │   │   ├── PreviewWindow.tsx
│   │   │   └── PreviewWidget.tsx
│   │   ├── ThemeSelector/
│   │   │   ├── ThemeSelector.tsx
│   │   │   └── ThemePreview.tsx
│   │   └── LayoutPresets/
│   │       ├── PresetSelector.tsx
│   │       └── PresetCard.tsx
│   ├── themes/
│   │   ├── default.theme.json
│   │   ├── dark.theme.json
│   │   ├── light.theme.json
│   │   ├── neon.theme.json
│   │   └── minimal.theme.json
│   ├── presets/
│   │   ├── race.layout.json
│   │   ├── practice.layout.json
│   │   └── endurance.layout.json
│   └── hooks/
│       ├── useDragDrop.ts
│       └── useTheme.ts
└── Controllers/
    ├── ThemesController.cs
    └── PresetsController.cs
```

---

## 📋 Implementation Checklist

### Week 1: Drag-and-Drop + Live Preview

#### Days 1-3: Drag-and-Drop Implementation

- [ ] Research drag-and-drop libraries (react-dnd, dnd-kit)
- [ ] Install and configure chosen library
- [ ] Create DraggableWidget component
- [ ] Implement DropZone component
- [ ] Add drag preview overlay
- [ ] Handle snap-to-grid (optional)
- [ ] Test drag-and-drop functionality
- [ ] Add visual feedback during drag

#### Days 4-7: Live Preview System

- [ ] Design preview window component
- [ ] Create scaled-down overlay preview
- [ ] Implement real-time widget rendering
- [ ] Add widget resize handles
- [ ] Sync preview with actual overlay
- [ ] Add preview controls (zoom, pan)
- [ ] Test preview accuracy
- [ ] Optimize preview performance

### Week 2: Theme System

#### Days 8-10: Theme Architecture

- [ ] Design theme configuration structure
- [ ] Create theme API endpoints
- [ ] Implement theme switching logic
- [ ] Build ThemeSelector component
- [ ] Add theme preview functionality
- [ ] Create default theme
- [ ] Test theme application

#### Days 11-14: Pre-built Themes

- [ ] Design Dark theme
- [ ] Design Light theme
- [ ] Design Neon theme
- [ ] Design Minimal theme
- [ ] Create theme preview thumbnails
- [ ] Test all themes with all widgets
- [ ] Add theme export/import

### Week 3: Layout Presets + Polish

#### Days 15-17: Layout Presets

- [ ] Design Race layout preset
- [ ] Design Practice layout preset
- [ ] Design Endurance layout preset
- [ ] Create preset API endpoints
- [ ] Build PresetSelector component
- [ ] Add preset preview cards
- [ ] Test preset loading
- [ ] Create custom preset save

#### Days 18-21: Export/Import + Polish

- [ ] Implement configuration export
- [ ] Add configuration import
- [ ] Create file validation
- [ ] Add error handling
- [ ] Polish UI design
- [ ] Add animations and transitions
- [ ] Write comprehensive documentation
- [ ] Final testing and bug fixes

---

## ⏱️ Time Breakdown

| Days | Focus Area | Hours |
|------|-----------|-------|
| **1-3** | Drag-and-Drop | 12-15 |
| **4-7** | Live Preview | 16-20 |
| **8-10** | Theme Architecture | 12-15 |
| **11-14** | Pre-built Themes | 16-20 |
| **15-17** | Layout Presets | 12-15 |
| **18-21** | Export/Import + Polish | 16-20 |

**Total Estimated Hours:** 84-105 hours

---

## 🔧 Technical Details

### Drag-and-Drop with dnd-kit

```typescript
// src/components/DragDropEditor/DragDropEditor.tsx
import React, { useState } from 'react';
import {
  DndContext,
  DragEndEvent,
  DragOverlay,
  useSensor,
  useSensors,
  PointerSensor,
} from '@dnd-kit/core';
import { DraggableWidget } from './DraggableWidget';
import { DropZone } from './DropZone';
import { Widget } from '../../services/api';

export const DragDropEditor: React.FC = () => {
  const [widgets, setWidgets] = useState<Widget[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    })
  );

  const handleDragStart = (event: any) => {
    setActiveId(event.active.id);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, delta } = event;
    
    setWidgets(prev =>
      prev.map(widget =>
        widget.id === active.id
          ? {
              ...widget,
              position: {
                ...widget.position,
                x: widget.position.x + delta.x,
                y: widget.position.y + delta.y,
              },
            }
          : widget
      )
    );
    
    setActiveId(null);
  };

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
    >
      <DropZone>
        {widgets.map(widget => (
          <DraggableWidget key={widget.id} widget={widget} />
        ))}
      </DropZone>
      
      <DragOverlay>
        {activeId ? (
          <div className="drag-preview">
            Dragging {widgets.find(w => w.id === activeId)?.name}
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
};
```

### Theme Configuration Structure

```typescript
// src/types/theme.types.ts
export interface Theme {
  id: string;
  name: string;
  description: string;
  colors: {
    background: string;
    text: string;
    primary: string;
    secondary: string;
    accent: string;
    success: string;
    warning: string;
    danger: string;
  };
  fonts: {
    family: string;
    sizes: {
      small: number;
      medium: number;
      large: number;
      xlarge: number;
    };
  };
  borders: {
    radius: number;
    width: number;
    color: string;
  };
  opacity: {
    widget: number;
    overlay: number;
  };
}
```

### Example Dark Theme

```json
{
  "id": "dark",
  "name": "Dark",
  "description": "Dark theme with blue accents",
  "colors": {
    "background": "#1a1a1a",
    "text": "#ffffff",
    "primary": "#0066cc",
    "secondary": "#444444",
    "accent": "#00aaff",
    "success": "#00ff00",
    "warning": "#ffaa00",
    "danger": "#ff0000"
  },
  "fonts": {
    "family": "Segoe UI, Arial, sans-serif",
    "sizes": {
      "small": 12,
      "medium": 14,
      "large": 18,
      "xlarge": 24
    }
  },
  "borders": {
    "radius": 8,
    "width": 1,
    "color": "#333333"
  },
  "opacity": {
    "widget": 0.85,
    "overlay": 0.95
  }
}
```

### Layout Preset Structure

```typescript
// src/types/preset.types.ts
export interface LayoutPreset {
  id: string;
  name: string;
  description: string;
  thumbnail: string;
  widgets: {
    widgetType: string;
    isEnabled: boolean;
    position: {
      x: number;
      y: number;
      width: number;
      height: number;
    };
    settings: Record<string, any>;
  }[];
}
```

### Example Race Layout Preset

```json
{
  "id": "race",
  "name": "Race Layout",
  "description": "Optimized for competitive racing",
  "thumbnail": "/presets/race.png",
  "widgets": [
    {
      "widgetType": "Relative",
      "isEnabled": true,
      "position": { "x": 50, "y": 50, "width": 350, "height": 200 },
      "settings": { "carsAhead": 3, "carsBehind": 3 }
    },
    {
      "widgetType": "Fuel",
      "isEnabled": true,
      "position": { "x": 450, "y": 50, "width": 200, "height": 150 },
      "settings": { "lowFuelWarning": 5.0 }
    },
    {
      "widgetType": "Timing",
      "isEnabled": true,
      "position": { "x": 50, "y": 300, "width": 250, "height": 120 },
      "settings": { "showSectorTimes": false }
    },
    {
      "widgetType": "TrackMap",
      "isEnabled": true,
      "position": { "x": 1400, "y": 50, "width": 300, "height": 300 },
      "settings": { "showOtherCars": true }
    }
  ]
}
```

### Theme Selector Component

```typescript
// src/components/ThemeSelector/ThemeSelector.tsx
import React, { useState, useEffect } from 'react';
import { Theme } from '../../types/theme.types';
import { themeApi } from '../../services/api';
import './ThemeSelector.css';

export const ThemeSelector: React.FC = () => {
  const [themes, setThemes] = useState<Theme[]>([]);
  const [selectedTheme, setSelectedTheme] = useState<string>('default');

  useEffect(() => {
    loadThemes();
  }, []);

  const loadThemes = async () => {
    const data = await themeApi.getAll();
    setThemes(data);
  };

  const handleThemeChange = async (themeId: string) => {
    await themeApi.apply(themeId);
    setSelectedTheme(themeId);
  };

  return (
    <div className="theme-selector">
      <h2>Select Theme</h2>
      <div className="theme-grid">
        {themes.map(theme => (
          <div
            key={theme.id}
            className={`theme-card ${selectedTheme === theme.id ? 'active' : ''}`}
            onClick={() => handleThemeChange(theme.id)}
          >
            <div className="theme-preview" style={{
              backgroundColor: theme.colors.background,
              color: theme.colors.text,
              borderColor: theme.colors.primary
            }}>
              <div className="preview-content">
                <span style={{ color: theme.colors.primary }}>Aa</span>
                <span style={{ color: theme.colors.accent }}>Bb</span>
              </div>
            </div>
            <div className="theme-info">
              <h3>{theme.name}</h3>
              <p>{theme.description}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
```

### Export/Import Functionality

```typescript
// src/services/configExport.ts
export const configExportService = {
  exportConfiguration: async (name: string) => {
    const config = await layoutApi.getCurrent();
    const blob = new Blob([JSON.stringify(config, null, 2)], {
      type: 'application/json'
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${name}.overlay.json`;
    link.click();
    URL.revokeObjectURL(url);
  },

  importConfiguration: async (file: File): Promise<LayoutConfiguration> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = (e) => {
        try {
          const config = JSON.parse(e.target?.result as string);
          // Validate configuration
          if (!isValidConfiguration(config)) {
            reject(new Error('Invalid configuration file'));
            return;
          }
          resolve(config);
        } catch (error) {
          reject(error);
        }
      };
      reader.onerror = () => reject(reader.error);
      reader.readAsText(file);
    });
  }
};

function isValidConfiguration(config: any): boolean {
  return (
    config &&
    typeof config.name === 'string' &&
    Array.isArray(config.widgets) &&
    config.widgets.every((w: any) => 
      w.widgetType && 
      w.position && 
      typeof w.position.x === 'number'
    )
  );
}
```

---

## 🎨 UI Design

### Advanced Configuration Layout

```
┌─────────────────────────────────────────────────────────────┐
│ iRacing Overlay - Advanced Configuration                    │
├─────────────────────────────────────────────────────────────┤
│ [Layouts] [Themes] [Widgets] [Export/Import]               │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────┐  ┌───────────────────────────┐  │
│  │ Available Widgets    │  │ Live Preview             │  │
│  │                      │  │                           │  │
│  │ [Relative] [Drag me] │  │  ┌─────────┐            │  │
│  │ [Fuel]    [Drag me] │  │  │Relative │            │  │
│  │ [Timing]  [Drag me] │  │  └─────────┘            │  │
│  │ [Standings][Drag]   │  │     ┌────┐              │  │
│  │ [TrackMap] [Drag]   │  │     │Fuel│              │  │
│  │                      │  │     └────┘              │  │
│  └──────────────────────┘  └───────────────────────────┘  │
│                                                              │
│  Theme: [Dark ▼]  Preset: [Race ▼]                         │
│  [Export Config] [Import Config] [Reset to Default]        │
└─────────────────────────────────────────────────────────────┘
```

---

## 🐛 Common Issues & Solutions

### Issue: Drag Performance Poor

**Symptoms:** Laggy dragging, low FPS

**Solutions:**

- Use transform instead of position for dragging
- Reduce preview complexity during drag
- Implement drag throttling
- Use CSS will-change property

### Issue: Theme Not Applying

**Symptoms:** Theme changes don't show in overlay

**Solutions:**

- Verify theme is applied to all widget components
- Check CSS variable propagation
- Use SignalR to push theme changes
- Add theme reload endpoint

### Issue: Layout Import Fails

**Symptoms:** Imported layouts don't load

**Solutions:**

- Add robust validation
- Show clear error messages
- Provide example files
- Add version compatibility check

---

## 🧪 Testing Scenarios

### Test 1: Drag-and-Drop

1. Drag a widget around preview
2. Verify position updates smoothly
3. Drop widget at new location
4. Verify position saved
5. Check overlay reflects change

### Test 2: Theme Switching

1. Select different themes
2. Verify instant preview
3. Check all widgets update
4. Restart and verify theme persisted

### Test 3: Layout Presets

1. Load Race preset
2. Verify all widgets positioned correctly
3. Load Practice preset
4. Verify layout changes
5. Create custom layout and save

### Test 4: Export/Import

1. Configure custom layout
2. Export configuration
3. Reset to default
4. Import saved configuration
5. Verify layout restored correctly

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **Drag FPS** | 60 FPS | 45+ FPS | <30 FPS |
| **Theme Switch** | <500ms | <1s | >2s |
| **Preview Update** | <100ms | <200ms | >500ms |
| **Export Time** | <1s | <2s | >5s |
| **Import Validation** | <500ms | <1s | >3s |

---

## ✨ Completion Checklist

MVP 6 is complete when:

- [ ] All success criteria met
- [ ] Drag-and-drop works smoothly
- [ ] Live preview accurate
- [ ] 3+ themes implemented
- [ ] 2+ layout presets available
- [ ] Export/import functional
- [ ] Performance targets achieved
- [ ] UI is polished and intuitive
- [ ] Comprehensive documentation written
- [ ] User testing completed
- [ ] All bugs fixed
- [ ] Code committed to Git

---

## 🚀 Next Steps

Once MVP 6 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Create comprehensive demo video
4. Write full user documentation
5. Prepare for v1.0 release
6. **Optional:** Start Phase 4 (AI Features) planning

---

## 🎯 Alternative: Skip MVP 6

**If you want to release sooner:**

- MVP 5 provides essential configuration
- MVP 6 is polish and convenience
- Can be added in v1.1 or v1.2
- Focus on stability and bug fixes instead
- Build user base first, add features later

**Recommendation:** Complete MVP 5, gather feedback, then decide on MVP 6

---

## 📚 Resources

- [dnd-kit Documentation](https://docs.dndkit.com/)
- [React Beautiful DnD](https://github.com/atlassian/react-beautiful-dnd)
- [CSS Custom Properties (Theming)](https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties)
- [File Export/Import in JavaScript](https://developer.mozilla.org/en-US/docs/Web/API/File_API)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After MVP 5 completion (decision point)  
**Related:** [MVP 5](./MVP5_Basic_Config.md) | [Overview](../../MANAGEABLE_PHASES.md)
