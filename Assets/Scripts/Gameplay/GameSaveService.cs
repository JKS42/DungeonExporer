using System;
using System.IO;
using DungeonExporer.Player;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Lightweight session save: player pose, quests, inventory. F5 save / F9 load to the project user save path.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class GameSaveService : MonoBehaviour
    {
        [SerializeField] private Transform _player;

        private string SavePath => Path.Combine(Application.persistentDataPath, "dungeon_session_save.json");

        private void Awake()
        {
            if (_player == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p != null)
                    _player = p.transform;
            }
        }

        private void Start()
        {
            TryLoadOnStart();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;
            if (PauseMenuController.IsPaused)
                return;

            if (Keyboard.current.f5Key.wasPressedThisFrame)
                SaveGame();
            if (Keyboard.current.f9Key.wasPressedThisFrame)
                LoadGame();
        }

        private void TryLoadOnStart()
        {
            if (!File.Exists(SavePath))
                return;
            LoadGameInternal(showLog: false);
        }

        private void SaveGame()
        {
            try
            {
                var data = new GameSaveData { version = 1 };
                if (_player != null)
                {
                    Vector3 p = _player.position;
                    data.px = p.x;
                    data.py = p.y;
                    data.pz = p.z;
                }

                if (QuestManager.Instance != null)
                    QuestManager.Instance.ExportToSave(data);
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.ExportToSave(data);

                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log($"GameSaveService: saved to {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameSaveService: save failed: {e.Message}");
            }
        }

        private void LoadGame()
        {
            LoadGameInternal(showLog: true);
        }

        private void LoadGameInternal(bool showLog)
        {
            if (!File.Exists(SavePath))
            {
                if (showLog)
                    Debug.LogWarning("GameSaveService: no save file yet (F5 to save).");
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || data.version < 1)
                {
                    if (showLog)
                        Debug.LogWarning("GameSaveService: save file unreadable.");
                    return;
                }

                QuestManager.Instance?.ApplyFromSave(data);
                PlayerInventory.Instance?.ApplyFromSave(data);

                if (_player != null)
                    _player.SetPositionAndRotation(new Vector3(data.px, data.py, data.pz), _player.rotation);

                if (showLog)
                    Debug.Log($"GameSaveService: loaded from {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameSaveService: load failed: {e.Message}");
            }
        }
    }
}
