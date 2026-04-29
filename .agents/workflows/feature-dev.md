---
description: Comprehensive feature development workflow with specialized agents for codebase exploration, architecture design, and quality review.
---

1. /task Create 7-phase checklist in `task.md`
2. /skill feature-dev-logic
3. Execute Phase 1: Discovery. Ask clarifying questions if the feature request is unclear.
   - Describe what problem you're solving.
   - Identify constraints or requirements.
   - Summarize and confirm understanding.
4. Execute Phase 2: Exploration.
   - Launch `code-explorer` roles.
   - Trace flow and patterns.
   - Read 5-10 key files.
   - Present summary of findings.
5. Execute Phase 3: Clarifying Questions.
   - Identify edge cases, error handling, integration points.
   - Present a clear list of questions.
   - **User Review Required**: Wait for answers.
6. Execute Phase 4: Architecture Design.
   - Propose 2-3 approaches with trade-offs (Minimal, Clean, Pragmatic).
   - Recommend one.
   - **User Review Required**: Ask which approach to use.
7. Execute Phase 5: Implementation.
   - Implement the chosen architecture.
   - Follow conventions strictly.
8. Execute Phase 6: Quality Review.
   - Build and fix the syntax errors.
   - Review for bugs, simplicity, and conventions.
   - **User Review Required**: Present findings and ask what to do (fix now, fix later, proceed).
9. Execute Phase 7: Summary.
   - Summarize work, key decisions, modified files, and next steps.