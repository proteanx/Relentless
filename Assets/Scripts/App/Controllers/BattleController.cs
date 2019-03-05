using System;
using System.Collections.Generic;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;

namespace Loom.ZombieBattleground
{
    public class BattleController : IController
    {
        private IGameplayManager _gameplayManager;

        private ITutorialManager _tutorialManager;

        private ActionsQueueController _actionsQueueController;

        private AbilitiesController _abilitiesController;

        private BattlegroundController _battlegroundController;

        private VfxController _vfxController;

        private Dictionary<Enumerators.SetType, Enumerators.SetType> _strongerElemental, _weakerElemental;

        public void Dispose()
        {
        }

        public void Init()
        {
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _tutorialManager = GameClient.Get<ITutorialManager>();

            _actionsQueueController = _gameplayManager.GetController<ActionsQueueController>();
            _abilitiesController = _gameplayManager.GetController<AbilitiesController>();
            _vfxController = _gameplayManager.GetController<VfxController>();
            _battlegroundController = _gameplayManager.GetController<BattlegroundController>();

            FillStrongersAndWeakers();
        }

        public void Update()
        {
        }

        public void ResetAll()
        {
        }

        public void AttackPlayerByUnit(BoardUnitModel attackingUnitModel, Player attackedPlayer)
        {
            int damageAttacking = attackingUnitModel.CurrentDamage;

            if (attackingUnitModel != null && attackedPlayer != null)
            {
                attackedPlayer.Defense -= damageAttacking;
            }

            attackingUnitModel.InvokeUnitAttacked(attackedPlayer, damageAttacking, true);

            _vfxController.SpawnGotDamageEffect(attackedPlayer, -damageAttacking);

            _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.BattleframeAttacked, attackingUnitModel.TutorialObjectId);

            _actionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
            {
                ActionType = Enumerators.ActionType.CardAttackOverlord,
                Caller = attackingUnitModel,
                TargetEffects = new List<PastActionsPopup.TargetEffectParam>()
                {
                    new PastActionsPopup.TargetEffectParam()
                    {
                        ActionEffectType = Enumerators.ActionEffectType.ShieldDebuff,
                        Target = attackedPlayer,
                        HasValue = true,
                        Value = -damageAttacking
                    }
                }
            });

            if (attackingUnitModel.OwnerPlayer == _gameplayManager.CurrentPlayer)
            {
                _gameplayManager.PlayerMoves.AddPlayerMove(new PlayerMove(Enumerators.PlayerActionType.AttackOnOverlord,
                    new AttackOverlord(attackingUnitModel, attackedPlayer, damageAttacking)));
            }
        }

        public void AttackUnitByUnit(BoardUnitModel attackingUnitModel, BoardUnitModel attackedUnitModel, bool hasCounterAttack = true)
        {
            int damageAttacked = 0;
            int damageAttacking;

            if (attackingUnitModel != null && attackedUnitModel != null)
            {
                int additionalDamageAttacker =
                    _abilitiesController.GetStatModificatorByAbility(attackingUnitModel, attackedUnitModel, true);
                int additionalDamageAttacked =
                    _abilitiesController.GetStatModificatorByAbility(attackedUnitModel, attackingUnitModel, false);

                damageAttacking = attackingUnitModel.CurrentDamage + additionalDamageAttacker;

                if (damageAttacking > 0 && attackedUnitModel.HasBuffShield)
                {
                    damageAttacking = 0;
                    attackedUnitModel.HasUsedBuffShield = true;
                }

                attackedUnitModel.LastAttackingSetType = attackingUnitModel.Card.CardPrototype.CardSetType;//LastAttackingUnit = attackingUnit;
                attackedUnitModel.CurrentHp -= damageAttacking;

                CheckOnKillEnemyZombie(attackedUnitModel);

                if (attackedUnitModel.CurrentHp <= 0)
                {
                    attackingUnitModel.InvokeKilledUnit(attackedUnitModel);
                }

                _vfxController.SpawnGotDamageEffect(_battlegroundController.GetBoardUnitViewByModel(attackedUnitModel), -damageAttacking);

                attackedUnitModel.InvokeUnitDamaged(attackingUnitModel);
                attackingUnitModel.InvokeUnitAttacked(attackedUnitModel, damageAttacking, true);

                if (hasCounterAttack)
                {
                    if (attackedUnitModel.CurrentHp > 0 && attackingUnitModel.AttackAsFirst || !attackingUnitModel.AttackAsFirst)
                    {
                        damageAttacked = attackedUnitModel.CurrentDamage + additionalDamageAttacked;

                        if (damageAttacked > 0 && attackingUnitModel.HasBuffShield)
                        {
                            damageAttacked = 0;
                            attackingUnitModel.HasUsedBuffShield = true;
                        }

                        attackingUnitModel.LastAttackingSetType = attackedUnitModel.Card.CardPrototype.CardSetType;
                        attackingUnitModel.CurrentHp -= damageAttacked;

                        if (attackingUnitModel.CurrentHp <= 0)
                        {
                            attackedUnitModel.InvokeKilledUnit(attackingUnitModel);
                        }

                        _vfxController.SpawnGotDamageEffect(_battlegroundController.GetBoardUnitViewByModel(attackingUnitModel), -damageAttacked);

                        attackingUnitModel.InvokeUnitDamaged(attackedUnitModel);
                        attackedUnitModel.InvokeUnitAttacked(attackingUnitModel, damageAttacked, false);
                    }
                }

                _actionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
                    {
                    ActionType = Enumerators.ActionType.CardAttackCard,
                    Caller = attackingUnitModel,
                    TargetEffects = new List<PastActionsPopup.TargetEffectParam>()
                    {
                        new PastActionsPopup.TargetEffectParam()
                        {
                            ActionEffectType = Enumerators.ActionEffectType.ShieldDebuff,
                            Target = attackedUnitModel,
                            HasValue = true,
                            Value = -damageAttacking
                        }
                    }
                });

                _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.BattleframeAttacked, attackingUnitModel.TutorialObjectId);

                if (attackingUnitModel.OwnerPlayer == _gameplayManager.CurrentPlayer)
                {
                    _gameplayManager.PlayerMoves.AddPlayerMove(
                        new PlayerMove(
                            Enumerators.PlayerActionType.AttackOnUnit,
                            new AttackUnit(attackingUnitModel, attackedUnitModel, damageAttacked, damageAttacking))
                        );
                }
            }
        }

        public void AttackUnitBySkill(Player attackingPlayer, BoardSkill skill, BoardUnitModel attackedUnitModel, int modifier, int damageOverride = -1)
        {
            if (attackedUnitModel != null)
            {
                int damage = damageOverride != -1 ? damageOverride : skill.Skill.Value + modifier;

                if (damage > 0 && attackedUnitModel.HasBuffShield)
                {
                    damage = 0;
                    attackedUnitModel.UseShieldFromBuff();
                }
                attackedUnitModel.LastAttackingSetType = attackingPlayer.SelfHero.HeroElement;
                attackedUnitModel.CurrentHp -= damage;

                CheckOnKillEnemyZombie(attackedUnitModel);

                _vfxController.SpawnGotDamageEffect(_battlegroundController.GetBoardUnitViewByModel(attackedUnitModel), -damage);
            }
        }

        public void AttackPlayerBySkill(Player attackingPlayer, BoardSkill skill, Player attackedPlayer, int damageOverride = -1)
        {
            if (attackedPlayer != null)
            {
                int damage = damageOverride != -1 ? damageOverride : skill.Skill.Value;

                attackedPlayer.Defense -= damage;

                _vfxController.SpawnGotDamageEffect(attackedPlayer, -damage);
            }
        }

        public void HealPlayerBySkill(Player healingPlayer, BoardSkill skill, Player healedPlayer)
        {
            if (healingPlayer != null)
            {
                healedPlayer.Defense += skill.Skill.Value;
                if (skill.Skill.OverlordSkill != Enumerators.OverlordSkill.HARDEN &&
                    skill.Skill.OverlordSkill != Enumerators.OverlordSkill.ICE_WALL)
                {
                    if (healingPlayer.Defense > Constants.DefaultPlayerHp)
                    {
                        healingPlayer.Defense = Constants.DefaultPlayerHp;
                    }
                }
            }
        }

        public void HealUnitBySkill(Player healingPlayer, BoardSkill skill, BoardUnitModel healedCreature)
        {
            if (healedCreature != null)
            {
                healedCreature.CurrentHp += skill.Skill.Value;
                if (healedCreature.CurrentHp > healedCreature.MaxCurrentHp)
                {
                    healedCreature.CurrentHp = healedCreature.MaxCurrentHp;
                }
            }
        }

        public void AttackUnitByAbility(
            object attacker, AbilityData ability, BoardUnitModel attackedUnitModel, int damageOverride = -1)
        {
            int damage = damageOverride != -1 ? damageOverride : ability.Value;

            if (attackedUnitModel != null)
            {
                if (damage > 0 && attackedUnitModel.HasBuffShield)
                {
                    damage = 0;
                    attackedUnitModel.UseShieldFromBuff();
                }

                switch (attacker)
                {
                    case BoardUnitModel model:
                        attackedUnitModel.LastAttackingSetType = model.Card.CardPrototype.CardSetType;
                        break;
                    case BoardSpell spell:
                        attackedUnitModel.LastAttackingSetType = spell.Card.CardPrototype.CardSetType;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(attacker), attacker, null);
                }

                attackedUnitModel.CurrentHp -= damage;
                CheckOnKillEnemyZombie(attackedUnitModel);
            }
        }

        public void AttackPlayerByAbility(object attacker, AbilityData ability, Player attackedPlayer, int damageOverride = -1)
        {
            int damage = damageOverride != -1 ? damageOverride : ability.Value;

            if (attackedPlayer != null)
            {
                attackedPlayer.Defense -= damage;

                _vfxController.SpawnGotDamageEffect(attackedPlayer, -damage);
            }
        }

        public void HealPlayerByAbility(object healler, AbilityData ability, Player healedPlayer, int value = -1)
        {
            int healValue = ability.Value;

            if (value > 0)
                healValue = value;

            if (healedPlayer != null)
            {
                healedPlayer.Defense += healValue;
                if (healedPlayer.Defense > Constants.DefaultPlayerHp)
                {
                    healedPlayer.Defense = Constants.DefaultPlayerHp;
                }
            }
        }

        public void HealUnitByAbility(object healler, AbilityData ability, BoardUnitModel healedCreature, int value = -1)
        {
            int healValue = ability.Value;

            if (value > 0)
                healValue = value;

            if (healedCreature != null)
            {
                healedCreature.CurrentHp += healValue;
                if (healedCreature.CurrentHp > healedCreature.MaxCurrentHp)
                {
                    healedCreature.CurrentHp = healedCreature.MaxCurrentHp;
                }
            }
        }

        public void CheckOnKillEnemyZombie(BoardUnitModel attackedUnit)
        {
            if (!attackedUnit.OwnerPlayer.IsLocalPlayer && attackedUnit.CurrentHp == 0)
            {
                GameClient.Get<IOverlordExperienceManager>().ReportExperienceAction(_gameplayManager.CurrentPlayer.SelfHero, Common.Enumerators.ExperienceActionType.KillMinion);
            }
        }

        private void FillStrongersAndWeakers()
        {
            _strongerElemental = new Dictionary<Enumerators.SetType, Enumerators.SetType>
            {
                {
                    Enumerators.SetType.FIRE, Enumerators.SetType.TOXIC
                },
                {
                    Enumerators.SetType.TOXIC, Enumerators.SetType.LIFE
                },
                {
                    Enumerators.SetType.LIFE, Enumerators.SetType.EARTH
                },
                {
                    Enumerators.SetType.EARTH, Enumerators.SetType.AIR
                },
                {
                    Enumerators.SetType.AIR, Enumerators.SetType.WATER
                },
                {
                    Enumerators.SetType.WATER, Enumerators.SetType.FIRE
                }
            };

            _weakerElemental = new Dictionary<Enumerators.SetType, Enumerators.SetType>
            {
                {
                    Enumerators.SetType.FIRE, Enumerators.SetType.WATER
                },
                {
                    Enumerators.SetType.TOXIC, Enumerators.SetType.FIRE
                },
                {
                    Enumerators.SetType.LIFE, Enumerators.SetType.TOXIC
                },
                {
                    Enumerators.SetType.EARTH, Enumerators.SetType.LIFE
                },
                {
                    Enumerators.SetType.AIR, Enumerators.SetType.EARTH
                },
                {
                    Enumerators.SetType.WATER, Enumerators.SetType.AIR
                }
            };
        }
    }
}
