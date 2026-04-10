important the project should be clear as possible so i can be involve in the code and on the whole process

# Memory
At the start of each session, read only the essential memory files needed to restore current project context (overview, active decisions, recent feedback, next steps). Read additional files from `.claude/memory/` only when relevant to the current task. On a new machine, copy the memory files into `~/.claude/projects/<project-path>/memory/` if needed for the local auto-memory system.

# Code Comments
- Only explain non-trivial decisions — avoid stating the obvious.
- make lite comments at the code only for context and at important points

# Unity UI Hierarchy
- All objects must be accessible via the Unity UI Hierarchy window unless there is a good reason otherwise.
- as generals make importan variables as SerializeField
- use Prefabs and Scriptable Objects reuse objects such player, enemies, traps, effects and etc.

# Unity C# Code Standards

Write production-quality Unity C# code as a professional.

## Design & Architecture
- Use design patterns when appropriate.
- Always implement features in a scalable, maintainable, and production-ready way — even if the request is simple.
- Avoid one-off or hardcoded solutions. Design systems with proper abstractions, clear separation of concerns, and future extensibility in mind.
- Keep scripts focused (single responsibility) and avoid large monolithic classes.
- Keep code modular and easy to extend or modify.

## Code Quality
- Write maintainable, readable, and efficient code.
- Prefer simple, clear, and maintainable solutions over clever ones.
- Follow Unity best practices and C# conventions.
- Use meaningful, consistent names for variables, methods, and classes.
- Write short, readable methods with minimal nesting.
- Handle edge cases and errors cleanly.
- Add logs in important places so i can get feedbacks when i test the game

## networking 
- For each new component, explicitly decide: does this state need to be seen by all clients? If yes → NetworkBehaviour. If no → MonoBehaviour. Document the reason in a comment.

## Performance
- Avoid unnecessary allocations and expensive operations in Update loops.

## Unity Patterns
- Use appropriate Unity patterns (MonoBehaviour, ScriptableObjects, events, singelton and etc) when relevant.

## When Modifying Existing Code
- Match the existing style and architecture.
- Integrate cleanly without hacks or duplication.

## Output
- Output clean, concise, and efficient code suitable for real game development.

## Game Design Decisions
- Never make in-game design decisions (which upgrades exist, their values, game balance, content) without asking first. Structural/technical decisions (canvas hierarchy, component wiring, code architecture) are fine to decide independently.
- Never create gameplay effects, VFX, audio, or content assets unless explicitly asked.


## git
- If you modified files, at the end of the task automatically run:

git add -A
git commit -m "<short descriptive commit message>"

- If the automatic commit fails, stop and report the exact error.
- Do not fix Git errors unless I explicitly asked for a Git-related task.