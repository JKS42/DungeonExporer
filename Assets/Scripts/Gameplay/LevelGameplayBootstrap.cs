using System.Collections;
using System.Collections.Generic;
using DungeonExporer.Dungeon;
using DungeonExporer.Player;
using DungeonExporer.Settings;
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
        [SerializeField] private OllamaHandler _ollama;

        [Header("AI trap placement")]
        [Tooltip("When enabled and Ollama is available, Cap's model chooses trap cells (validated in C#).")]
        [SerializeField] private bool _useAiTrapPlacement = true;
        [SerializeField] private int _aiMaxTraps = 12;
        [SerializeField] private float _aiTrapPlanTimeoutSeconds = 8f;

        [Header("AI loot / enemies / signs")]
        [Tooltip("When enabled and Ollama is available, Cap's model chooses loot, foes, and sign text (validated in C#).")]
        [SerializeField] private bool _useAiContentPlacement = true;
        [SerializeField] private int _aiMaxLoot = 14;
        [SerializeField] private int _aiMaxEnemies = 8;
        [SerializeField] private int _aiMaxSigns = 6;
        [SerializeField] private float _aiContentPlanTimeoutSeconds = 8f;

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
        [SerializeField] private float _enemyAggroRange = 12f;
        [SerializeField] private float _enemyAttackRange = 2.4f;
        [SerializeField] private float _enemyMoveSpeed = 2.6f;
        [SerializeField] private float _enemyDamage = 14f;
        [SerializeField] private float _enemyAttackCooldown = 1.1f;

        private Transform _lootRoot;
        private readonly HashSet<Vector2Int> _scatterReserved = new HashSet<Vector2Int>();
        private DungeonTrapPlan _cachedTrapPlan;
        private bool _trapPlanDone;
        private DungeonContentPlan _cachedContentPlan;
        private bool _contentPlanDone;

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureEventSystem(_inputActions);
            ResolveModelsIfMissing();
            if (_ollama == null)
                _ollama = FindFirstObjectByType<OllamaHandler>();
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

            EnsurePlayerCombat();

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
            StartCoroutine(PrefetchAiPlansSequential());
            StartCoroutine(PlaceDungeonWhenReady());
        }

        private IEnumerator PrefetchAiPlansSequential()
        {
            // Give the player a window to talk to Cap before level-load planners use Ollama.
            yield return new WaitForSecondsRealtime(4f);

            _trapPlanDone = false;
            _contentPlanDone = false;
            _cachedTrapPlan = null;
            _cachedContentPlan = null;

            if (_useAiTrapPlacement && GameSettings.LlmEnabled && _ollama != null && _dungeon != null)
            {
                yield return WaitUntilDialogueClosed();
                yield return DungeonTrapPlanner.FetchPlanCoroutine(
                    _ollama,
                    _dungeon,
                    _aiMaxTraps,
                    _minCellsFromSpawn,
                    plan => { _cachedTrapPlan = plan; },
                    () => { });
            }

            _trapPlanDone = true;

            if (_useAiContentPlacement && GameSettings.LlmEnabled && _ollama != null && _dungeon != null)
            {
                yield return WaitUntilDialogueClosed();
                yield return DungeonContentPlanner.FetchPlanCoroutine(
                    _ollama,
                    _dungeon,
                    _aiMaxLoot,
                    _aiMaxEnemies,
                    _aiMaxSigns,
                    _minCellsFromSpawn,
                    plan => { _cachedContentPlan = plan; },
                    () => { });
            }

            _contentPlanDone = true;
        }

        private static IEnumerator WaitUntilDialogueClosed()
        {
            while (DialoguePanelController.IsOpen)
                yield return null;
        }

        private void EnsurePlayerCombat()
        {
            if (_player == null)
                return;

            PlayerCombat combat = _player.GetComponent<PlayerCombat>();
            if (combat == null)
                combat = _player.gameObject.AddComponent<PlayerCombat>();

            Transform camera = _player.GetComponentInChildren<Camera>()?.transform;
            if (camera == null && Camera.main != null)
                camera = Camera.main.transform;

            combat.Wire(_inputActions, camera);
        }

        private IEnumerator PlaceDungeonWhenReady()
        {
            float contentWaited = 0f;
            while (!_contentPlanDone && contentWaited < _aiContentPlanTimeoutSeconds)
            {
                contentWaited += Time.unscaledDeltaTime;
                yield return null;
            }

            ScatterLootAndEnemies(_cachedContentPlan);
            SpawnSigns(_cachedContentPlan);

            if (_cachedContentPlan != null && !string.IsNullOrWhiteSpace(_cachedContentPlan.CapNote))
                DungeonFlavorHudBridge.PublishFlavorToast?.Invoke(_cachedContentPlan.CapNote.Trim(), 5f);

            float trapWaited = 0f;
            while (!_trapPlanDone && trapWaited < _aiTrapPlanTimeoutSeconds)
            {
                trapWaited += Time.unscaledDeltaTime;
                yield return null;
            }

            ScatterTraps(_cachedTrapPlan);

            if (_cachedTrapPlan != null && !string.IsNullOrWhiteSpace(_cachedTrapPlan.CapNote))
                DungeonFlavorHudBridge.PublishFlavorToast?.Invoke(_cachedTrapPlan.CapNote.Trim(), 6f);
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

        private void ScatterLootAndEnemies(DungeonContentPlan aiPlan)
        {
            if (_dungeon == null)
            {
                Debug.LogWarning("LevelGameplayBootstrap: no DungeonLevelBuilder; skipping scatter.");
                return;
            }

            _scatterReserved.Clear();
            var config = BuildScatterConfig();
            config.spikeTrapCount = 0;

            IReadOnlyList<PlannedLoot> loot = aiPlan != null ? aiPlan.Loot : null;
            IReadOnlyList<PlannedEnemy> enemies = aiPlan != null ? aiPlan.Enemies : null;
            DungeonLootScatter.ScatterLoot(_dungeon, config, loot, _scatterReserved, SpawnPickup);
            DungeonLootScatter.ScatterEnemies(_dungeon, config, enemies, _scatterReserved, SpawnEnemy);
        }

        private void SpawnSigns(DungeonContentPlan aiPlan)
        {
            if (_dungeon == null || aiPlan == null || aiPlan.Signs.Count == 0)
                return;

            float cell = _dungeon.CellSize;
            for (int i = 0; i < aiPlan.Signs.Count; i++)
            {
                PlannedSign sign = aiPlan.Signs[i];
                Vector3 pos = _dungeon.CellCenterWorld(sign.Cell);
                DungeonSignPost.Create(_lootRoot, pos, sign.Text, cell);
            }
        }

        private void ScatterTraps(DungeonTrapPlan aiPlan)
        {
            if (_dungeon == null)
                return;

            var config = BuildScatterConfig();
            IReadOnlyList<PlannedTrap> traps = aiPlan != null ? aiPlan.Traps : null;
            DungeonLootScatter.ScatterTraps(_dungeon, config, traps, _scatterReserved, SpawnHazard);
        }

        private DungeonLootScatter.ScatterConfig BuildScatterConfig() => new DungeonLootScatter.ScatterConfig
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

        private void SpawnHazard(Vector3 position, DungeonTrapType trapType = DungeonTrapType.Spike)
        {
            float cell = _dungeon != null ? _dungeon.CellSize : 2.5f;
            float pad = cell * 0.38f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = trapType switch
            {
                DungeonTrapType.Ember => "EmberTrap",
                DungeonTrapType.Slime => "SlimeTrap",
                _ => "SpikeHazard"
            };
            go.transform.SetParent(_lootRoot, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(pad, 0.22f, pad);

            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                if (trapType == DungeonTrapType.Spike && _spikeTrapMaterial != null)
                    rend.sharedMaterial = _spikeTrapMaterial;
                else
                {
                    Material mat = rend.material;
                    mat.color = trapType switch
                    {
                        DungeonTrapType.Ember => new Color(0.95f, 0.42f, 0.1f, 1f),
                        DungeonTrapType.Slime => new Color(0.24f, 0.78f, 0.34f, 1f),
                        _ => new Color(0.88f, 0.32f, 0.26f, 1f)
                    };
                    mat.EnableKeyword("_EMISSION");
                }
            }

            var hazardVisual = go.AddComponent<HazardTrapVisual>();
            hazardVisual.Configure(trapType);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var hazard = go.AddComponent<HazardVolume>();
            hazard.Configure(trapType);
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

            var melee = go.AddComponent<EnemyMeleeAI>();
            melee.Configure(
                _enemyAggroRange,
                _enemyAttackRange,
                _enemyMoveSpeed,
                _enemyDamage,
                _enemyAttackCooldown);
        }
    }
}
