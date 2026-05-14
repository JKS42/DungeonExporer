using DungeonExporer.Dungeon;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Wires Ollama, dialogue UI, and spawns a minimal NPC + training dummy near the maze spawn.
    /// </summary>
    public sealed class LevelGameplayBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private DungeonLevelBuilder _dungeon;
        [SerializeField] private Vector3 _npcOffsetFromSpawn = new(2.2f, 0f, 2.2f);
        [SerializeField] private Vector3 _enemyOffsetFromSpawn = new(5f, 0.55f, 2.2f);

        private void Reset()
        {
            GameObject p = GameObject.Find("Player");
            if (p != null)
                _player = p.transform;
            _dungeon = FindFirstObjectByType<DungeonLevelBuilder>();
        }

        private void Start()
        {
            DialoguePanelController dialogue = FindFirstObjectByType<DialoguePanelController>();
            if (_player == null)
                _player = GameObject.Find("Player")?.transform;

            if (_inputActions == null)
                Debug.LogWarning("LevelGameplayBootstrap: assign the same Input Actions asset as the player (InputSystem_Actions).");

            Vector3 origin = Vector3.zero;
            bool haveSpawn = false;
            if (_dungeon != null && _dungeon.HasRecordedPlayerSpawn)
            {
                origin = _dungeon.LastPlayerSpawnWorld;
                haveSpawn = true;
            }

            if (!haveSpawn && _player != null)
                origin = new Vector3(_player.position.x, 0f, _player.position.z);

            SpawnNpc(origin, dialogue);
            SpawnTrainingDummy(origin);
        }

        private void SpawnNpc(Vector3 spawnFloor, DialoguePanelController dialogue)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Npc_Cap";
            go.transform.SetParent(transform, false);
            Vector3 pos = spawnFloor + _npcOffsetFromSpawn;
            pos.y = 0.9f;
            go.transform.position = pos;

            var npc = go.AddComponent<NpcInteractable>();
            npc.Wire(dialogue, _inputActions, _player);
        }

        private void SpawnTrainingDummy(Vector3 spawnFloor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TrainingDummy";
            go.transform.SetParent(transform, false);
            Vector3 pos = spawnFloor + _enemyOffsetFromSpawn;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            go.AddComponent<EnemyActor>();
        }
    }
}
