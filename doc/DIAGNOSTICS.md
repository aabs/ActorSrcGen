# Diagnostics Reference

| ID | Title | When It Appears | How To Fix |
|----|-------|-----------------|------------|
| ASG0001 | Actor with multiple input types must be disjoint | Multiple entry steps share the same input type, so routing is ambiguous. | Use distinct input types for each entry step or consolidate the steps to a single entry point. |
| ASG0002 | Actor must have at least one input type | No methods are marked with [FirstStep], [Step], or receiver attributes. | Add an entry method with [FirstStep] or [Step] (or Receiver) so the actor can accept input. |
| ASG0003 | Error generating source | An exception occurred during generation; details are in the diagnostic message. | Fix the underlying exception (often invalid signatures, missing partial keyword, or template issues) and re-run `dotnet test`. |

Notes
- All diagnostics use category `ActorSrcGen` and report the actor symbol location when available.
- Generation stops at errors; resolve them to resume emission.
- Use `dotnet test --filter Category=Integration` to reproduce most diagnostic scenarios.
