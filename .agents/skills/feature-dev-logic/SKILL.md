---
name: feature-dev-logic
description: Internal instructions for the feature development workflow.
---

# Feature Development Skill

A systematic 7-phase approach to building new features in a codebase. Instead of jumping straight into code, this skill guides you through understanding the codebase, asking clarifying questions, designing architecture, and ensuring quality.

## Core Principles
- **Ask clarifying questions**: Identify all ambiguities early. Wait for user answers before proceeding with architecture/implementation.
- **Understand before acting**: Map patterns and abstractions first using specialized agent roles.
- **Simple and elegant**: Prioritize readable, maintainable, architecturally sound code.
- **Interactive transitions**: Stop and confirm at every major transition (Discovery -> Exploration -> Clarification -> Architecture -> Implementation -> Review).

## When to Use This Skill
- New features that touch multiple files
- Features requiring architectural decisions
- Complex integrations with existing code
- Features where requirements are somewhat unclear

---

## 7-Phase Workflow

### Phase 1: Discovery
**Goal**: Understand what needs to be built.
1. Create a checklist for all 7 phases.
2. clarify the problem, requirements, and constraints with the user.
3. Summarize understanding and confirm before proceeding.

### Phase 2: Codebase Exploration
**Goal**: Understand relevant code and patterns.
1. Conceptually "launch" 2-3 **code-explorer** roles to analyze different aspects (similar features, architecture, UI patterns, etc.).
2. Trace through entry points, call chains, and data flows.
3. Identify 5-10 key files and read them to build deep context.
4. Present a comprehensive summary of findings to the user.

### Phase 3: Clarifying Questions
**Goal**: Resolve all ambiguities.
1. Identify edge cases, error handling, integration points, and backward compatibility needs.
2. Present questions in an organized list.
3. **Wait for answers before proceeding to architecture.**

### Phase 4: Architecture Design
**Goal**: Design multiple implementation approaches.
1. Propose 2-3 approaches with different trade-offs:
   - **Minimal**: Smallest change, maximum reuse.
   - **Clean**: Elegant abstractions, high maintainability.
   - **Pragmatic**: Balance of speed and quality.
2. Recommend one approach with reasoning.
3. **Ask the user for their preferred choice.**

### Phase 5: Implementation
**Goal**: Build the feature.
**DO NOT START WITHOUT EXPLICIT APPROVAL.**
1. Implement the chosen architecture using project conventions.
2. Write clean, well-documented code.
3. Update progress in the checklist.

### Phase 6: Quality Review
**Goal**: Ensure simplicity and correctness.
1. Conceptually "launch" 3 **code-reviewer** roles focusing on:
   - Simplicity/DRY/Elegance.
   - Bugs/Correctness (Logic, edge cases).
   - Conventions/Abstractions.
2. Consolidate high-confidence issues (confidence ≥80).
3. Present findings and ask what to do (fix now, fix later, proceed).

### Phase 7: Summary
**Goal**: Document what was accomplished.
1. Summarize what was built, key decisions, files modified, and suggested next steps.
