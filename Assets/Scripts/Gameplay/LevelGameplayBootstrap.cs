using DungeonExporer.Dungeon;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Wires Ollama, dialogue UI, NPC Cap, maze-scattered loot, hazards, and encounter enemies.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LevelGameplayBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private DungeonLevelBuilder _dungeon;

        [Header("NPC")]
        [Tooltip("Meshy Cap model (FBX under Assets/Models). Auto-assigned in the editor from GameplayModelPaths.NpcCapFbx.")]
        [SerializeField] private GameObject _npcModel;
        [Tooltip("Fallback offset from player spawn if no safe (S) floor is found.")]
        [SerializeField] private Vector3 _npcOffsetFromSpawn = new(2.5f, 0f, 2.5f);
        [SerializeField] private float _npcHeight = 1.65f;
        [Tooltip("Fine-tune Cap facing after auto-align (usually 0).")]
        [SerializeField] private float _npcVisualYawOffset;
        [SerializeField] private Texture2D _npcAlbedo;

        [Header("Scattered loot (walkable maze cells)")]
        [SerializeField] private int _scatteredPebbles = 12;
        [SerializeField] private int _scatteredRations = 7;
        [SerializeField] private int _spikeTrapCount = 16;
        [SerializeField] private int _encounterEnemyCount = 10;
        [Tooltip("Minimum grid cells (Chebyshev) from player spawn P where loot/hazards/enemies may appear.")]
        [SerializeField] private int _minCellsFromSpawn = 5;
        [SerializeField] private int _scatterSeed;
        [SerializeField] private float _pickupHeight = 0.35f;
        [SerializeField] private float _hazardHeight = 0.25f;
        [SerializeField] private float _enemyHeight = 1.1f;
        [SerializeField] private bool _preferCorridorSpikes = true;

        [Header("Hazard art")]
        [SerializeField] private Material _spikeTrapMaterial;

        [Header("Encounter foes")]
        [Tooltip("Meshy Grumblemite / foe model (FBX under Assets/Models). Auto-assigned from GameplayModelPaths.EnemyFbx.")]
        [SerializeField] private GameObject _enemyModel;
        [SerializeField] private float _enemyVisualYawOffset = 180f;
        [SerializeField] private Texture2D _enemyAlbedo;
        [SerializeField] private float _enemyMaxHealth = 45f;
        [SerializeField] private string _enemyDefeatQuestEventId = "defeated_dungeon_foe";

        private Transform _lootRoot;

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureEventSystem(_inputActions);
            ResolveModelsIfMissing();
        }

        private void Reset()
        {
            GameObject p = GameObject.Find("Player");
            if (p != null)
                _player = p.transform;
            _dungeon = FindFirstObjectByType<DungeonLevelBuilder>();
            ResolveModelsIfMissing();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveModelsIfMissing();
        }
#endif

        private void ResolveModelsIfMissing()
        {
#if UNITY_EDITOR
            if (_npcModel == null)
            {
                _npcModel = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayModelPaths.NpcCapFbx);
                if (_npcModel == null)
                    Debug.LogWarning(
                        "LevelGameplayBootstrap: NPC model not found at " + GameplayModelPaths.NpcCapFbx);
            }

            if (_enemyModel == null)
            {
                _enemyModel = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayModelPaths.EnemyFbx);
                if (_enemyModel == null)
                    Debug.LogWarning(
                        "LevelGameplayBootstrap: enemy model not found at " + GameplayModelPaths.EnemyFbx);
            }

            if (_npcAlbedo == null)
                _npcAlbedo = MeshyMaterialUtility.LoadAlbedoFromFbxPath(GameplayModelPaths.NpcCapFbx);
            if (_enemyAlbedo == null)
                _enemyAlbedo = MeshyMaterialUtility.LoadAlbedoFromFbxPath(GameplayModelPaths.EnemyFbx);
#endif
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

            _lootRoot = new GameObject("WorldLoot").transform;
            _lootRoot.SetParent(transform, false);

            SpawnNpc(origin, dialogue);
            ScatterWorldContent();
        }

        private void SpawnNpc(Vector3 spawnFloor, DialoguePanelController dialogue)
        {
            Vector3 pos;
            if (_dungeon != null && _dungeon.TryGetNpcHubPosition(out Vector3 hubPos))
                pos = hubPos;
            else
                pos = spawnFloor + _npcOffsetFromSpawn;

            pos.y = 0f;

            Vector3 lookAt = _player != null
                ? new Vector3(_player.position.x, 0f, _player.position.z)
                : spawnFloor;
            float faceYaw = CharacterVisualUtility.YawToward(pos, lookAt);
            GameObject go = CharacterVisualUtility.CreateRoot("Npc_Cap", pos, faceYaw);
            go.transform.SetParent(transform, false);

            if (CharacterVisualUtility.TryAttachModel(go, _npcModel, _npcHeight, 0f, false, _npcAlbedo))
            {
                Transform visual = go.transform.Find("Visual");
                if (visual != null)
                    CharacterVisualUtility.AlignVisualToward(visual.gameObject, lookAt, _npcVisualYawOffset);
            }
            else
            {
                Debug.LogWarning("LevelGameplayBootstrap: using fallback capsule for Npc_Cap (assign _npcModel).");
                CharacterVisualUtility.AddFallbackCapsule(go, _npcHeight, new Color(0.45f, 0.62f, 0.88f, 1f), false);
            }

            var npc = go.AddComponent<NpcInteractable>();
            npc.Wire(dialogue, _inputActions, _player);
        }

        private void ScatterWorldContent()
        {
            if (_dungeon == null)
            {
                Debug.LogWarning("LevelGameplayBootstrap: no DungeonLevelBuilder; skipping scatter.");
                return;
            }

            var config = new DungeonLootScatter.ScatterConfig
            {
                pebbleCount = _scatteredPebbles,
                rationCount = _scatteredRations,
                spikeTrapCount = _spikeTrapCount,
                encounterEnemyCount = _encounterEnemyCount,
                minCellsFromSpawn = _minCellsFromSpawn,
                randomSeed = _scatterSeed,
                pickupHeight = _pickupHeight,
                hazardHeight = _hazardHeight,
                enemyHeight = _enemyHeight,
                preferCorridorsForHazards = _preferCorridorSpikes
            };

            DungeonLootScatter.Scatter(_dungeon, _lootRoot, config, SpawnPickup, SpawnHazard, SpawnEnemy);
        }

        private void SpawnPickup(Vector3 position, string itemId, string displayName, int count, float healAmount,
            Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pickup_" + itemId;
            go.transform.SetParent(_lootRoot, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.42f;

            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;

            PickupBubbleVisual.Apply(go, itemId, healAmount, tint);

            var pickup = go.AddComponent<WorldPickup>();
            pickup.Configure(itemId, displayName, count, healAmount);
        }

        private void SpawnHazard(Vector3 position)
        {
            float cell = _dungeon != null ? _dungeon.CellSize : 2.5f;
            float pad = cell * 0.38f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SpikeHazard";
            go.transform.SetParent(_lootRoot, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(pad, 0.22f, pad);

            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_spikeTrapMaterial != null)
                    rend.sharedMaterial = _spikeTrapMaterial;
                else
                    rend.material.color = new Color(0.55f, 0.22f, 0.28f, 1f);
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            go.AddComponent<HazardVolume>();
        }

        private void SpawnEnemy(Vector3 position)
        {
            position.y = 0f;

            Vector3 faceTarget = _player != null
                ? new Vector3(_player.position.x, 0f, _player.position.z)
                : position + Vector3.forward;
            float faceYaw = CharacterVisualUtility.YawToward(position, faceTarget);
            GameObject go = CharacterVisualUtility.CreateRoot("DungeonFoe", position, faceYaw);
            go.transform.SetParent(_lootRoot, false);

            if (CharacterVisualUtility.TryAttachModel(go, _enemyModel, _enemyHeight, 0f, true, _enemyAlbedo))
            {
                Transform visual = go.transform.Find("Visual");
                if (visual != null)
                    CharacterVisualUtility.AlignVisualToward(visual.gameObject, faceTarget, _enemyVisualYawOffset);
            }
            else
            {
                Debug.LogWarning("LevelGameplayBootstrap: using fallback capsule for DungeonFoe (assign _enemyModel).");
                CharacterVisualUtility.AddFallbackCapsule(go, _enemyHeight, new Color(0.62f, 0.22f, 0.2f, 1f), true);
            }

            var enemy = go.AddComponent<EnemyActor>();
            enemy.Configure(_enemyMaxHealth, _enemyDefeatQuestEventId);
        }
    }
}
