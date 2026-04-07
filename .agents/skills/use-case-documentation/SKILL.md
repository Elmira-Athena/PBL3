---
name: Use Case Documentation
description: Write formal Use Cases with actors, preconditions, triggers, main flows, alternative flows, and postconditions
---

# Use Case Documentation Skill

## Purpose
Describe how a user interactions with a system to achieve a specific goal. Use cases capture functional requirements from a user-centric perspective, often used in Waterfall or Hybrid environments where detailed specification is needed before implementation.

## When to Use
- Complex transactional systems (banking, healthcare).
- When specific alternate flows and error handling must be rigorously defined.
- Regulatory compliance requires detailed documentation.
- Bridging gap between business needs and technical functional specs.

## Anatomy of a Use Case

### Key Elements
1.  **Use Case Name**: Verb-Noun phrase (e.g., "Withdraw Cash").
2.  **ID**: Unique identifier (e.g., UC-ATM-01).
3.  **Actor(s)**: Who interacts? (Primary: Initiates; Secondary: External systems).
4.  **Description**: Brief summary of goal.
5.  **Preconditions**: What must be true BEFORE this starts? (e.g., "User is logged in").
6.  **Trigger**: What starts the use case? (e.g., "User clicks Withdraw").
7.  **Main Success Scenario (Basic Flow)**: The "Happy Path" steps.
8.  **Alternative Flows (Extensions)**: Variations or error paths.
9.  **Postconditions**: What is true AFTER reliable completion? (e.g., "Balance updated").
10. **Business Rules**: Logic links.

## Use Case Template

### Example: Withdraw Cash at ATM

**ID**: UC-ATM-01
**Name**: Withdraw Cash
**Primary Actor**: Bank Customer
**Secondary Actor**: Bank System (Backend)

**Description**: Customer withdraws physical cash from their checking account via ATM.

**Preconditions**:
1. ATM has sufficient cash.
2. ATM is online.
3. Customer has valid card and PIN.

**Trigger**: Customer inserts card.

**Main Success Scenario (Happy Path)**:
1. Customer inserts debit card.
2. System validates card readability.
3. System prompts for PIN.
4. Customer enters PIN.
5. System validates PIN with Bank System.
6. System displays Application Menu.
7. Customer selects "Withdraw Cash".
8. System prompts for Amount.
9. Customer enters Amount (e.g., $100).
10. System checks daily limit and account balance.
11. System authorizes transaction.
12. System dispenses Cash.
13. System prints Receipt.
14. System ejects Card.
15. Use case ends.

**Alternative Flows**:

*   **A1: Invalid PIN (at Step 5)**
    1. System displays "Invalid PIN" message.
    2. System prompts to re-enter PIN.
    3. Customer re-enters PIN.
    4. Resume at Step 5.
    *   *Constraint*: If PIN invalid 3 times, perform **A2 (Lock Card)**.

*   **A2: Lock Card (from A1)**
    1. System displays "Card Locked due to attempts".
    2. System retains card.
    3. Use case ends.

*   **A3: Insufficient Funds (at Step 10)**
    1. System determines account balance < requested amount.
    2. System displays "Insufficient Funds".
    3. System prompts to enter lower amount or cancel.
    4. Customer enters new amount.
    5. Resume at Step 10.

*   **A4: ATM Out of Cash (at Step 10)**
    1. System checks internal cash dispenser > requested amount.
    2. System detects low cash.
    3. System displays "Unable to dispense full amount".
    4. Use case ends.

**Postconditions**:
- User account debited by withdrawal amount.
- Transaction log created.
- Cash inventory updated.

## Use Case vs. User Story

| Feature | Use Case | User Story |
|---------|----------|------------|
| **Focus** | System interaction steps | User value/goal |
| **Detail** | High (Flows, Errors) | Low (Conversation starter) |
| **Format** | Document/Structured Text | card/Post-it (Who, What, Why) |
| **Lifecycle** | Update/Maintain over time | Done and discarded/archived |
| **Context** | Waterfall/Hybrid/Complex | Agile/Scrum |

## Best Practices
- **Write in Active Voice**: "System validates PIN" (not "PIN is validated").
- **Keep it System-Neutral UI**: Don't say "User clicks blue button at top right". Say "User selects Option".
- **Focus on Goal**: Don't get lost in technical details (e.g., "System sends JSON packet").
- **Link Exceptions**: Ensure every error path is handled.

## Tools
- **Lark Docs / MS Word**: Standard formatting.
- **Visual Paradigm / UML Tools**: For Use Case Diagrams.
- **Gherkin**: Can translate Use Case flows into Gherkin scenarios for testing.
