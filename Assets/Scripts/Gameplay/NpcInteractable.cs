using DungeonExporer.Player;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Stand near the NPC and use Interact to open dialogue. Chooses between a primary quest and an optional follow-up
    /// (e.g. after the first quest is completed).
    /// </summary>
    public sealed class NpcInteractable : MonoBehaviour
    {
        [SerializeField] private string _displayName = "Cap";
        [SerializeField] private string _questId = "cap_training";
        [Tooltip("Offered only when completed prerequisite (see quest definition) and this id is still available.")]
        [SerializeField] private string _followUpQuestId = "echoes_in_the_dark";
        [SerializeField] private float _interactRadius = 2.8f;

        private DialoguePanelController _dialogue;
        private InputActionAsset _inputActions;
        private InputAction _interactAction;
        private Transform _player;

        private void Start()
        {
            CacheInteractAction();
        }

        private void CacheInteractAction()
        {
            if (_inputActions == null)
                return;
            _interactAction = _inputActions.FindActionMap("Player", throwIfNotFound: true)
                .FindAction("Interact", throwIfNotFound: true);
        }

        public void Wire(DialoguePanelController dialogue, InputActionAsset inputActions, Transform player)
        {
            _dialogue = dialogue;
            _inputActions = inputActions;
            _player = player;
            CacheInteractAction();
        }

        private string ResolveQuestId()
        {
            if (QuestManager.Instance == null)
                return _questId;

            bool hasFollow = !string.IsNullOrWhiteSpace(_followUpQuestId);

            if (hasFollow && QuestManager.Instance.IsQuestActive(_followUpQuestId))
                return _followUpQuestId;
            if (QuestManager.Instance.IsQuestActive(_questId))
                return _questId;
            if (hasFollow && QuestManager.Instance.CanOfferQuest(_followUpQuestId))
                return _followUpQuestId;
            if (QuestManager.Instance.CanOfferQuest(_questId))
                return _questId;
            if (hasFollow && QuestManager.Instance.IsQuestCompleted(_followUpQuestId))
                return _followUpQuestId;
            if (QuestManager.Instance.IsQuestCompleted(_questId))
                return _questId;
            return _questId;
        }

        private void Update()
        {
            if (_player == null || _dialogue == null || _interactAction == null)
                return;
            if (DialoguePanelController.IsOpen || PauseMenuController.IsPaused)
                return;
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
                return;

            Vector3 flat = transform.position;
            Vector3 pp = _player.position;
            float dx = pp.x - flat.x;
            float dz = pp.z - flat.z;
            if (dx * dx + dz * dz > _interactRadius * _interactRadius)
                return;

            if (!_interactAction.WasPerformedThisFrame())
                return;

            _dialogue.BeginSession(_displayName, ResolveQuestId());
        }
    }
}
