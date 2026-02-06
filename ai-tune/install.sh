#!/bin/bash
# AI-Tune Installation Script for Mac/Linux
# Run with: bash install.sh [--global | --project PATH]

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Parse arguments
GLOBAL=false
PROJECT_PATH="."

while [[ $# -gt 0 ]]; do
    case $1 in
        --global)
            GLOBAL=true
            shift
            ;;
        --project)
            PROJECT_PATH="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Usage: $0 [--global | --project PATH]"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}==================================${NC}"
echo -e "${CYAN}  AI-Tune Installation${NC}"
echo -e "${CYAN}==================================${NC}"
echo ""

# Check if beads is installed
echo -e "${YELLOW}Checking prerequisites...${NC}"
if command -v bd &> /dev/null; then
    BD_VERSION=$(bd --version 2>&1 || echo "unknown")
    echo -e "${GREEN}✓ Beads is installed: $BD_VERSION${NC}"
else
    echo -e "${RED}✗ Beads (bd) not found!${NC}"
    echo ""
    echo -e "${YELLOW}Please install beads first:${NC}"
    echo -e "  ${NC}curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash${NC}"
    echo ""
    exit 1
fi

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SOURCE_CLAUDE_DIR="$SCRIPT_DIR/.claude"
SOURCE_SESSIONS_DIR="$SCRIPT_DIR/.claude-sessions"

# Verify source directories exist
if [ ! -d "$SOURCE_CLAUDE_DIR" ]; then
    echo -e "${RED}✗ Source .claude directory not found at: $SOURCE_CLAUDE_DIR${NC}"
    exit 1
fi

if [ "$GLOBAL" = true ]; then
    # Global installation
    echo ""
    echo -e "${YELLOW}Installing AI-Tune globally...${NC}"
    
    GLOBAL_CLAUDE_DIR="$HOME/.claude"
    
    # Create global directory if it doesn't exist
    mkdir -p "$GLOBAL_CLAUDE_DIR"
    echo -e "${GREEN}✓ Global .claude directory ready${NC}"
    
    # Copy agents
    echo -e "  Copying agents..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/agents"
    cp -r "$SOURCE_CLAUDE_DIR/agents/"* "$GLOBAL_CLAUDE_DIR/agents/"
    echo -e "${GREEN}  ✓ Agents installed${NC}"
    
    # Copy commands
    echo -e "  Copying commands..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/commands"
    cp -r "$SOURCE_CLAUDE_DIR/commands/"* "$GLOBAL_CLAUDE_DIR/commands/"
    echo -e "${GREEN}  ✓ Commands installed${NC}"
    
    # Copy skills
    echo -e "  Copying skills..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/skills"
    cp -r "$SOURCE_CLAUDE_DIR/skills/"* "$GLOBAL_CLAUDE_DIR/skills/"
    echo -e "${GREEN}  ✓ Skills installed${NC}"
    
    # Copy contexts
    echo -e "  Copying contexts..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/contexts"
    cp -r "$SOURCE_CLAUDE_DIR/contexts/"* "$GLOBAL_CLAUDE_DIR/contexts/"
    echo -e "${GREEN}  ✓ Contexts installed${NC}"
    
    # Copy rules
    echo -e "  Copying rules..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/rules"
    if [ -d "$SOURCE_CLAUDE_DIR/rules" ]; then
        cp -r "$SOURCE_CLAUDE_DIR/rules/"* "$GLOBAL_CLAUDE_DIR/rules/"
        echo -e "${GREEN}  ✓ Rules installed${NC}"
    fi
    
    # Copy hooks
    echo -e "  Copying hooks..."
    mkdir -p "$GLOBAL_CLAUDE_DIR/hooks"
    cp -r "$SOURCE_CLAUDE_DIR/hooks/"* "$GLOBAL_CLAUDE_DIR/hooks/"
    echo -e "${GREEN}  ✓ Hooks installed${NC}"
    
    echo ""
    echo -e "${GREEN}✓ Global installation complete!${NC}"
    echo ""
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "  ${NC}1. In each project: cd your-project && bd init${NC}"
    echo -e "  ${NC}2. Set up beads-Claude integration: bd setup claude --project${NC}"
    echo -e "  ${NC}3. Try it: /bd-ready${NC}"
    
else
    # Project-local installation
    echo ""
    echo -e "${YELLOW}Installing AI-Tune to project: $PROJECT_PATH${NC}"
    
    PROJECT_PATH=$(cd "$PROJECT_PATH" && pwd)
    PROJECT_CLAUDE_DIR="$PROJECT_PATH/.claude"
    PROJECT_SESSIONS_DIR="$PROJECT_PATH/.claude-sessions"
    
    # Create project .claude directory
    mkdir -p "$PROJECT_CLAUDE_DIR"
    echo -e "${GREEN}✓ .claude directory ready${NC}"
    
    # Copy everything
    echo -e "  Copying configuration..."
    cp -r "$SOURCE_CLAUDE_DIR/"* "$PROJECT_CLAUDE_DIR/"
    echo -e "${GREEN}  ✓ .claude directory copied${NC}"
    
    # Create .claude-sessions directory
    mkdir -p "$PROJECT_SESSIONS_DIR"
    echo -e "${GREEN}  ✓ Created .claude-sessions directory${NC}"
    
    # Copy session template
    if [ -d "$SOURCE_SESSIONS_DIR" ]; then
        cp -r "$SOURCE_SESSIONS_DIR/"* "$PROJECT_SESSIONS_DIR/"
        echo -e "${GREEN}  ✓ Session template copied${NC}"
    fi
    
    # Initialize beads if not already initialized
    cd "$PROJECT_PATH"
    if [ ! -d ".beads" ]; then
        echo ""
        echo -e "${YELLOW}Initializing beads in project...${NC}"
        bd init
        echo -e "${GREEN}✓ Beads initialized${NC}"
    else
        echo -e "${GREEN}  ✓ Beads already initialized${NC}"
    fi
    
    # Set up Claude Code integration
    echo ""
    echo -e "${YELLOW}Setting up beads-Claude integration...${NC}"
    bd setup claude --project
    echo -e "${GREEN}✓ Integration configured${NC}"
    
    # Add .claude-sessions to .gitignore if git repo exists
    if [ -d ".git" ]; then
        if [ -f ".gitignore" ]; then
            if ! grep -q ".claude-sessions" ".gitignore"; then
                echo -e "\n# AI-Tune session memory (local only)\n.claude-sessions/" >> .gitignore
                echo -e "${GREEN}  ✓ Added .claude-sessions to .gitignore${NC}"
            fi
        else
            echo -e "# AI-Tune session memory (local only)\n.claude-sessions/" > .gitignore
            echo -e "${GREEN}  ✓ Created .gitignore with .claude-sessions${NC}"
        fi
    fi
    
    echo ""
    echo -e "${GREEN}✓ Project installation complete!${NC}"
    echo ""
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "  ${NC}1. Open your project in Claude Code (VS Code)${NC}"
    echo -e "  ${NC}2. Try: /bd-ready${NC}"
    echo -e "  ${NC}3. Create your first plan: /bd-plan \"Your feature\"${NC}"
    echo ""
    echo -e "${YELLOW}Quick Reference:${NC}"
    echo -e "  ${NC}/bd-plan       - Plan and create bd issues${NC}"
    echo -e "  ${NC}/bd-ready      - Show unblocked tasks${NC}"
    echo -e "  ${NC}/bd-work <id>  - Start working on task${NC}"
    echo -e "  ${NC}/bd-complete   - Finish task and find next${NC}"
    echo -e "  ${NC}/sessions      - Manage session memory${NC}"
fi

echo ""
echo -e "${CYAN}==================================${NC}"
echo -e "${CYAN}  Installation Complete!${NC}"
echo -e "${CYAN}==================================${NC}"
