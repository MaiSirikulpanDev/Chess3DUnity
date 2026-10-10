using Chess.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Chess > Build Scene: assembles Assets/Scenes/Main.unity from the Low Poly Chess Pack prefabs.
public static class SetupScene
{
    const string Prefabs = "Assets/BrokenVector/LowpolyChessPack/Prefabs/";

    [MenuItem("Chess/Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var board = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Board.prefab"));
        board.transform.rotation = Quaternion.Euler(0, 90, 0); // puts a dark square on a1, bottom-left for White

        const string matPath = "Assets/Chess/Highlight.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        var view = new GameObject("Chess").AddComponent<ChessView>();
        view.whitePrefabs = Load("White ");
        view.blackPrefabs = Load("Black ");
        view.highlight = mat;

        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0, 14, -12), Quaternion.Euler(50, 0, 0));
        cam.fieldOfView = 46;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
    }

    static GameObject[] Load(string colour)
    {
        var names = new[] { "King", "Queen", "Bishop", "Knight", "Rook", "Pawn" };
        var result = new GameObject[6];
        for (int i = 0; i < 6; i++) result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + colour + names[i] + ".prefab");
        return result;
    }
}
