using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// Spawns floor, walls, ceiling, lighting, and encounter volumes from an ASCII grid in a <see cref="TextAsset"/> (maze + safe / encounter rooms).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DungeonLevelBuilder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Text file: one maze row per line. Lines starting with ; are comments. Symbols # . S E P (same width on every row).")]
        [SerializeField] private TextAsset _mazeLayout;
        [SerializeField] private Transform _player;

        [Header("Scale")]
        [SerializeField] private float _cellSize = 2.5f;
        [SerializeField] private float _wallHeight = 5.5f;
        [SerializeField] private float _floorThickness = 0.22f;

        [Header("Placement")]
        [SerializeField] private Vector3 _gridOrigin;

        [Header("Walls")]
        [Tooltip("URP Lit material with brick base map. Leave empty to use a flat procedural color.")]
        [SerializeField] private Material _wallMaterial;
        [Tooltip("Approximate world width of one brick column; tiling is derived from wall cube size.")]
        [SerializeField] private float _brickWorldMeters = 1.35f;

        [Header("Floors")]
        [Tooltip("URP Lit material with stone floor base map. Leave empty to use flat corridor / safe / encounter colors.")]
        [SerializeField] private Material _floorMaterial;
        [Tooltip("Approximate world width of one floor tile in meters.")]
        [SerializeField] private float _floorTileWorldMeters = 1.25f;

        [Header("Ceiling")]
        [SerializeField] private bool _buildCeiling = true;
        [SerializeField] private float _ceilingThickness = 0.32f;
        [Tooltip("Uses floor material with a dark tint when empty.")]
        [SerializeField] private Material _ceilingMaterial;

        [Header("Lighting")]
        [SerializeField] private bool _setupLighting = true;
        [SerializeField] private float _directionalIntensity = 1.55f;
        [SerializeField] private Color _ambientFill = new(0.38f, 0.36f, 0.34f, 1f);
        [SerializeField] private int _maxTorches = 56;
        [Tooltip("Minimum grid cells between torch point lights (Chebyshev).")]
        [SerializeField] private int _torchSpacing = 3;
        [SerializeField] private float _torchIntensity = 16f;
        [SerializeField] private float _torchRange = 18f;
        [SerializeField] private float _torchHeightFactor = 0.72f;

        [Header("Encounter gameplay")]
        [Tooltip("Quest/world event id fired the first time the player enters any <c>E</c> encounter cell volume.")]
        [SerializeField] private string _encounterEnterQuestEventId = "entered_encounter_zone";

        /// <summary>Floor center (XZ) of the maze <c>P</c> spawn after the last successful <see cref="Build"/>; Y is floor level.</summary>
        public Vector3 LastPlayerSpawnWorld { get; private set; }

        /// <summary>True after <see cref="Build"/> placed the player at the maze <c>P</c> cell.</summary>
        public bool HasRecordedPlayerSpawn { get; private set; }

        /// <summary>Walkable floor cells (<c>.</c>, <c>S</c>, <c>E</c>) after the last successful <see cref="Build"/>.</summary>
        public IReadOnlyList<Vector2Int> WalkableCells => _walkableCells;

        public Vector2Int SpawnCell => _spawnCell;

        public float CellSize => _cellSize;

        private string[] _gridRows = System.Array.Empty<string>();
        private readonly List<Vector2Int> _walkableCells = new List<Vector2Int>(256);
        private Vector2Int _spawnCell = new Vector2Int(-1, -1);

        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Material _matWall;
        private Material _matCorridor;
        private Material _matSafe;
        private Material _matEncounter;
        private MaterialPropertyBlock _wallPropBlock;
        private MaterialPropertyBlock _floorPropBlock;
        private MaterialPropertyBlock _ceilingPropBlock;

        private void Awake()
        {
            _gridRows = ParseMazeLayout(_mazeLayout);
            if (!ValidateGrid())
                return;
            _wallPropBlock = new MaterialPropertyBlock();
            _floorPropBlock = new MaterialPropertyBlock();
            _ceilingPropBlock = new MaterialPropertyBlock();
            EnsureMaterials();
            Build();
        }

        private static string[] ParseMazeLayout(TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("DungeonLevelBuilder: Assign a Maze Layout TextAsset on the Dungeon object.");
                return System.Array.Empty<string>();
            }

            var rows = new List<string>();
            string text = asset.text ?? string.Empty;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(";"))
                    continue;
                rows.Add(line.TrimEnd());
            }

            return rows.Count == 0 ? System.Array.Empty<string>() : rows.ToArray();
        }

        private bool ValidateGrid()
        {
            if (_gridRows.Length == 0)
            {
                Debug.LogError("DungeonLevelBuilder: Maze layout has no grid rows.");
                return false;
            }

            int w = _gridRows[0].Length;
            int spawnCount = 0;
            var invalid = new StringBuilder();
            bool ok = true;

            for (int r = 0; r < _gridRows.Length; r++)
            {
                string row = _gridRows[r];
                if (row.Length != w)
                {
                    Debug.LogError($"DungeonLevelBuilder: row {r} length {row.Length} != {w}.");
                    ok = false;
                    continue;
                }

                for (int c = 0; c < row.Length; c++)
                {
                    char ch = row[c];
                    if (ch == 'P')
                        spawnCount++;
                    else if (ch != '#' && ch != '.' && ch != 'S' && ch != 'E')
                        invalid.AppendLine($"  row {r} col {c}: '{ch}' (code {(int)ch})");
                }
            }

            if (spawnCount != 1)
            {
                Debug.LogError($"DungeonLevelBuilder: maze must contain exactly one 'P' spawn; found {spawnCount}.");
                ok = false;
            }

            if (invalid.Length > 0)
            {
                Debug.LogError("DungeonLevelBuilder: invalid maze characters:\n" + invalid);
                ok = false;
            }

            return ok;
        }

        private void EnsureMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            _matCorridor = new Material(shader) { color = new Color(0.5f, 0.46f, 0.4f) };
            _matSafe = new Material(shader) { color = new Color(0.34f, 0.46f, 0.38f) };
            _matEncounter = new Material(shader) { color = new Color(0.52f, 0.34f, 0.3f) };

            if (_wallMaterial == null)
                _matWall = new Material(shader) { color = new Color(0.26f, 0.24f, 0.21f) };
            else
                _matWall = null;
        }

        private void Build()
        {
            if (_gridRows.Length == 0 || _matCorridor == null)
                return;

            int w = _gridRows[0].Length;
            for (int r = 0; r < _gridRows.Length; r++)
            {
                if (_gridRows[r].Length != w)
                    return;
            }

            Transform root = transform;
            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root, false);
            Transform floorsRoot = new GameObject("Floors").transform;
            floorsRoot.SetParent(root, false);
            Transform ceilingsRoot = new GameObject("Ceilings").transform;
            ceilingsRoot.SetParent(root, false);
            Transform encounterRoot = new GameObject("EncounterVolumes").transform;
            encounterRoot.SetParent(root, false);
            Transform flavorRoot = new GameObject("FlavorVolumes").transform;
            flavorRoot.SetParent(root, false);

            int height = _gridRows.Length;
            int width = w;

            Vector2? spawnCell = null;
            _walkableCells.Clear();
            _spawnCell = new Vector2Int(-1, -1);

            for (int z = 0; z < height; z++)
            {
                string row = _gridRows[z];
                for (int x = 0; x < width; x++)
                {
                    char c = row[x];
                    Vector3 cellCenter = CellCenterWorld(x, z);

                    if (c == '#')
                    {
                        CreateWall(wallsRoot, cellCenter);
                    }
                    else if (c == 'P')
                    {
                        spawnCell = new Vector2(x, z);
                        _spawnCell = new Vector2Int(x, z);
                        CreateFloor(floorsRoot, cellCenter, FloorTint.Corridor);
                        if (_buildCeiling)
                            CreateCeiling(ceilingsRoot, cellCenter);
                    }
                    else if (c == '.')
                    {
                        _walkableCells.Add(new Vector2Int(x, z));
                        CreateFloor(floorsRoot, cellCenter, FloorTint.Corridor);
                        if (_buildCeiling)
                            CreateCeiling(ceilingsRoot, cellCenter);
                    }
                    else if (c == 'S')
                    {
                        _walkableCells.Add(new Vector2Int(x, z));
                        CreateFloor(floorsRoot, cellCenter, FloorTint.Safe);
                        if (_buildCeiling)
                            CreateCeiling(ceilingsRoot, cellCenter);
                        CreateFlavorVolume(flavorRoot, cellCenter, DungeonFlavorKind.Safe);
                    }
                    else if (c == 'E')
                    {
                        _walkableCells.Add(new Vector2Int(x, z));
                        CreateFloor(floorsRoot, cellCenter, FloorTint.Encounter);
                        if (_buildCeiling)
                            CreateCeiling(ceilingsRoot, cellCenter);
                        CreateEncounterVolume(encounterRoot, cellCenter);
                        CreateFlavorVolume(flavorRoot, cellCenter, DungeonFlavorKind.Encounter);
                    }
                }
            }

            if (spawnCell.HasValue && _player != null)
            {
                Vector3 p = CellCenterWorld(Mathf.RoundToInt(spawnCell.Value.x), Mathf.RoundToInt(spawnCell.Value.y));
                LastPlayerSpawnWorld = new Vector3(p.x, 0f, p.z);
                HasRecordedPlayerSpawn = true;
                _player.SetPositionAndRotation(new Vector3(p.x, 0f, p.z), Quaternion.identity);
            }

            if (_setupLighting)
                SetupDungeonLighting();
        }

        public Vector3 CellCenterWorld(int x, int z)
        {
            float cx = _gridOrigin.x + (x + 0.5f) * _cellSize;
            float cz = _gridOrigin.z + (z + 0.5f) * _cellSize;
            return new Vector3(cx, 0f, cz);
        }

        public Vector3 CellCenterWorld(Vector2Int cell) => CellCenterWorld(cell.x, cell.y);

        /// <summary>
        /// Picks a random walkable cell at least <paramref name="minChebyshevCellsFromSpawn"/> away from <c>P</c>,
        /// excluding cells already present in <paramref name="reserved"/>.
        /// </summary>
        public bool TryPickScatterCell(
            System.Random rng,
            int minChebyshevCellsFromSpawn,
            HashSet<Vector2Int> reserved,
            System.Predicate<Vector2Int> extraFilter,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (_walkableCells.Count == 0 || rng == null)
                return false;

            int attempts = _walkableCells.Count * 4;
            for (int i = 0; i < attempts; i++)
            {
                Vector2Int cell = _walkableCells[rng.Next(_walkableCells.Count)];
                if (reserved != null && reserved.Contains(cell))
                    continue;
                if (_spawnCell.x >= 0 && ChebyshevDistance(cell, _spawnCell) < minChebyshevCellsFromSpawn)
                    continue;
                if (extraFilter != null && !extraFilter(cell))
                    continue;

                worldPosition = CellCenterWorld(cell);
                reserved?.Add(cell);
                return true;
            }

            return false;
        }

        public bool IsCorridorCell(Vector2Int cell) => GetCellSymbol(cell) == '.';

        public bool IsEncounterCell(Vector2Int cell) => GetCellSymbol(cell) == 'E';

        public bool IsSafeCell(Vector2Int cell) => GetCellSymbol(cell) == 'S';

        public char GetCellSymbol(Vector2Int cell)
        {
            if (cell.y < 0 || cell.y >= _gridRows.Length)
                return '#';
            string row = _gridRows[cell.y];
            if (cell.x < 0 || cell.x >= row.Length)
                return '#';
            return row[cell.x];
        }

        /// <summary>
        /// World position for the quest NPC: center of the nearest <c>S</c> safe cell to spawn, else open floor near spawn.
        /// </summary>
        public bool TryGetNpcHubPosition(out Vector3 worldPosition, int maxChebyshevFromSpawn = 14)
        {
            worldPosition = default;
            if (_walkableCells.Count == 0 || _spawnCell.x < 0)
                return false;

            Vector2Int? bestSafe = null;
            int bestSafeDist = int.MaxValue;
            Vector2Int? bestOpen = null;
            int bestOpenDist = int.MaxValue;

            for (int i = 0; i < _walkableCells.Count; i++)
            {
                Vector2Int cell = _walkableCells[i];
                int dist = ChebyshevDistance(cell, _spawnCell);
                if (dist > maxChebyshevFromSpawn)
                    continue;

                char symbol = GetCellSymbol(cell);
                if (symbol == 'S' && dist < bestSafeDist)
                {
                    bestSafeDist = dist;
                    bestSafe = cell;
                }
                else if (symbol == '.' && dist < bestOpenDist)
                {
                    bestOpenDist = dist;
                    bestOpen = cell;
                }
            }

            Vector2Int? chosen = bestSafe ?? bestOpen;
            if (!chosen.HasValue)
                return false;

            worldPosition = CellCenterWorld(chosen.Value);
            return true;
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private void CreateWall(Transform parent, Vector3 cellCenter)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.SetParent(parent, false);
            go.transform.position = cellCenter + new Vector3(0f, _wallHeight * 0.5f, 0f);
            go.transform.localScale = new Vector3(_cellSize, _wallHeight, _cellSize);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            float brick = Mathf.Max(0.08f, _brickWorldMeters);
            if (_wallMaterial != null)
            {
                mr.sharedMaterial = _wallMaterial;
                float tx = Mathf.Max(0.25f, _cellSize / brick);
                float ty = Mathf.Max(0.25f, _wallHeight / brick);
                _wallPropBlock.Clear();
                _wallPropBlock.SetVector(BaseMapStId, new Vector4(tx, ty, 0f, 0f));
                mr.SetPropertyBlock(_wallPropBlock);
            }
            else
            {
                mr.sharedMaterial = _matWall;
            }
        }

        private enum FloorTint
        {
            Corridor,
            Safe,
            Encounter
        }

        private void CreateCeiling(Transform parent, Vector3 cellCenter)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ceiling";
            go.transform.SetParent(parent, false);
            float half = _ceilingThickness * 0.5f;
            float y = _wallHeight - half;
            go.transform.position = cellCenter + new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(_cellSize, _ceilingThickness, _cellSize);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            Material mat = _ceilingMaterial != null ? _ceilingMaterial : _floorMaterial;
            if (mat != null)
            {
                mr.sharedMaterial = mat;
                float tile = Mathf.Max(0.2f, _floorTileWorldMeters);
                float t = Mathf.Max(0.25f, _cellSize / tile);
                _ceilingPropBlock.Clear();
                _ceilingPropBlock.SetVector(BaseMapStId, new Vector4(t, t, 0f, 0f));
                _ceilingPropBlock.SetColor(BaseColorId, new Color(0.68f, 0.66f, 0.62f, 1f));
                mr.SetPropertyBlock(_ceilingPropBlock);
            }
            else
            {
                mr.sharedMaterial = _matCorridor;
            }
        }

        private void CreateFloor(Transform parent, Vector3 cellCenter, FloorTint tint)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Floor";
            go.transform.SetParent(parent, false);
            float half = _floorThickness * 0.5f;
            go.transform.position = cellCenter + new Vector3(0f, -half, 0f);
            go.transform.localScale = new Vector3(_cellSize, _floorThickness, _cellSize);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (_floorMaterial != null)
            {
                mr.sharedMaterial = _floorMaterial;
                float tile = Mathf.Max(0.2f, _floorTileWorldMeters);
                float t = Mathf.Max(0.25f, _cellSize / tile);
                _floorPropBlock.Clear();
                _floorPropBlock.SetVector(BaseMapStId, new Vector4(t, t, 0f, 0f));
                _floorPropBlock.SetColor(BaseColorId, tint switch
                {
                    FloorTint.Safe => new Color(0.9f, 0.98f, 0.92f),
                    FloorTint.Encounter => new Color(1f, 0.9f, 0.88f),
                    _ => Color.white
                });
                mr.SetPropertyBlock(_floorPropBlock);
            }
            else
            {
                mr.sharedMaterial = tint switch
                {
                    FloorTint.Safe => _matSafe,
                    FloorTint.Encounter => _matEncounter,
                    _ => _matCorridor
                };
            }
        }

        private void CreateEncounterVolume(Transform parent, Vector3 cellCenter)
        {
            GameObject go = new GameObject("EncounterCell");
            go.transform.SetParent(parent, false);
            go.transform.position = cellCenter + new Vector3(0f, _wallHeight * 0.45f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(_cellSize * 0.92f, _wallHeight * 0.85f, _cellSize * 0.92f);
            var encounter = go.AddComponent<DungeonEncounterVolume>();
            encounter.Configure(_encounterEnterQuestEventId);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void SetupDungeonLighting()
        {
            var lightsRoot = new GameObject("DungeonLights").transform;
            lightsRoot.SetParent(transform, false);

            Light sun = FindDirectionalLight();
            if (sun != null)
            {
                sun.intensity = _directionalIntensity;
                sun.color = new Color(1f, 0.94f, 0.82f);
                sun.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientFill;

            var torchCells = new List<Vector2Int>();
            int spacing = Mathf.Max(1, _torchSpacing);

            if (_spawnCell.x >= 0)
                TryAddTorchCell(torchCells, _spawnCell, spacing, force: true);

            for (int i = 0; i < _walkableCells.Count; i++)
            {
                Vector2Int cell = _walkableCells[i];
                if (GetCellSymbol(cell) == 'S')
                    TryAddTorchCell(torchCells, cell, spacing, force: true);
            }

            for (int i = 0; i < _walkableCells.Count && torchCells.Count < _maxTorches; i++)
            {
                Vector2Int cell = _walkableCells[i];
                if ((cell.x + cell.y) % 2 != 0)
                    continue;
                TryAddTorchCell(torchCells, cell, spacing, force: false);
            }

            for (int i = 0; i < torchCells.Count; i++)
            {
                Vector3 pos = CellCenterWorld(torchCells[i]);
                pos.y = _wallHeight * _torchHeightFactor;
                CreateTorchLight(lightsRoot, pos);
            }
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            }

            return null;
        }

        private void TryAddTorchCell(List<Vector2Int> torchCells, Vector2Int cell, int spacing, bool force)
        {
            if (!force)
            {
                if (torchCells.Count >= _maxTorches)
                    return;

                for (int i = 0; i < torchCells.Count; i++)
                {
                    if (ChebyshevDistance(torchCells[i], cell) < spacing)
                        return;
                }
            }

            torchCells.Add(cell);
        }

        private void CreateTorchLight(Transform parent, Vector3 position)
        {
            var root = new GameObject("Torch");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.45f);
            light.intensity = _torchIntensity;
            light.range = _torchRange;
            light.shadows = LightShadows.None;

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.SetParent(root.transform, false);
            bulb.transform.localPosition = Vector3.zero;
            bulb.transform.localScale = Vector3.one * 0.28f;
            Object.Destroy(bulb.GetComponent<Collider>());

            var rend = bulb.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.45f, 1f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.2f, 0.7f, 0.35f));
                rend.sharedMaterial = mat;
            }
        }

        private void CreateFlavorVolume(Transform parent, Vector3 cellCenter, DungeonFlavorKind kind)
        {
            GameObject go = new GameObject(kind == DungeonFlavorKind.Safe ? "FlavorSafe" : "FlavorEncounter");
            go.transform.SetParent(parent, false);
            go.transform.position = cellCenter + new Vector3(0f, _wallHeight * 0.28f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(_cellSize * 0.94f, _wallHeight * 0.5f, _cellSize * 0.94f);
            var flavor = go.AddComponent<DungeonFlavorZone>();
            flavor.Configure(kind);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_player == null)
            {
                GameObject found = GameObject.Find("Player");
                if (found != null)
                    _player = found.transform;
            }
        }
#endif
    }
}
