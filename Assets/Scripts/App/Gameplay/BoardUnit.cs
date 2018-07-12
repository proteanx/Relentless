// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using System;

using UnityEngine;
using UnityEngine.Rendering;

using DG.Tweening;
using TMPro;
using LoomNetwork.CZB.Common;
using System.Collections.Generic;
using LoomNetwork.CZB.Helpers;
using LoomNetwork.Internal;
using LoomNetwork.CZB.Data;

namespace LoomNetwork.CZB
{
    public class BoardUnit
    {
        public event Action CreatureOnDieEvent;
        public event Action<object> CreatureOnAttackEvent;

        public event Action<int, int> CreatureHPChangedEvent;
        public event Action<int, int> CreatureDamageChangedEvent;

        private Action<int, int> damageChangedDelegate;
        private Action<int, int> healthChangedDelegate;

        private ILoadObjectsManager _loadObjectsManager;
        private IGameplayManager _gameplayManager;
        private ISoundManager _soundManager;
        private ITimerManager _timerManager;
        private ITutorialManager _tutorialManager;
        private PlayerController _playerController;
        private BattlegroundController _battlegroundController;
        private AnimationsController _animationsController;
        private BattleController _battleController;
        private ActionsQueueController _actionsQueueController;
        private VFXController _vfxController;
        private RanksController _ranksController;

        private GameObject _fightTargetingArrowPrefab;

        private GameObject _selfObject;

        private SpriteRenderer _pictureSprite;
        private SpriteRenderer _frozenSprite;
        private SpriteRenderer _glowSprite;
        private SpriteRenderer _frameSprite;
        private SpriteRenderer _animationSprite;
        private GameObject _shieldSprite;

        private TextMeshPro _attackText;
        private TextMeshPro _healthText;

        private ParticleSystem _sleepingParticles;

        private GameObject _feralFrame,
                           _heavyFrame;

        private int _damage;
        private int _health;

        private int _stunTurns = 0;

        private bool _readyForBuffs = false;

        private BoardArrow abilitiesTargetingArrow;
        private BattleBoardArrow fightTargetingArrow;

        private AnimationEventTriggering arrivalAnimationEventHandler;

        private OnBehaviourHandler _onBehaviourHandler;

        private GameObject unitContentObject;

        private Animator unitAnimator;

        private List<Enumerators.BuffType> _buffsOnUnit;

        private bool _dead = false;

        private bool _attacked = false;

        public bool hasFeral;
        public bool hasHeavy;
        public int numTurnsOnBoard;

        public int initialDamage;
        public int initialHP;

        public Player ownerPlayer;

        public List<UnitAnimatorInfo> animatorControllers;

        public List<object> attackedBoardObjectsThisTurn;

        public Enumerators.AttackInfoType attackInfoType = Enumerators.AttackInfoType.ANY;


        public int Damage
        {
            get
            {
                return _damage;
            }
            set
            {
                var oldDamage = _damage;
                _damage = value;


                if (oldDamage != _damage)
                    CreatureDamageChangedEvent?.Invoke(oldDamage, _damage);
            }
        }

        public int HP
        {
            get
            {
                return _health;
            }
            set
            {
                var oldHealth = _health;
                _health = value;

                _health = Mathf.Clamp(_health, 0, 99);

                if (oldHealth != _health)
                    CreatureHPChangedEvent?.Invoke(oldHealth, _health);
            }
        }

        public Transform transform { get { return _selfObject.transform; } }
        public GameObject gameObject { get { return _selfObject; } }
        public Sprite sprite { get { return _pictureSprite.sprite; } }

        public bool IsPlayable { get; set; }

        public WorkingCard Card { get; private set; }

        public int InstanceId { get; private set; }

        public bool IsStun
        {
            get { return (_stunTurns > 0 ? true : false); }
        }

        public List<Enumerators.BuffType> BuffsOnUnit { get { return _buffsOnUnit; } }

        public bool HasBuffRush { get; set; }
        public bool HasBuffHeavy { get; set; }
        public bool HasBuffShield { get; set; }
        public bool TakeFreezeToAttacked { get; set; }
        public int AdditionalDamage { get; set; }
        public int AdditionalAttack { get; set; }
        public int AdditionalDefense { get; set; }


        public BoardUnit(Transform parent)
        {
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _tutorialManager = GameClient.Get<ITutorialManager>();
            _timerManager = GameClient.Get<ITimerManager>();

            _battlegroundController = _gameplayManager.GetController<BattlegroundController>();
            _playerController = _gameplayManager.GetController<PlayerController>();
            _animationsController = _gameplayManager.GetController<AnimationsController>();
            _battleController = _gameplayManager.GetController<BattleController>();
            _actionsQueueController = _gameplayManager.GetController<ActionsQueueController>();
            _vfxController = _gameplayManager.GetController<VFXController>();
            _ranksController = _gameplayManager.GetController<RanksController>();

            _selfObject = MonoBehaviour.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/Gameplay/BoardCreature"));
            _selfObject.transform.SetParent(parent, false);

            _fightTargetingArrowPrefab = _loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/Gameplay/FightTargetingArrow");

            _pictureSprite = _selfObject.transform.Find("GraphicsAnimation/PictureRoot/CreaturePicture").GetComponent<SpriteRenderer>();
            _frozenSprite = _selfObject.transform.Find("Other/Frozen").GetComponent<SpriteRenderer>();
            _glowSprite = _selfObject.transform.Find("Other/Glow").GetComponent<SpriteRenderer>();
            _frameSprite = _selfObject.transform.Find("GraphicsAnimation").GetComponent<SpriteRenderer>();
            _animationSprite = _selfObject.transform.Find("GraphicsAnimation").GetComponent<SpriteRenderer>();
            _shieldSprite = _selfObject.transform.Find("Other/Shield").gameObject;

            _feralFrame = _selfObject.transform.Find("Other/object_feral_frame").gameObject;
            _heavyFrame = _selfObject.transform.Find("Other/object_heavy_frame").gameObject;

            _attackText = _selfObject.transform.Find("Other/AttackAndDefence/AttackText").GetComponent<TextMeshPro>();
            _healthText = _selfObject.transform.Find("Other/AttackAndDefence/DefenceText").GetComponent<TextMeshPro>();

            _sleepingParticles = _selfObject.transform.Find("Other/SleepingParticles").GetComponent<ParticleSystem>();

            unitAnimator = _selfObject.transform.Find("GraphicsAnimation").GetComponent<Animator>();

            unitContentObject = _selfObject.transform.Find("Other").gameObject;

            arrivalAnimationEventHandler = _selfObject.transform.Find("GraphicsAnimation").GetComponent<AnimationEventTriggering>();

            _onBehaviourHandler = _selfObject.GetComponent<OnBehaviourHandler>();

            arrivalAnimationEventHandler.OnAnimationEvent += ArrivalAnimationEventHandler;

            _onBehaviourHandler.OnMouseUpEvent += OnMouseUp;
            _onBehaviourHandler.OnMouseDownEvent += OnMouseDown;
            _onBehaviourHandler.OnTriggerEnter2DEvent += OnTriggerEnter2D;
            _onBehaviourHandler.OnTriggerExit2DEvent += OnTriggerExit2D;

            animatorControllers = new List<UnitAnimatorInfo>();
            for (int i = 0; i < Enum.GetNames(typeof(Enumerators.CardType)).Length; i++)
            {
                animatorControllers.Add(new UnitAnimatorInfo()
                {
                    animator = _loadObjectsManager.GetObjectByPath<RuntimeAnimatorController>("Animators/" + ((Enumerators.CardType)i).ToString() + "ArrivalController"),
                    cardType = (Enumerators.CardType)i
                });
            }

            _buffsOnUnit = new List<Enumerators.BuffType>();
            attackedBoardObjectsThisTurn = new List<object>();
        }

        public bool IsHeavyUnit()
        {
            return HasBuffHeavy || hasHeavy;
        }

        public bool IsFeralUnit()
        {
            return HasBuffRush || hasFeral;
        }


        public void Reset()
        {
          
        }

        public void Die(bool returnToHand = false)
        {
            CreatureHPChangedEvent -= healthChangedDelegate;
            CreatureDamageChangedEvent -= damageChangedDelegate;

            _dead = true;

            if (!returnToHand)
                _battlegroundController.KillBoardCard(this);

            CreatureOnDieEvent?.Invoke();
        }

        public void BuffUnit(Enumerators.BuffType type)
        {
            if (!_readyForBuffs)
                return;

            _buffsOnUnit.Add(type);
        }

        public void RemoveBuff(Enumerators.BuffType type)
        {
            if (!_readyForBuffs)
                return;

            _buffsOnUnit.Remove(type);
        }

        public void ClearBuffs()
        {
            if (!_readyForBuffs)
                return;

            int damageToDelete = 0;
            int attackToDelete = 0;
            int defenseToDelete = 0;

            foreach (var buff in _buffsOnUnit)
            {
                switch (buff)
                {
                    case Enumerators.BuffType.ATTACK:
                        attackToDelete++;
                        break;
                    case Enumerators.BuffType.DAMAGE:
                        damageToDelete++;
                        break;
                    case Enumerators.BuffType.DEFENCE:
                        defenseToDelete++;
                        break;
                    case Enumerators.BuffType.FREEZE:
                        TakeFreezeToAttacked = false;
                        break;
                    case Enumerators.BuffType.HEAVY:
                        HasBuffHeavy = false;
                        break;
                    case Enumerators.BuffType.RUSH:
                        HasBuffRush = false;
                        IsPlayable = _attacked;
                        break;
                    case Enumerators.BuffType.SHIELD:
                        HasBuffShield = false;
                        break;
                    default: break;
                }
            }

            _buffsOnUnit.Clear();

            AdditionalDefense -= defenseToDelete;
            AdditionalAttack -= attackToDelete;
            AdditionalDamage -= damageToDelete;
            HP -= defenseToDelete;
            Damage -= attackToDelete;

            UpdateFrameByType();
        }

        public void ApplyBuffs()
        {
            if (!_readyForBuffs)
                return;

            foreach (var buff in _buffsOnUnit)
            {
                switch(buff)
                {
                    case Enumerators.BuffType.ATTACK:
                        AdditionalAttack++;
                        break;
                    case Enumerators.BuffType.DAMAGE:
                        AdditionalDamage++;
                        break;
                    case Enumerators.BuffType.DEFENCE:
                        AdditionalDefense++;
                        break;
                    case Enumerators.BuffType.FREEZE:
                        TakeFreezeToAttacked = true;
                        break;
                    case Enumerators.BuffType.HEAVY:
                        HasBuffHeavy = true;
                        break;
                    case Enumerators.BuffType.RUSH:
                        HasBuffRush = true;
                        IsPlayable = !_attacked;
                        break;
                    case Enumerators.BuffType.SHIELD:
                        HasBuffShield = true;
                        break;
                    default: break;
                }
            }

            HP += AdditionalDefense;
            Damage += AdditionalAttack;

            UpdateFrameByType();
        }

        public void UseShieldFromBuff()
        {
            HasBuffShield = false;
            _buffsOnUnit.Remove(Enumerators.BuffType.SHIELD);
            _shieldSprite.SetActive(HasBuffShield);
        }

        public void UpdateFrameByType()
        {
            _shieldSprite.SetActive(HasBuffShield);

            _heavyFrame.SetActive(false);
            _feralFrame.SetActive(false);

            unitAnimator.enabled = true;

            if (HasBuffHeavy && !hasHeavy)
            {
                unitAnimator.enabled = false;
                _frameSprite.enabled = false;
                _heavyFrame.SetActive(true);
            }
            else if (HasBuffRush && !hasFeral)
            {
                unitAnimator.enabled = false;
                _frameSprite.enabled = false;
                _feralFrame.SetActive(true);
            }
        }

        public void ArrivalAnimationEventHandler(string param)
        {
            if (param.Equals("ArrivalAnimationDone"))
            {
                unitContentObject.SetActive(true);
                if (hasFeral)
                {
                    //  frameSprite.sprite = frameSprites[1];
                    StopSleepingParticles();
                    if (ownerPlayer != null)
                        SetHighlightingEnabled(true);
                }


                InternalTools.SetLayerRecursively(_selfObject, 0, new List<string>() { _sleepingParticles.name });

                if (Card.libraryCard.cardRank == Enumerators.CardRank.COMMANDER)
                {
                    _soundManager.PlaySound(Enumerators.SoundType.CARDS, Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_PLAY + "1", Constants.ZOMBIES_SOUND_VOLUME, false, true);
                    _soundManager.PlaySound(Enumerators.SoundType.CARDS, Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_PLAY + "2", Constants.ZOMBIES_SOUND_VOLUME / 2f, false, true);
                }
                else
                {
                    _soundManager.PlaySound(Enumerators.SoundType.CARDS, Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_PLAY, Constants.ZOMBIES_SOUND_VOLUME, false, true);
                }


                if (Card.libraryCard.name.Equals("Freezzee"))
                {
                    var freezzees = GetEnemyUnitsList(this).FindAll(x => x.Card.libraryCard.id == Card.libraryCard.id);

                    if (freezzees.Count > 0)
                    {
                        foreach (var creature in freezzees)
                        {
                            creature.Stun(1);
                            CreateFrozenVFX(creature.transform.position);
                        }
                    }
                }


                _readyForBuffs = true;
                _ranksController.UpdateRanksBuffs(ownerPlayer);
            }
            else if (param.Equals("ArrivalAnimationHeavySetLayerUnderBattleFrame"))
            {
                InternalTools.SetLayerRecursively(gameObject, 0, new List<string>() { _sleepingParticles.name });

                _animationSprite.sortingOrder = -_animationSprite.sortingOrder;
                _pictureSprite.sortingOrder = -_pictureSprite.sortingOrder;
            }
        }

        private void CreateFrozenVFX(Vector3 pos)
        {
            var _frozenVFX = MonoBehaviour.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/VFX/FrozenVFX"));
            _frozenVFX.transform.position = Utilites.CastVFXPosition(pos + Vector3.forward);
            DestroyCurrentParticle(_frozenVFX);
        }

        private void DestroyCurrentParticle(GameObject currentParticle, bool isDirectly = false, float time = 5f)
        {
            if (isDirectly)
                DestroyParticle(new object[] { currentParticle });
            else
                _timerManager.AddTimer(DestroyParticle, new object[] { currentParticle }, time, false);
        }

        private void DestroyParticle(object[] param)
        {
            GameObject particleObj = param[0] as GameObject;
            MonoBehaviour.Destroy(particleObj);
        }

        private List<BoardUnit> GetEnemyUnitsList(BoardUnit unit)
        {
            if (_gameplayManager.CurrentPlayer.BoardCards.Contains(unit))
                return _gameplayManager.OpponentPlayer.BoardCards;
            return _gameplayManager.CurrentPlayer.BoardCards;
        }

        public void SetObjectInfo(WorkingCard card, string setName = "")
        {
            Card = card;

            // hack for top zombies
            if (!ownerPlayer.IsLocalPlayer)
                _sleepingParticles.transform.localPosition = new Vector3(_sleepingParticles.transform.localPosition.x, _sleepingParticles.transform.localPosition.y, 3f);

            _pictureSprite.sprite = _loadObjectsManager.GetObjectByPath<Sprite>(string.Format("Images/Cards/Illustrations/{0}_{1}_{2}", setName.ToLower(), Card.libraryCard.cardRank.ToString().ToLower(), Card.libraryCard.picture.ToLower()));

            _pictureSprite.transform.localPosition = MathLib.FloatVector3ToVector3(Card.libraryCard.cardViewInfo.position);
            _pictureSprite.transform.localScale = MathLib.FloatVector3ToVector3(Card.libraryCard.cardViewInfo.scale);

            unitAnimator.runtimeAnimatorController = animatorControllers.Find(x => x.cardType == Card.libraryCard.cardType).animator;
            if (Card.type == Enumerators.CardType.WALKER)
            {
                _sleepingParticles.transform.position += Vector3.up * 0.7f;
            }

            Damage = card.damage;
            HP = card.health;

            initialDamage = Damage;
            initialHP = HP;

            _attackText.text = Damage.ToString();
            _healthText.text = HP.ToString();

            damageChangedDelegate = (oldValue, newValue) =>
            {
                UpdateUnitInfoText(_attackText, Damage, initialDamage);
            };

            CreatureDamageChangedEvent += damageChangedDelegate;

            healthChangedDelegate = (oldValue, newValue) =>
            {
                UpdateUnitInfoText(_healthText, HP, initialHP);
                CheckOnDie();
            };

            CreatureHPChangedEvent += healthChangedDelegate;

            switch (Card.libraryCard.cardType)
            {
                case Enumerators.CardType.FERAL:
                    hasFeral = true;
                    IsPlayable = true;
                    _soundManager.PlaySound(Enumerators.SoundType.FERAL_ARRIVAL, Constants.ARRIVAL_SOUND_VOLUME, false, false, true);
                    break;
                case Enumerators.CardType.HEAVY:
                    _soundManager.PlaySound(Enumerators.SoundType.HEAVY_ARRIVAL, Constants.ARRIVAL_SOUND_VOLUME, false, false, true);
                    hasHeavy = true;
                    break;
                case Enumerators.CardType.WALKER:
                default:
                    _soundManager.PlaySound(Enumerators.SoundType.WALKER_ARRIVAL, Constants.ARRIVAL_SOUND_VOLUME, false, false, true);
                    break;
            }

            if (hasHeavy)
            {
                //   glowSprite.gameObject.SetActive(false);
                //  pictureMaskTransform.localScale = new Vector3(50, 55, 1);
                // frameSprite.sprite = frameSprites[2];
            }
            SetHighlightingEnabled(false);

            unitAnimator.StopPlayback();
            unitAnimator.Play(0);
        }

        private void CheckOnDie()
        {
            if (HP <= 0 && !_dead)
                Die();
        }

        public void PlayArrivalAnimation()
        {
            unitAnimator.SetTrigger("Active");
        }

        public void OnStartTurn()
        {
            attackedBoardObjectsThisTurn.Clear();
            numTurnsOnBoard += 1;
            StopSleepingParticles();

            if (ownerPlayer != null && IsPlayable && _gameplayManager.CurrentTurnPlayer.Equals(ownerPlayer))
            {
                SetHighlightingEnabled(true);

                _attacked = false;
            }
        }

        public void OnEndTurn()
        {
            if (_stunTurns > 0)
                _stunTurns--;
            if (_stunTurns == 0)
            {
                IsPlayable = true;
                _frozenSprite.DOFade(0, 1);
            }

            CancelTargetingArrows();
        }

        public void Stun(int turns)
        {
            Debug.Log("WAS STUNED");
            if (turns > _stunTurns)
                _stunTurns = turns;
            IsPlayable = false;

            _frozenSprite.DOFade(1, 1);
            //sleepingParticles.Play();
        }

        public void CancelTargetingArrows()
        {
            if (abilitiesTargetingArrow != null)
            {
                MonoBehaviour.Destroy(abilitiesTargetingArrow.gameObject);
            }
            if (fightTargetingArrow != null)
            {
                MonoBehaviour.Destroy(fightTargetingArrow.gameObject);
            }
        }

        private void UpdateUnitInfoText(TextMeshPro text, int stat, int initialStat)
        {
            if (text == null || !text)
                return;

            text.text = stat.ToString();

            if (stat > initialStat)
                text.color = Color.green;
            else if (stat < initialStat)
                text.color = Color.red;
            else
            {
                text.color = Color.white;
            }
            var sequence = DOTween.Sequence();
            sequence.Append(text.transform.DOScale(new Vector3(1.4f, 1.4f, 1.0f), 0.4f));
            sequence.Append(text.transform.DOScale(new Vector3(1.0f, 1.0f, 1.0f), 0.2f));
            sequence.Play();
        }

        public void SetHighlightingEnabled(bool enabled)
        {
            _glowSprite.enabled = enabled;
        }

        public void StopSleepingParticles()
        {
            if (_sleepingParticles != null)
                _sleepingParticles.Stop();
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            if (collider.transform.parent != null)
            {
                var targetingArrow = collider.transform.parent.parent.GetComponent<BoardArrow>();
                if (targetingArrow != null)
                {
                    targetingArrow.OnCardSelected(this);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collider)
        {
            if (collider.transform.parent != null)
            {
                var targetingArrow = collider.transform.parent.parent.GetComponent<BoardArrow>();
                if (targetingArrow != null)
                {
                    targetingArrow.OnCardUnselected(this);
                }
            }
        }

        private void OnMouseDown(GameObject obj)
        {
            //if (fightTargetingArrowPrefab == null)
            //    return;

            //Debug.LogError(IsPlayable + " | " + ownerPlayer.isActivePlayer + " | " + ownerPlayer);

            if (_gameplayManager.IsTutorial && _gameplayManager.TutorialStep == 18)
                return;

            if (ownerPlayer != null && ownerPlayer.IsLocalPlayer && _playerController.IsActive && IsPlayable)
            {
                fightTargetingArrow = MonoBehaviour.Instantiate(_fightTargetingArrowPrefab).GetComponent<BattleBoardArrow>();
                fightTargetingArrow.targetsType = new List<Enumerators.SkillTargetType>() { Enumerators.SkillTargetType.OPPONENT, Enumerators.SkillTargetType.OPPONENT_CARD };
                fightTargetingArrow.BoardCards = _gameplayManager.OpponentPlayer.BoardCards;
                fightTargetingArrow.owner = this;
                fightTargetingArrow.Begin(transform.position);

                if (attackInfoType == Enumerators.AttackInfoType.ONLY_DIFFERENT)
                    fightTargetingArrow.ignoreBoardObjectsList = attackedBoardObjectsThisTurn;

                if (ownerPlayer.Equals(_gameplayManager.CurrentPlayer))
                {
                    _battlegroundController.DestroyCardPreview();
                    _playerController.IsCardSelected = true;

                    if (_tutorialManager.IsTutorial)
                        _tutorialManager.DeactivateSelectTarget();
                }
            }
        }

        private void OnMouseUp(GameObject obj)
        {
            if (ownerPlayer != null && ownerPlayer.IsLocalPlayer && _playerController.IsActive && IsPlayable)
            {
                if (fightTargetingArrow != null)
                {
                    fightTargetingArrow.End(this);

                    if (ownerPlayer.Equals(_gameplayManager.CurrentPlayer))
                    {
                        _playerController.IsCardSelected = false;
                    }
                }
            }
        }

        public void ForceSetCreaturePlayable()
        {
            if (IsStun)
                return;

            SetHighlightingEnabled(true);
            IsPlayable = true;
        }

        public void DoCombat(object target)
        {
            if (target == null)
            {
                if (_tutorialManager.IsTutorial)
                    _tutorialManager.ActivateSelectTarget();
                return;
            }

            var sortingGroup = _selfObject.GetComponent<SortingGroup>();

            if (target is Player)
            {
                var targetPlayer = target as Player;
                SetHighlightingEnabled(false);
                IsPlayable = false;
                _attacked = true;

                // GameClient.Get<ISoundManager>().PlaySound(Enumerators.SoundType.CARDS, libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_ATTACK, Constants.ZOMBIES_SOUND_VOLUME, false, true);

                //sortingGroup.sortingOrder = 100;

                _soundManager.StopPlaying(Enumerators.SoundType.CARDS);
                _soundManager.PlaySound(Enumerators.SoundType.CARDS, Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_ATTACK, Constants.ZOMBIES_SOUND_VOLUME, false, true);

                _actionsQueueController.AddNewActionInToQueue((parameter, completeCallback) =>
                {
                    attackedBoardObjectsThisTurn.Add(targetPlayer);

                    _animationsController.DoFightAnimation(_selfObject, targetPlayer.AvatarObject, 0.1f, () =>
                    {

                        Vector3 positionOfVFX = targetPlayer.AvatarObject.transform.position;
                       // positionOfVFX.y = 4.45f; // was used only for local player

                        _vfxController.PlayAttackVFX(Card.libraryCard.cardType, positionOfVFX, Damage);

                        _battleController.AttackPlayerByCreature(this, targetPlayer);
                        CreatureOnAttackEvent?.Invoke(targetPlayer);
                    },
                    () =>
                    {
                        //sortingGroup.sortingOrder = 0;
                        fightTargetingArrow = null;

                        completeCallback?.Invoke();
                    });
                });
            }
            else if (target is BoardUnit)
            {
                var targetCard = target as BoardUnit;
                SetHighlightingEnabled(false);
                IsPlayable = false;

                _soundManager.StopPlaying(Enumerators.SoundType.CARDS);
                _soundManager.PlaySound(Enumerators.SoundType.CARDS, Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_ATTACK, Constants.ZOMBIES_SOUND_VOLUME, false, true);

                _actionsQueueController.AddNewActionInToQueue((parameter, completeCallback) =>
                {
                    attackedBoardObjectsThisTurn.Add(targetCard);

                    //sortingGroup.sortingOrder = 100;

                    // play sound when target creature attack more than our
                    if (targetCard.Damage > Damage)
                        _soundManager.PlaySound(Enumerators.SoundType.CARDS, targetCard.Card.libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_ATTACK, Constants.ZOMBIES_SOUND_VOLUME, false, true);

                    _animationsController.DoFightAnimation(_selfObject, targetCard.transform.gameObject, 0.5f, () =>
                    {
                        _vfxController.PlayAttackVFX(Card.libraryCard.cardType, targetCard.transform.position, Damage);

                        _battleController.AttackCreatureByCreature(this, targetCard);

                        if(TakeFreezeToAttacked)
                            targetCard.Stun(1);

                        targetCard.HP -= AdditionalDamage;

                        CreatureOnAttackEvent?.Invoke(targetCard);
                    },
                    () =>
                    {
                        //sortingGroup.sortingOrder = 0;
                        fightTargetingArrow = null;

                        completeCallback?.Invoke();
                    });
                });
            }
        }
    }

    [Serializable]
    public class UnitAnimatorInfo
    {
        public Enumerators.CardType cardType;
        public RuntimeAnimatorController animator;
    }
}