# Project Overview
- **Game Title**: Viking River Rowers
- **High-Level Concept**: A stylized 3D endless Viking longship river runner where the player navigates a treacherous river, dodges obstacles (rocks, logs, barrels) across three lanes, and survives high-intensity rapid-rowing current surges.
- **Players**: Single player
- **Inspiration / Reference Games**: Temple Run (lane movement), Subway Surfers, Flappy Bird (rapid-rowing rhythm)
- **Tone / Art Direction**: Stylized, colorful, low-poly 3D using primitives
- **Target Platform**: PC (MacOS/Windows) and Mobile (Android/iOS)
- **Screen Orientation / Resolution**: Landscape (1920x1080) with responsive UI layout
- **Render Pipeline**: Universal Render Pipeline (URP)

---

# Game Mechanics

## Core Gameplay Loop
1. **Navigate**: The Viking longship is positioned near the bottom of the screen. The river scrolls towards the player, conveying forward movement.
2. **Dodge**: Obstacles (rocks, logs, barrels) spawn at the horizon in one of three lanes (Left, Center, Right). They move towards the player. The player must steer left or right to avoid them.
3. **Survive Rapids**: Periodically, the game enters a "Rapid-Rowing Phase" where the current surges. The ship is pushed backward (towards the bottom of the screen). The player must rapidly boost (space, down arrow, or double-down swipe/tap) to paddle forward, maintaining their position while still dodging obstacles. If pushed past the bottom edge, it is a game over.
4. **Escalate**: The distance scored increases over time. The scroll speed and obstacle spawn rate scale up, increasing difficulty.
5. **Score**: High score is saved and displayed when the ship crashes.

## Controls and Input Methods
Supported inputs using the **New Input System** (fully supporting keyboard, mouse-simulation, and mobile touch):
- **Lane Movement**: 
  - Keyboard: `A` / `D` or `Left Arrow` / `Right Arrow` (steers to left/right lane instantly or smoothly).
  - Swipes/Mouse Drags: Quick horizontal drag or swipe left/right.
- **Rapid-Rowing Boost**:
  - Keyboard: `S` or `Down Arrow` or `Space`.
  - Swipes/Mouse Drags: Downward swipe, mouse double-click, or screen tap.
- **UI Interaction**: Mouse clicks and screen taps for Menu, Play, Restart.

---

# UI
- **Start Menu Overlay**: Displays Game Title, high score, and a "PADDLE TO START" pulsing button.
- **In-Game HUD**:
  - **Score**: Current distance traveled (meters).
  - **Level/Speed**: Current difficulty indicator.
  - **Phase Alert**: Displays "RAPID SURGE!" or "NORMAL WATER" with flashing colors.
  - **Boost Indicator**: Visual flash/gauge showing active rowing velocity or pushback distance.
- **Game Over Overlay**: Displays "SHIPWRECKED!", final score, high score, and a "ROW AGAIN" (Restart) button.

---

# Key Asset & Context

We will create a clean and organized directory structure:
`Assets/VikingRiverRowers/Scripts/`
`Assets/VikingRiverRowers/Prefabs/`
`Assets/VikingRiverRowers/Materials/`
`Assets/VikingRiverRowers/Scenes/`

### Visual Assets (Constructed from 3D Primitives):
1. **Viking Longship**: 
   - Hull (scaled Cube or Cylinder, stylized with curved prow/stern using rotated Cubes).
   - Mast & Sail (Cylinder & flat Quad/Cube with a red/white striped material).
   - Oars (Cylinder shafts with flat paddle ends, attached to the hull's sides).
   - Rowers (Simple Capsule dummies with small horn Cylinder helmets).
2. **Obstacles**:
   - Rock (Grey Sphere/Cube with randomized scale and rotation).
   - Log (Brown horizontal Cylinder).
   - Barrel (Brown vertical Cylinder with metal ring details using torus/cylinder).
3. **River Environment**:
   - Riverbanks (Tall green/brown Cubes lined with simple primitive Pine Trees: Green Cones on Brown Cylinders).
   - Water Tile (Semi-transparent blue Plane or Cube with scrolling textures or procedurally animated wave elements).

---

# Implementation Steps

### Step 1: Base Folder Structure & Scene Setup
- **Description**: Create directories under `Assets/VikingRiverRowers/` for Scripts, Prefabs, Materials, and Scenes. Create the main `VikingRiverRowersScene` with a camera angled at $30^\circ$ looking down the river from above-behind the player (e.g., Camera Position: `(0, 6, -8)`, Rotation: `(25, 0, 0)`). Setup basic lighting.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

### Step 2: Game State Manager (`GameManager.cs`)
- **Description**: Implement a persistent singleton `GameManager` to manage state: `Menu`, `Playing`, `RapidPhase`, `GameOver`. Track current distance/score, high scores, elapsed time, current river speed, and difficulty scaling.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Player Controller & Lane Movement (`PlayerController.cs`)
- **Description**: Manage 3-lane positioning ($X = -3, 0, 3$). Handle New Input System inputs (Keyboard: A/D/Left/Right, S/Down/Space; Mouse/Touch swipes). Interpolate $X$ positions smoothly for clean steering. Handle visual rolling/leaning into turns and water bobbing ($Y$ wave offset).
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

### Step 4: Rapid-Rowing Pushback System (`PlayerController.cs` & `GameManager.cs`)
- **Description**: Add rapid-rowing surge mechanics. Periodically, the current speed doubles and pushes the player ship in the negative $Z$ direction (e.g., from $Z = 0$ down to $Z = -5$). The player must repeatedly boost to push the ship back to safety ($Z \ge 0$). If $Z < -5$, trigger a Game Over.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

### Step 5: Oar Animation Controller (`OarAnimator.cs`)
- **Description**: Programmatically animate oars attached to the ship's sides. Row in a rhythmic elliptical or sinusoidal rotation loop. Accelerate the rowing frequency during the Rapid-Rowing phase or when the player taps to boost, giving immediate tactile feedback.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: Yes

### Step 6: Environment Scroller & Recycling (`EnvironmentScroller.cs` & `EnvironmentManager.cs`)
- **Description**: Create a procedural, endless scrolling system. Water and riverbank segments are spawned ahead, scroll down in the $-Z$ direction at the active river speed, and are recycled/repositioned when they pass behind the camera.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

### Step 7: Obstacle Spawner & Movement (`ObstacleSpawner.cs` & `Obstacle.cs`)
- **Description**: Spawn obstacles at the horizon ($Z = 45$) in lanes ($X = -3, 0, 3$). Obstacles travel in $-Z$. Generate random obstacle patterns (e.g., Rock in lane 1, Log in lane 2, lane 3 clear) ensuring **at least one lane is always open**. Increase spawning rate and speed over time. Handle collision detection via triggers (causing crash/GameOver).
- **Assigned role**: developer
- **Dependencies**: Step 3, Step 6
- **Parallelizable**: No

### Step 8: Stylized 3D Art Assembly & Prefabs
- **Description**: Construct 3D visual models using Unity primitive meshes (Cylinder, Cube, Sphere, Cone). Assign simple colored, unlit, or stylized materials (water, wood, sail, banks, rocks). Assemble the Viking Longship, Rocks, Logs, Barrels, and Riverbanks, and save them as Prefabs.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

### Step 9: UI Manager (`UIManager.cs`)
- **Description**: Build a responsive Canvas-based UI containing the Start Menu, In-game HUD (meters, rapid warning banner, boost alert), and Game Over panel. Hook up buttons to `GameManager` methods for start and restart.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

### Step 10: Assembly, Integration & Validation
- **Description**: Combine all elements in the scene: place the environment manager, spawner, UI Canvas, and the player longship. Conduct a comprehensive playtest to verify obstacle clearing, speed progression, rapid-rowing mechanics, and score tracking. Resolve any console errors or warnings.
- **Assigned role**: developer
- **Dependencies**: All preceding steps
- **Parallelizable**: No

---

# Verification & Testing

### Test Cases & Checklist:
1. **Lane Alignment**: Verify ship centers precisely in $X = -3, 0, 3$ when steering. Steering must be smooth, not teleporting.
2. **Obstacle Lane Guarantee**: Spawn 100 waves programmatically in a dry-run test and assert that *never* are all 3 lanes blocked in a single wave.
3. **Collision Detection**: Crashing into an obstacle or steering too far into the banks triggers the game-over screen instantly.
4. **Rapid Surge Balancing**: Verify the ship drift rate in the rapid phase is challenging but completely fair. Pressing boost moves the ship forward by a responsive, fixed increment.
5. **Score progression**: Ensure distance scales correctly with river speed and elapsed time.
6. **Input responsiveness**: Verify that A/D, Arrow keys, space, swipes, and mouse clicks work flawlessly on both desktop and mobile layouts.
7. **No Console Logs/Errors**: Run the scene and ensure no null reference exceptions or warnings appear in the Unity Console.
