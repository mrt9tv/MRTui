# MVP 5: Basic Configuration UI

**Duration:** 1-2 weeks (Actual: ~1 week)  
**Goal:** Desktop-based configuration interface (WPF)  
**Status:** ✅ **COMPLETE** (February 2025)  
**Prerequisite:** MVP 1-4 must be complete

---

## 🎯 What You Built

- **WPF Configuration UI** (MainWindow) with tabbed interface
- **Widget enable/disable** via checkboxes (immediate apply)
- **Real-time configuration** updates (no Apply buttons)
- **Settings persistence** to AppSettings.json
- **Direct integration** with WidgetManager (no web API needed)

---

## ✅ Success Criteria

- ✅ Configuration UI accessible via MainWindow (WPF desktop app)
- ✅ Can enable/disable each widget (Activate checkboxes)
- ✅ Can adjust widget positions by dragging (manual positioning)
- ✅ Changes apply in real-time to overlay (event-driven)
- ✅ Settings persist after restart (AppSettings.json)
- ✅ Clean, functional UI with immediate feedback
- ✅ UI responds instantly (<16ms via event handlers)

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay.Web/
├── Program.cs
├── Controllers/
│   ├── WidgetsController.cs
│   ├── LayoutController.cs
│   └── SettingsController.cs
├── ClientApp/
│   ├── package.json
│   ├── tsconfig.json
│   ├── public/
│   │   └── index.html
│   └── src/
│       ├── App.tsx
│       ├── App.css
│       ├── components/
│       │   ├── WidgetList.tsx
│       │   ├── WidgetSettings.tsx
│       │   ├── PositionEditor.tsx
│       │   └── LayoutSelector.tsx
│       ├── services/
│       │   └── api.ts
│       └── types/
│           └── widget.types.ts
└── appsettings.json
```

---

## 📋 Implementation Checklist

### Week 1: Backend API + Basic Frontend

#### Days 1-2: ASP.NET Core API

- [ ] Create ASP.NET Core Web API project
- [ ] Configure CORS for localhost
- [ ] Add Swagger/OpenAPI documentation
- [ ] Create WidgetsController (GET, PUT endpoints)
- [ ] Create LayoutController (save/load)
- [ ] Add real-time communication (SignalR optional)
- [ ] Test API endpoints with Postman

#### Days 3-4: React Setup + Basic UI

- [ ] Initialize React + TypeScript project
- [ ] Install dependencies (axios, react-router, etc.)
- [ ] Create basic app layout
- [ ] Build WidgetList component
- [ ] Add enable/disable toggles
- [ ] Create API service wrapper
- [ ] Test basic data fetching

#### Days 5-7: Position Editor + Integration

- [ ] Build PositionEditor component
- [ ] Add X/Y input fields
- [ ] Implement position update API calls
- [ ] Add save/load layout buttons
- [ ] Test real-time updates to overlay
- [ ] Add basic error handling
- [ ] Polish UI styling

### Week 2: Advanced Features + Polish

#### Days 8-9: Real-Time Preview

- [ ] Implement SignalR hub (optional)
- [ ] Add live overlay updates
- [ ] Show connection status
- [ ] Handle disconnection gracefully

#### Days 10-12: Settings Per Widget

- [ ] Create widget-specific settings forms
- [ ] Add settings validation
- [ ] Implement settings save/load
- [ ] Test with each widget type

#### Days 13-14: Testing + Documentation

- [ ] End-to-end testing
- [ ] Cross-browser testing (Chrome, Edge)
- [ ] Write user guide
- [ ] Create API documentation
- [ ] Bug fixes and polish

---

## ⏱️ Time Breakdown

| Days | Focus Area | Hours |
|------|-----------|-------|
| **1-2** | ASP.NET Core API | 8-10 |
| **3-4** | React Setup + Basic UI | 10-12 |
| **5-7** | Position Editor + Integration | 12-14 |
| **8-9** | Real-Time Preview | 6-8 |
| **10-12** | Widget Settings | 10-12 |
| **13-14** | Testing + Documentation | 6-8 |

**Total Estimated Hours:** 52-64 hours

---

## 🔧 Technical Details

### ASP.NET Core API Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder => builder
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Add SignalR (optional)
builder.Services.AddSignalR();

// Register services
builder.Services.AddSingleton<IWidgetManager, WidgetManager>();
builder.Services.AddSingleton<LayoutManager>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Widgets API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class WidgetsController : ControllerBase
{
    private readonly IWidgetManager _widgetManager;
    private readonly ILogger<WidgetsController> _logger;
    
    public WidgetsController(
        IWidgetManager widgetManager,
        ILogger<WidgetsController> logger)
    {
        _widgetManager = widgetManager;
        _logger = logger;
    }
    
    [HttpGet]
    public ActionResult<IEnumerable<WidgetDto>> GetAll()
    {
        var widgets = _widgetManager.Widgets
            .Select(w => new WidgetDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                IsEnabled = w.IsEnabled,
                Position = new PositionDto
                {
                    X = w.Configuration.Position.X,
                    Y = w.Configuration.Position.Y,
                    Width = w.Configuration.Position.Width,
                    Height = w.Configuration.Position.Height
                },
                Settings = w.Configuration.Settings
            });
        
        return Ok(widgets);
    }
    
    [HttpGet("{id}")]
    public ActionResult<WidgetDto> GetById(string id)
    {
        var widget = _widgetManager.GetWidget(id);
        if (widget == null)
            return NotFound();
        
        return Ok(new WidgetDto
        {
            Id = widget.Id,
            Name = widget.Name,
            // ... other properties
        });
    }
    
    [HttpPut("{id}/enabled")]
    public ActionResult SetEnabled(string id, [FromBody] bool enabled)
    {
        var widget = _widgetManager.GetWidget(id);
        if (widget == null)
            return NotFound();
        
        widget.IsEnabled = enabled;
        if (enabled)
            _widgetManager.ShowWidget(id);
        else
            _widgetManager.HideWidget(id);
        
        return NoContent();
    }
    
    [HttpPut("{id}/position")]
    public ActionResult UpdatePosition(string id, [FromBody] PositionDto position)
    {
        var widget = _widgetManager.GetWidget(id);
        if (widget == null)
            return NotFound();
        
        widget.Configuration.Position = new WidgetPosition
        {
            X = position.X,
            Y = position.Y,
            Width = position.Width,
            Height = position.Height
        };
        
        return NoContent();
    }
    
    [HttpPut("{id}/settings")]
    public ActionResult UpdateSettings(string id, [FromBody] Dictionary<string, object> settings)
    {
        var widget = _widgetManager.GetWidget(id);
        if (widget == null)
            return NotFound();
        
        widget.Configuration.Settings = settings;
        
        return NoContent();
    }
}
```

### React API Service

```typescript
// src/services/api.ts
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

export interface Widget {
  id: string;
  name: string;
  description: string;
  isEnabled: boolean;
  position: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  settings: Record<string, any>;
}

export const widgetApi = {
  async getAll(): Promise<Widget[]> {
    const response = await axios.get<Widget[]>(`${API_BASE_URL}/widgets`);
    return response.data;
  },

  async getById(id: string): Promise<Widget> {
    const response = await axios.get<Widget>(`${API_BASE_URL}/widgets/${id}`);
    return response.data;
  },

  async setEnabled(id: string, enabled: boolean): Promise<void> {
    await axios.put(`${API_BASE_URL}/widgets/${id}/enabled`, enabled);
  },

  async updatePosition(id: string, position: Widget['position']): Promise<void> {
    await axios.put(`${API_BASE_URL}/widgets/${id}/position`, position);
  },

  async updateSettings(id: string, settings: Record<string, any>): Promise<void> {
    await axios.put(`${API_BASE_URL}/widgets/${id}/settings`, settings);
  }
};

export const layoutApi = {
  async save(name: string): Promise<void> {
    await axios.post(`${API_BASE_URL}/layout/save`, { name });
  },

  async load(name: string): Promise<void> {
    await axios.post(`${API_BASE_URL}/layout/load`, { name });
  },

  async list(): Promise<string[]> {
    const response = await axios.get<string[]>(`${API_BASE_URL}/layout/list`);
    return response.data;
  }
};
```

### React WidgetList Component

```typescript
// src/components/WidgetList.tsx
import React, { useEffect, useState } from 'react';
import { Widget, widgetApi } from '../services/api';
import './WidgetList.css';

export const WidgetList: React.FC = () => {
  const [widgets, setWidgets] = useState<Widget[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadWidgets();
  }, []);

  const loadWidgets = async () => {
    try {
      setLoading(true);
      const data = await widgetApi.getAll();
      setWidgets(data);
      setError(null);
    } catch (err) {
      setError('Failed to load widgets');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const toggleWidget = async (id: string, currentState: boolean) => {
    try {
      await widgetApi.setEnabled(id, !currentState);
      setWidgets(prev =>
        prev.map(w => (w.id === id ? { ...w, isEnabled: !currentState } : w))
      );
    } catch (err) {
      console.error('Failed to toggle widget', err);
    }
  };

  if (loading) return <div>Loading...</div>;
  if (error) return <div className="error">{error}</div>;

  return (
    <div className="widget-list">
      <h2>Widgets</h2>
      {widgets.map(widget => (
        <div key={widget.id} className="widget-item">
          <div className="widget-info">
            <h3>{widget.name}</h3>
            <p>{widget.description}</p>
          </div>
          <label className="toggle-switch">
            <input
              type="checkbox"
              checked={widget.isEnabled}
              onChange={() => toggleWidget(widget.id, widget.isEnabled)}
            />
            <span className="slider"></span>
          </label>
        </div>
      ))}
    </div>
  );
};
```

### React PositionEditor Component

```typescript
// src/components/PositionEditor.tsx
import React, { useState } from 'react';
import { Widget, widgetApi } from '../services/api';
import './PositionEditor.css';

interface Props {
  widget: Widget;
  onUpdate: () => void;
}

export const PositionEditor: React.FC<Props> = ({ widget, onUpdate }) => {
  const [position, setPosition] = useState(widget.position);
  const [saving, setSaving] = useState(false);

  const handleChange = (field: keyof Widget['position'], value: number) => {
    setPosition(prev => ({ ...prev, [field]: value }));
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      await widgetApi.updatePosition(widget.id, position);
      onUpdate();
    } catch (err) {
      console.error('Failed to save position', err);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="position-editor">
      <h3>Position: {widget.name}</h3>
      <div className="position-inputs">
        <label>
          X:
          <input
            type="number"
            value={position.x}
            onChange={e => handleChange('x', Number(e.target.value))}
          />
        </label>
        <label>
          Y:
          <input
            type="number"
            value={position.y}
            onChange={e => handleChange('y', Number(e.target.value))}
          />
        </label>
        <label>
          Width:
          <input
            type="number"
            value={position.width}
            onChange={e => handleChange('width', Number(e.target.value))}
          />
        </label>
        <label>
          Height:
          <input
            type="number"
            value={position.height}
            onChange={e => handleChange('height', Number(e.target.value))}
          />
        </label>
      </div>
      <button onClick={handleSave} disabled={saving}>
        {saving ? 'Saving...' : 'Save Position'}
      </button>
    </div>
  );
};
```

---

## 🎨 UI Design (Basic)

### Main Layout

```
┌────────────────────────────────────────┐
│ iRacing Overlay Configuration          │
├────────────────────────────────────────┤
│                                        │
│  ┌──────────────┐  ┌─────────────────┐│
│  │ Widget List  │  │ Position Editor ││
│  │              │  │                 ││
│  │ ☑ Relative   │  │ X: 100          ││
│  │ ☑ Fuel       │  │ Y: 100          ││
│  │ ☐ Timing     │  │ Width: 350      ││
│  │ ☐ Standings  │  │ Height: 200     ││
│  │              │  │                 ││
│  │              │  │ [Save Position] ││
│  └──────────────┘  └─────────────────┘│
│                                        │
│  [Save Layout] [Load Layout] [Reset]  │
└────────────────────────────────────────┘
```

---

## 🐛 Common Issues & Solutions

### Issue: CORS Errors

**Symptoms:** API calls fail with CORS errors

**Solutions:**

- Verify CORS policy includes correct origin
- Check AllowCredentials is set if needed
- Use proxy in React dev server (alternative)

### Issue: Changes Not Applying

**Symptoms:** UI updates but overlay doesn't change

**Solutions:**

- Check API is actually updating widget configuration
- Verify widget is listening for configuration changes
- Add logging to track update flow
- Implement SignalR for real-time updates

### Issue: Port Conflicts

**Symptoms:** Can't start API or React dev server

**Solutions:**

- Use different ports (API: 5000, React: 3000)
- Check no other apps using those ports
- Configure ports in appsettings.json and package.json

---

## 🧪 Testing Scenarios

### Test 1: Enable/Disable Widgets

1. Open web interface
2. Toggle widget enable checkbox
3. Verify widget appears/disappears in overlay
4. Restart app and verify state persisted

### Test 2: Position Adjustment

1. Select a widget
2. Change X/Y values
3. Click Save Position
4. Verify widget moves in overlay
5. Restart and verify position saved

### Test 3: Multiple Widgets

1. Enable 3-4 widgets
2. Adjust positions for each
3. Save layout
4. Restart application
5. Verify all widgets restored correctly

### Test 4: Error Handling

1. Stop overlay application
2. Try to make changes in web UI
3. Verify appropriate error messages
4. Restart overlay and verify recovery

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **API Response** | <50ms | <100ms | >200ms |
| **UI Load Time** | <1s | <2s | >5s |
| **Position Update** | <100ms | <200ms | >500ms |
| **Layout Save** | <500ms | <1s | >3s |
| **Web UI Memory** | <100MB | <150MB | >250MB |

---

## ✨ Completion Checklist

Before moving to MVP 6, ensure:

- [ ] All success criteria met
- [ ] Web interface accessible and functional
- [ ] Can enable/disable all widgets
- [ ] Position updates work in real-time
- [ ] Configuration persists across restarts
- [ ] API documented (Swagger)
- [ ] Frontend code is clean and maintainable
- [ ] Error handling implemented
- [ ] User guide written
- [ ] Code committed to Git

---

## 🚀 Next Steps

Once MVP 5 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Create demo video of configuration UI
4. Write comprehensive user guide
5. Commit all code changes
6. **Decision Point:** Move to MVP 6 or polish existing features

---

## 📚 Resources

- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
- [React TypeScript](https://react-typescript-cheatsheet.netlify.app/)
- [Axios HTTP Client](https://axios-http.com/docs/intro)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Create React App with TypeScript](https://create-react-app.dev/docs/adding-typescript/)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After completion  
**Related:** [MVP 4](./MVP4_Multi_Widget.md) | [MVP 6](./MVP6_Advanced_Config.md) | [Overview](../../MANAGEABLE_PHASES.md)
