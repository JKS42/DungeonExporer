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
    [DefaultExecutionOrder(-35)]
    public sealed class NpcInteractable : MonoBehaviour
    {
        [SerializeField] private string _displayName = "Cap";
        [Tooltip("Stable id for conversation memory (per NPC).")]
        [SerializeField] private string _npcConversationId = "cap";
        [SerializeField] private string _questId = "cap_training";
        [Tooltip("Offered only when completed prerequisite (see quest definition) and this id is still available.")]
        [SerializeField] private string _followUpQuestId = "echoes_in_the_dark";
        [SerializeField] private float _interactRadius = 2.8f;

        private DialoguePanelController _dialogue;
        private InputActionAsset _inputActions;
        private InputAction _interactAction;
        private Transform _player;
        private bool _wasInPrefetchRange;

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
            float sq = dx * dx + dz * dz;
            float radiusSq = _interactRadius * _interactRadius;
            bool inRange = sq <= radiusSq;

            if (inRange && !_wasInPrefetchRange)
                _dialogue.PrefetchNpcLine(_displayName, ResolveQuestId(), _npcConversationId);
            _wasInPrefetchRange = inRange;

            if (!inRange)
                return;

            if (!_interactAction.WasPerformedThisFrame())
                return;

            _dialogue.BeginSession(_displayName, ResolveQuestId(), _npcConversationId);
        }

        private void LateUpdate()
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
            float sq = dx * dx + dz * dz;
            if (sq > _interactRadius * _interactRadius)
                return;

            string key = ResolveInteractBindingLabel();
            NpcPromptRegistry.OfferCandidate(sq, $"{key} — {_displayName}");
        }

        private string ResolveInteractBindingLabel()
        {
            if (_inputActions == null)
                return "E";

            InputActionMap map = _inputActions.FindActionMap("Player", false);
            InputAction act = map?.FindAction("Interact", false);
            if (act == null)
                return "E";

            string s = act.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            return string.IsNullOrWhiteSpace(s) ? "E" : s.Trim();
        }
    }
}
