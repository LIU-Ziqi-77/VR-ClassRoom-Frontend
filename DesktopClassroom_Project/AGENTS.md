# AGENTS.md

## Project context

This is a Unity-based VR Classroom research prototype for teacher training.

The intended user is a novice or early-career teacher who enters a simulated classroom through a VR headset. In this virtual classroom, the teacher can practice teaching, classroom management, and responding to student behaviors without affecting real students.

The classroom contains multiple virtual student agents. These agents are intended to simulate classroom-relevant behaviors such as:
- paying attention
- raising hands
- asking questions
- showing confusion
- getting distracted
- side-talking
- leaving their seats
- reacting to the teacher's instruction

The long-term vision is that each virtual student can be controlled or influenced by an AI/LLM-based agent. The teacher's speech may eventually be captured through speech-to-text and used as context for student-agent behavior generation. However, do not assume the full LLM backend, real-time speech-to-text pipeline, or complete multi-agent reasoning system is implemented unless explicitly requested.

The current development focus is on the Unity front end and research-demo reliability:
- stable VR interaction
- believable and interpretable student behaviors
- visible animation and behavior presentation
- Unity scene stability
- student animation/behavior control
- mock, scripted, or rule-based agent behavior simulation
- desktop and VR testing workflows

This is a research prototype, not a production VR training product. Prioritize small, reliable, demonstrable behavior slices over broad architectural rewrites. It is acceptable to use mock data, scripted triggers, keyboard controls, inspector buttons, or simple rule-based behavior logic when testing front-end behavior.

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