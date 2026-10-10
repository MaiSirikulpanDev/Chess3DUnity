# Chess3DUnity
A single-player 3D chess game. Play White against a built-in AI. Built with Unity 6 (URP) and low-poly models from the Synty POLYGON Starter Pack; the rules engine is plain C#, separate from Unity, and developed test-first.

## Local setup (paid/licensed assets are not in this repo)

1. Open with Unity 6000.4.9f1.
2. Import **Low Poly Chess Pack** (Broken Vector) from the Asset Store.
3. Convert `Assets/BrokenVector/LowpolyChessPack/Materials/Chess Material.mat` to **Universal Render Pipeline/Lit**
   (the pack ships a Built-in Standard material, which renders magenta in URP). Move `_MainTex` to `_BaseMap`.

## Tests

`dotnet test DotnetTests` (fast) or Unity Test Runner, EditMode.

## Play

1. Do the local setup above, then run **Chess > Build Scene** (or open `Assets/Scenes/Main.unity`). The game opens on a title screen: **Start Game** leads to a setup screen where you pick the difficulty and the colour you play; **Exit Game** quits. The **Menu** button in the game returns to the title screen.
2. Press Play. You are White: click a piece, then a highlighted square (green = move, red = capture). The AI answers as Black.
3. Turn the camera with A/D or left/right arrows and tilt it with W/S or up/down arrows (or right-mouse drag); the lowest it goes is level with the board. **Flip View** swings it to the other side. **Level** cycles Easy/Normal/Hard (remembered between runs). The last move is ringed in orange, a king in check in red, and the moves are listed at the top left. **Undo** takes back your last move and the AI reply; **New Game** (top right) restarts. A pawn reaching the last rank asks which piece to become.

PlayMode tests (`Assets/Chess/PlayTests`) load the scene, click through a move and save screenshots to `Logs/shots/`.

## Forest scene

The board stands on a giant tree stump in a clearing, ringed by pines and broadleaf trees (Polytope Studio
**Low Poly Environment - Nature Free**). After importing that pack, also import its
`Lowpoly_Environments/URP/PT_Nature_Free_URP_17.unitypackage` (the pack's shaders are Built-in otherwise), then run
**Chess > Build Scene**. The forest uses a fixed random seed, so rebuilding gives the same layout.
