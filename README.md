# Viking Rowing Game

A mobile rhythm-rowing game set on an endless Nordic river.

Control a Viking longship by tracing rowing strokes with both thumbs, synchronize each pull with the drumbeat, and build enough momentum to travel as far as possible.

> **Project status:** Early prototype. The gameplay, visual direction, and technical architecture are still under development.

## Game Concept

The player’s longship remains near the center of a fixed 2.5D/3D camera while the river and surrounding environment move past it.

The goal is to make every rowing stroke feel physical. The player should feel the resistance of the water, the pull of the oars, the release of each stroke, and its impact on the ship’s movement.

The game combines the accessibility of an endless runner with a skill-based rhythm system:

* Strong and well-timed strokes increase the ship’s speed.
* Consistent rowing builds combos and fills the boost meter.
* Weak, inaccurate, or poorly coordinated strokes reduce momentum.

## Core Gameplay Loop

1. Follow the rowing pattern displayed on the screen.
2. Trace the left and right strokes using your thumbs.
3. Match each stroke to the drumbeat.
4. Maintain rhythm and coordination to build speed.
5. Fill the boost meter through successful rowing.
6. Travel as far as possible and improve your score.


## Technical Direction

* **Engine:** Unity
* **Rendering:** Universal Render Pipeline (URP)
* **Camera:** Cinemachine fixed-follow camera
* **Target platform:** Mobile
* **World generation:** Recycled, chunk-based endless river
* **Water:** Shader Graph
* **Effects:** Unity Particle System or GPU particles
* **Post-processing:** Fog, bloom, color grading, and speed effects
* **Input:** Dual-touch gesture recognition
* **Performance goal:** Stable mobile performance with responsive touch controls


## Roadmap

### Prototype

* [ ] Implement dual-thumb gesture recognition
* [ ] Evaluate stroke timing and accuracy
* [ ] Add longship movement and momentum
* [ ] Add drumbeats and visual rhythm cues
* [ ] Implement the boost meter
* [ ] Create endless river chunk recycling
* [ ] Add basic scoring and combos
* [ ] Test performance on mobile devices

### Gameplay Expansion

* [ ] Add more rowing patterns
* [ ] Introduce multiple difficulty levels
* [ ] Add obstacles and changing river conditions
* [ ] Expand the score and progression systems
* [ ] Add unlockable ship cosmetics
* [ ] Add potions or temporary run modifiers
* [ ] Improve audio, haptics, water effects, and camera feedback
* [ ] Add missions, achievements, and leaderboards

### Multiplayer Concept

* [ ] Add asynchronous shadow races
* [ ] Support competitive runs for two to six players
* [ ] Add friend challenges
* [ ] Add multiplayer leaderboards
* [ ] Allow players to race against previous runs

## Design Principles

* **Easy to understand, difficult to master**
* **Every rowing stroke should feel responsive**
* **Rhythm should affect movement, not only score**
* **Good coordination should create visible momentum**
* **The environment should feel alive without hurting mobile performance**
* **Failure should be easy to understand and encourage another run**

## Contributing

This is currently a personal game project.

Contribution guidelines, coding conventions, and development documentation will be added as the prototype develops.

## License

**All rights reserved.**

No permission for reuse, modification, or redistribution has been granted at this stage.
