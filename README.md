# Farm

A cozy top-down farming game built in Unity 6 with the Universal Render Pipeline, targeting WebGL. Grow crops, raise animals, cook dishes and fill truck orders until your farmhouse reaches its final level — all in one short browser session.

![Gameplay](docs/screenshot.png)

---

## Play It

Play it directly in browser on itch.io
https://rafaelpaz.itch.io/farmgame

---

### Controls

| Input | Action |
|---|---|
| **WASD** / **Arrow keys** | Move |
| **E** | Interact with whatever is in front of you — plant, harvest, collect, cook, sell, upgrade |
| **Left click** | Use menus and buttons |
| **Esc** | Close the open menu |
| **Cog button** (bottom right) | Open settings — music and sound volume, saved between sessions (pauses the game) |

## Gameplay

You start with 6 fields, unlimited seeds and a store to sell your crops. Walk up to a field and press **E**: plant a plot, wait for it to grow, harvest it, and sell the crop at the store. Coins go into the farmhouse, and every farmhouse upgrade physically grows the island and opens up something new. Reach **Level 5** to complete the game.

Raw crops sell cheaply, so the real game is turning them into something worth more. The stove turns ingredients into dishes worth several times their inputs, and from Level 3 delivery trucks pull up to the sell counter asking for a specific dish. A truck pays **two to three times** the store price, but only waits **80 seconds** before driving off — a countdown ring over the cab shows how long you have left. Mushrooms pop up around the farm to be foraged along the way.

Every timed thing — a growing crop, a dish on the stove, a waiting truck — shows the same countdown ring, and anything ready to collect floats a badge, so the whole farm reads at a glance. Reaching Level 5 plays a short celebration and ends the run with your stats: time taken, coins earned, dishes cooked and truck orders filled.

### Farmhouse Levels

| Level | Upgrade cost | Unlocks |
|---|---|---|
| 1 | — | Starting field, seed store, apple tree, wheat |
| 2 | 200 | The stove, carrots, cauliflower, bread and salad |
| 3 | 800 | Delivery trucks and the sell counter, corn, carrot soup and apple strudel |
| 4 | 3,200 | The ranch — chickens and a cow — plus cake, cheese, omelette and pizza |
| 5 | 15,000 | **Win** |

### Crops and Animals

| | Unlock | Produces | Time | Yield | Sells for |
|---|---|---|---|---|---|
| **Wheat** | Lv1 | Wheat | 16s | 4 | 8 |
| **Apple tree** | Lv1 | Apple | 50–70s | up to 3 | 22 |
| **Carrot** | Lv2 | Carrot | 35s | 4 | 20 |
| **Cauliflower** | Lv2 | Cauliflower | 50s | 4 | 30 |
| **Corn** | Lv3 | Corn | 120s | 3 | 90 |
| **Chicken** | Lv4 | Egg | 20–25s | up to 3 | 65 |
| **Cow** | Lv4 | Milk | 18–22s | up to 6 | 115 |
| **Mushroom** | Lv1 | Foraged | — | 1 | 18 |

Crops are harvested all at once. The apple tree and the animals keep producing on their own and bank what they make until they're full, so they reward coming back rather than standing and waiting.

### Recipes

| Dish | Unlock | Ingredients | Cook time | Sells for |
|---|---|---|---|---|
| **Bread** | Lv2 | 3 Wheat | 20s | 100 |
| **Salad** | Lv2 | 2 Cauliflower + 1 Carrot | 25s | 180 |
| **Carrot Soup** | Lv3 | 3 Carrot + 1 Mushroom | 30s | 240 |
| **Apple Strudel** | Lv3 | 1 Bread + 3 Apple | 35s | 400 |
| **Cheese** | Lv4 | 2 Milk | 30s | 500 |
| **Omelette** | Lv4 | 3 Egg + 1 Milk | 35s | 670 |
| **Cake** | Lv4 | 2 Corn + 2 Egg + 1 Milk | 45s | 860 |
| **Pizza** | Lv4 | 1 Bread + 1 Cheese + 2 Corn | 50s | 1,520 |

Later recipes feed on earlier ones — pizza needs bread and cheese — so a strong run keeps the stove busy and plans a step ahead.

---

## Technical Highlights

**Data-driven content.** Crops, items, recipes, animals, character actions, sound events and the farmhouse upgrade ladder are all ScriptableObjects, so every number in the tables above is tuned in the inspector without touching code.

**A farm that grows.** Each farmhouse level switches on a new section of the island and widens the camera's bounds, so the world expands in step with what the player can afford rather than laying everything out at once.

**One countdown, many clocks.** Crops, the stove and trucks all implement a single `ITimedProgress` interface, so the same countdown ring works over any of them without knowing what it is timing.

**Tuned for WebGL.** Music streams as compressed audio and sound effects are small mono clips. Rendering uses 2x MSAA, soft shadows and trimmed post-processing with no screen-space ambient occlusion, and the editor's quality settings match the web build's apart from render scale, so what you tune is what ships.

**Editor tooling.** Custom tools render item icons from 3D models and preview each farm level from one window.

---

## Built With

| Technology | Use |
|---|---|
| **Unity 6** (6000.5.2f1) | Engine |
| **Universal Render Pipeline** | Rendering, targeting WebGL |
| **Unity Input System** | Keyboard, mouse and gamepad input |
| **Unity UI (uGUI) + TextMesh Pro** | All menus, HUD and world-space prompts |
| **Unity AI Assistant** | Editor automation bridge used during development |
| **ComfyUI** | Stable Audio 3 for sound effects, Z-Image Turbo for item icons |
| **Claude Code** | AI pair programming — gameplay systems, audio, tooling and project cleanup |

---

## Asset Credits

The game's code is original and was mostly done by Claude Code. Art and audio came from:

| Source | Used for |
|---|---|
| **nobonoko** — *How Many Cats Are There In This Picture?* (2005) | Background music |
| **AssetHunts! — GameDev Starter Kit: Farming** | Ground tiles, crop growth stages, fences, trees |
| **Cozy Farm Asset Pack** | Farmhouse, delivery trucks, bushes and farm props |
| **Pandazole Lowpoly Bundle** | Store building, food models and props |
| **Neko Cat Free Edition** | The player cat and the truck drivers |
| **Layer Lab — GUI Pro: Fantasy RPG** | UI icons and fonts (Alata, Josefin Sans) |
| **Mixamo** | Character animations |
| **Generated with Stable Audio 3** (ComfyUI) | Sound effects |
| **Generated with Z-Image Turbo** (ComfyUI) | Item icons and the interact key prompt |
