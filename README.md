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

1. Do the local setup above, then run **Chess > Build Scene** (or open `Assets/Scenes/Main.unity`).
2. Press Play. You are White: click a piece, then a highlighted square (green = move, red = capture). The AI answers as Black.
3. **Undo** takes back your last move and the AI reply; **New Game** (top right) restarts. A pawn reaching the last rank asks which piece to become.

PlayMode tests (`Assets/Chess/PlayTests`) load the scene, click through a move and save screenshots to `Logs/shots/`.
