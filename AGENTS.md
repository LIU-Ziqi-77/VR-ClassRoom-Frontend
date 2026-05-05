# AGENTS.md

## Project context
This is a Unity-based VR Classroom research prototype for simulating multiple AI-driven student agents in a virtual classroom.

The system prioritizes:
- stable VR interaction
- visible and interpretable student behaviors
- incremental development
- research-demo reliability

Current focus:
- front-end behavior presentation
- Unity scene stability
- student animation/behavior control
- basic agent behavior simulation

Do not assume the LLM backend must be fully implemented unless explicitly requested.

## Communication style
- Be objective, rigorous, and critical.
- Do not flatter, overpraise, or agree too quickly.
- Distinguish clearly between implemented behavior, assumptions, and speculative ideas.
- When discussing research ideas or related work, do not present speculative claims as established facts.

## Workflow
- Before writing substantial code, first propose a minimal implementation plan.
- Prefer the smallest runnable slice first.
- Keep changes localized unless a broader refactor is explicitly requested.
- Preserve existing working behavior unless the requested change requires modifying it.
- When uncertain about Unity, XR Interaction Toolkit, VRM, animation rigs, or external APIs, search or inspect documentation/repo code instead of guessing.

## Unity safety rules
- Do not delete scene objects, prefabs, animation clips, animator controllers, avatar rigs, or XR settings without explicit approval.
- Do not rename important GameObjects, bones, prefabs, or serialized fields unless necessary.
- Do not make broad scene hierarchy changes unless explicitly requested.
- Prefer adding small scripts/components over rewriting existing systems.

## Coding workflow
- Before writing substantial code, first propose a minimal implementation plan.
- Explain likely risks before changing animation, rigging, XR, or scene-control code.
- After implementation, summarize:
  - files changed
  - what was changed
  - how to test it in Unity
  - known limitations

## Tool usage
- Use external tools such as GitHub, Unity docs, package docs, or paper/document access when external evidence is needed.
- When discussing ideas, related work, or external implementations, proactively search for relevant evidence when it is likely to improve accuracy or sharpen criticism. 
- Use subagents only for clearly separable complex tasks.
- Avoid spawning subagents for casual brainstorming or small edits. 
- For complex tasks, it is acceptable to spawn a small number of subagents when doing so materially improves parallel exploration, implementation, or verification.