using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Gameplay;
using Loom.ZombieBattleground.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Loom.ZombieBattleground.OverlordManager;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class LevelUpPopup : IUIPopup
    {
        private readonly WaitForSeconds _experienceFillWait = new WaitForSeconds(1);

        private const string _hideParameterName = "Hide";

        private ILoadObjectsManager _loadObjectsManager;

        private IUIManager _uiManager;

        private Button _buttonOk;

        private TextMeshProUGUI _currentLevel;

        private TextMeshProUGUI _skillName;

        private TextMeshProUGUI _skillDescription;

        private TextMeshProUGUI _message;

        private HorizontalLayoutGroup _abilitiesGroup;

        private GameObject _rewardSkillObject;

        private GameObject _rewardDisabledObject;

        private List<AbilityViewItem> _abilities;

        private AbilityViewItem _newOpenAbility;

        private Animator _backgroundAnimator, _containerAnimator;

        private LevelReward _levelReward;

        private Hero _selectedHero;

        public GameObject Self { get; private set; }

        private int _abilityListSize = 5;

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();

            _abilities = new List<AbilityViewItem>();
        }

        public void Dispose()
        {
            foreach (AbilityViewItem abilityViewItem in _abilities)
            {
                abilityViewItem.Dispose();
            }

            _abilities.Clear();
        }

        public void Hide()
        {
            if (Self == null)
                return;

            Self.SetActive(false);
            Object.Destroy(Self);
            Self = null;
        }

        public void SetMainPriority()
        {
        }

        public void Show()
        {
            if (Self != null)
                return;

            Self = Object.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/LevelUpPopup"));
            Self.transform.SetParent(_uiManager.Canvas3.transform, false);

            _buttonOk = Self.transform.Find("Pivot/levelup_panel/UI/Button_Continue").GetComponent<Button>();
            _buttonOk.onClick.AddListener(OnClickOkButtonEventHandler);

            _rewardSkillObject = Self.transform.Find("Pivot/levelup_panel/UI/RewardSkill_Panel").gameObject;

            _rewardDisabledObject = Self.transform.Find("Pivot/levelup_panel/UI/RewardDisabled_Panel").gameObject;

            _abilitiesGroup = _rewardSkillObject.transform.Find("Abilities").GetComponent<HorizontalLayoutGroup>();

            _message = _rewardDisabledObject.transform.Find("Message").GetComponent<TextMeshProUGUI>();

            _currentLevel = Self.transform.Find("Pivot/levelup_panel/UI/Text_Level")
                .GetComponent<TextMeshProUGUI>();

            _skillName = _rewardSkillObject.transform.Find("SkillName")
                .GetComponent<TextMeshProUGUI>();

            _skillDescription = _rewardSkillObject.transform.Find("SkillDescription")
                .GetComponent<TextMeshProUGUI>();

            _backgroundAnimator = Self.transform.Find("Background").GetComponent<Animator>();
            _containerAnimator = Self.transform.Find("Pivot").GetComponent<Animator>();

            _backgroundAnimator.GetComponent<AnimationEventTriggering>().AnimationEventTriggered += AnimationEventTriggeredHandler;        

            Self.SetActive(true);

            int playerDeckId = GameClient.Get<IGameplayManager>().PlayerDeckId;
            IDataManager dataManager = GameClient.Get<IDataManager>();

            int heroId = dataManager.CachedDecksData.Decks.First(d => d.Id == playerDeckId).HeroId;

            _selectedHero = dataManager.CachedHeroesData.Heroes[heroId];

            _currentLevel.text = _selectedHero.Level.ToString();

            _newOpenAbility = null;

            FillInfo();
        }

        public void Show(object data)
        {
            Show();
        }

        private void FillInfo()
        {
            _levelReward = GameClient.Get<IOverlordManager>().GetLevelReward(_selectedHero);

            _rewardDisabledObject.SetActive(false);
            _rewardSkillObject.SetActive(true);

            if (_levelReward != null)
            {
                switch (_levelReward.Reward)
                {
                    case LevelReward.OverlordSkillRewardItem skillReward:
                        {
                            _rewardDisabledObject.SetActive(false);
                            _rewardSkillObject.SetActive(true);

                            FillRewardSkillInfo(skillReward.SkillIndex);

                            AbilityInstanceOnSelectionChanged(_newOpenAbility);
                        }
                        break;
                    case LevelReward.UnitRewardItem unitReward:
                    case LevelReward.ItemReward itemReward:
                    default:
                        {
                            _rewardDisabledObject.SetActive(true);
                            _rewardSkillObject.SetActive(false);
                            _message.text = "Rewards have been disabled for ver " + BuildMetaInfo.Instance.DisplayVersionName;
                        }
                        break;
                }
            }
        }

        private void FillRewardSkillInfo(int skillIndex)
        {
            _abilities.Clear();

            AbilityViewItem abilityInstance = null;
            bool isDefault = false;
            for (int i = 0; i < _abilityListSize; i++)
            {
                abilityInstance = new AbilityViewItem(_abilitiesGroup.transform);

                if (i < _selectedHero.Skills.Count && _selectedHero.Skills[i].Unlocked)
                {
                    abilityInstance.Skill = _selectedHero.Skills[i];
                }
                isDefault = skillIndex == i;
                abilityInstance.UpdateUIState(isDefault);
                _abilities.Add(abilityInstance);
            }

            _newOpenAbility = _abilities[skillIndex];
        }

        public void Update()
        {
        }

        private void AbilityInstanceOnSelectionChanged(AbilityViewItem ability)
        {
            _skillName.text = ability.Skill.Title;
            _skillDescription.text = ability.Skill.Description;
        }

        private void AnimationEventTriggeredHandler(string animationName)
        {

            switch (animationName)
            {
                case "SetGlow":
                    _newOpenAbility?.UpdateUIState();
                    break;
                case "SetSkill":
                    _newOpenAbility?.UpdateUIState();
                    break;
                case "HideEnd":
                    _uiManager.HidePopup<LevelUpPopup>();
                    break;
                default:
                    break;
            }
        }

        private void OnClickOkButtonEventHandler()
        {
            GameClient.Get<ISoundManager>().PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);

            _backgroundAnimator.SetTrigger(_hideParameterName);
            _containerAnimator.SetTrigger(_hideParameterName);
        }

        private class AbilityViewItem
        {
            public readonly GameObject SelfObject;

            private readonly ILoadObjectsManager _loadObjectsManager;

            private readonly GameObject _glowObj;

            private readonly Image _abilityIconImage;

            private readonly Transform _parentGameObject;

            private HeroSkill _skill;

            private bool _isSelected;

            public AbilityViewItem(Transform root)
            {
                _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();

                _parentGameObject = root;
                SelfObject =
                    Object.Instantiate(
                        _loadObjectsManager.GetObjectByPath<GameObject>(
                            "Prefabs/UI/Elements/LevelUpPopupAbilityItem"), root, false);

                _glowObj = SelfObject.transform.Find("Glow").gameObject;
                _abilityIconImage = SelfObject.transform.Find("AbilityIcon").GetComponent<Image>();
            }


            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    _glowObj.SetActive(value);
                }
            }

            public HeroSkill Skill
            {
                get => _skill;
                set
                {
                    if (_skill == value)
                        return;

                    _skill = value;
                }
            }

            public void Dispose()
            {
                Object.Destroy(SelfObject);
            }


            public void UpdateUIState(bool isDefault = false)
            {
                _glowObj.SetActive(_isSelected);

                if (Skill != null && !isDefault)
                {
                    _abilityIconImage.sprite =
                        _loadObjectsManager.GetObjectByPath<Sprite>("Images/OverlordAbilitiesIcons/" + Skill.IconPath);
                }
                else
                {
                    _abilityIconImage.sprite =
                        _loadObjectsManager.GetObjectByPath<Sprite>("Images/OverlordAbilitiesIcons/overlordability_locked");
                }
            }
        }
    }
}
