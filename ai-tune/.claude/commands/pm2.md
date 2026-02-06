---
description: Auto-analyze project and generate PM2 service management commands
---

# PM2 Service Manager

Automatically detect your project type, generate a complete PM2 ecosystem config, and provide service management commands.

## Workflow

1. **Detect** project type and framework
2. **Generate** PM2 ecosystem config
3. **Setup** log rotation and monitoring
4. **Provide** common management commands

## Service Detection Table

| Framework | Default Port | Start Command |
|-----------|-------------|---------------|
| Vite (React/Vue) | 5173 | `vite --host` |
| Next.js | 3000 | `next start` |
| Nuxt | 3000 | `nuxt start` |
| CRA | 3000 | `react-scripts start` |
| Express | 3000 | `node server.js` |
| FastAPI | 8000 | `uvicorn main:app` |
| Go | 8080 | `go run .` |

## Generated Files

After running this command, you'll have:

```
project/
├── ecosystem.config.cjs    # PM2 config (use .cjs for ESM projects)
├── .pm2-commands.md         # Quick-reference commands
└── logs/                    # Log directory (gitignored)
```

## Windows Configuration

On Windows, PM2 configs **must** use `.cjs` extension if your project uses ESM (`"type": "module"` in package.json).

Interpreter paths on Windows:
- Node: `C:\\Program Files\\nodejs\\node.exe`
- Python: `C:\\Users\\<user>\\AppData\\Local\\Programs\\Python\\Python3x\\python.exe`

## Ecosystem Config Template

```javascript
// ecosystem.config.cjs
module.exports = {
  apps: [
    {
      name: 'app-name',
      script: './src/index.js',
      instances: 'max',
      exec_mode: 'cluster',
      watch: false,
      max_memory_restart: '500M',
      env: {
        NODE_ENV: 'production',
        PORT: 3000,
      },
      env_development: {
        NODE_ENV: 'development',
        PORT: 3000,
      },
      error_file: './logs/err.log',
      out_file: './logs/out.log',
      merge_logs: true,
      log_date_format: 'YYYY-MM-DD HH:mm:ss Z',
    },
  ],
}
```

## Common PM2 Commands

```bash
# Start with ecosystem config
pm2 start ecosystem.config.cjs

# Start in development mode
pm2 start ecosystem.config.cjs --env development

# List all processes
pm2 list

# Monitor processes
pm2 monit

# View logs
pm2 logs
pm2 logs app-name --lines 100

# Restart / Stop / Delete
pm2 restart app-name
pm2 stop app-name
pm2 delete app-name

# Save process list (survives reboot)
pm2 save

# Setup startup script
pm2 startup
```

## Key Rules

1. **Always detect** the project type before generating config
2. **Use `.cjs`** extension for ESM projects on all platforms
3. **Set `watch: false`** in production (use `true` only in dev)
4. **Configure log rotation** to prevent disk fill
5. **Set `max_memory_restart`** to prevent memory leaks
6. **Use cluster mode** for Node.js HTTP servers
7. **Use fork mode** for scripts, cron jobs, and non-Node processes

## Post-Init

After generating the PM2 config, update your project's `CLAUDE.md`:

```markdown
## PM2 Management

- Start: `pm2 start ecosystem.config.cjs`
- Logs: `pm2 logs`
- Monitor: `pm2 monit`
- Restart: `pm2 restart all`
```

## Log Rotation Setup

```bash
pm2 install pm2-logrotate
pm2 set pm2-logrotate:max_size 10M
pm2 set pm2-logrotate:retain 7
pm2 set pm2-logrotate:compress true
```
