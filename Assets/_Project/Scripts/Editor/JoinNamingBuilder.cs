using Game.Data;
using Game.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    /// <summary>
    /// Adds naming on join (2026-11-13) beside a scene's player manager, and in a kitchen the gate
    /// that keeps new players out during service and the Review.
    /// </summary>
    public static class JoinNamingBuilder
    {
        public static JoinNaming AddNaming(GameObject players, PlayerInputManager manager, PlayerJoinController join)
        {
            var naming = new SerializedObject(players.AddComponent<JoinNaming>());
            naming.FindProperty("_manager").objectReferenceValue = manager;
            naming.FindProperty("_joins").objectReferenceValue = join;
            naming.ApplyModifiedPropertiesWithoutUndo();
            return (JoinNaming)naming.targetObject;
        }

        public static KitchenJoinGate AddGate(GameObject players, PlayerInputManager manager, KitchenCompositionRoot root, CoopSettings settings)
        {
            var gate = new SerializedObject(players.AddComponent<KitchenJoinGate>());
            gate.FindProperty("_root").objectReferenceValue = root;
            gate.FindProperty("_manager").objectReferenceValue = manager;
            gate.FindProperty("_settings").objectReferenceValue = settings;
            gate.ApplyModifiedPropertiesWithoutUndo();
            return (KitchenJoinGate)gate.targetObject;
        }
    }
}
