using UnityEditor;
using FrizzNet.Editor.Inspectors;
using FrizzNet.Samples;

namespace FrizzNet.Samples.Editor
{
    // --- Custom Inspector Subclasses for Samples ---

    [CustomEditor(typeof(DemoSpawnManager))]
    [CanEditMultipleObjects]
    public class DemoSpawnManagerEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(LobbyExample))]
    [CanEditMultipleObjects]
    public class LobbyExampleEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(ChatExample))]
    [CanEditMultipleObjects]
    public class ChatExampleEditor : FrizzHelpEditorBase { }
}
