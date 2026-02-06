# Research Mode Context

You are in **research mode** - focused on exploration, learning, and discovery.

## Objectives

1. **Understand deeply** before implementing
2. **Document findings** for future reference
3. **Evaluate options** objectively
4. **Create knowledge artifacts** (bd issues, notes)

## Research Workflow

### 1. Define Question
- What are we trying to learn?
- Why does this matter?
- What decisions depend on this?

### 2. Gather Information
- Read documentation thoroughly
- Search for existing implementations
- Check community discussions (GitHub issues, Stack Overflow)
- Review academic papers if relevant

### 3. Experiment
- Create proof-of-concept code
- Test different approaches
- Measure performance if relevant
- Document what works and what doesn't

### 4. Document Findings
```bash
# Create research bd issue
bd create "Research: [Topic]" \
  --type documentation \
  --priority 3 \
  --description "$(cat <<'EOF'
## Question
[What we're researching]

## Approaches Evaluated
1. Approach A
   - Pros: ...
   - Cons: ...
   - Verdict: ...

2. Approach B
   - Pros: ...
   - Cons: ...
   - Verdict: ...

## Recommendation
[Best approach and why]

## Resources
- Link 1
- Link 2

## Next Steps
- Action 1
- Action 2
EOF
)"
```

### 5. Make Recommendation
- Clear decision with reasoning
- Trade-offs explained
- Implementation path outlined

## Research Patterns

### Spike Solution
- Time-boxed exploration (1-2 hours)
- Goal: Answer specific question
- Output: Document findings, throw away code

### Proof of Concept
- Minimal viable implementation
- Goal: Validate approach works
- Output: Working code + documentation

### Technology Evaluation
- Compare 2-3 alternatives
- Create comparison matrix
- Test key features
- Recommend best fit

## Documentation Format

### Option Comparison
```markdown
| Criteria | Option A | Option B | Option C |
|----------|----------|----------|----------|
| Performance | Fast | Medium | Slow |
| Ease of Use | Complex | Simple | Simple |
| Community | Large | Small | Large |
| Cost | Free | Paid | Free |
| Verdict | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ |
```

### Decision Record
```markdown
# ADR: [Decision Title]

## Status
Accepted / Proposed / Superseded

## Context
[What's the situation and problem?]

## Decision
[What we decided to do]

## Consequences
**Positive:**
- Benefit 1
- Benefit 2

**Negative:**
- Trade-off 1
- Trade-off 2

**Neutral:**
- Thing 1
- Thing 2
```

## Quick Commands

- `/search <topic>` - Web search for information
- `/read <url>` - Fetch and read documentation
- `/learn` - Extract patterns from current work

## Research Output Checklist

- [ ] Question clearly stated
- [ ] Multiple options considered
- [ ] Pros and cons documented
- [ ] Recommendation made with reasoning
- [ ] Created bd issue to preserve findings
- [ ] Implementation path outlined (if applicable)

## When Research is Done

1. **Create ADR** (Architectural Decision Record) if significant
2. **Update project docs** with findings
3. **Create implementation tasks** in bd
4. **Switch to dev mode** and build it

## Red Flags in Research

🚨 Stop and reconsider if:
- Spending > 2 hours without clear progress
- Falling into "analysis paralysis"
- Not documenting as you go
- Ignoring simpler alternatives
- Optimizing prematurely
