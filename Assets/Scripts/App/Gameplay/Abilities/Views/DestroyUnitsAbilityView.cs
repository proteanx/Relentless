using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Helpers;
using System.Collections.Generic;
using UnityEngine;

namespace Loom.ZombieBattleground
{
    public class DestroyUnitsAbilityView : AbilityViewBase<DestroyUnitsAbility>
    {
        private BattlegroundController _battlegroundController;

        private List<BoardUnitView> _unitsViews;

        #region Bulldozer

        private GameObject opponentLineObject;

        private GameObject playerLineObject;

        private GameObject _cardDissapearingPrefab;
        #endregion

        public DestroyUnitsAbilityView(DestroyUnitsAbility ability) : base(ability)
        {
            _battlegroundController = GameplayManager.GetController<BattlegroundController>();

            _unitsViews = new List<BoardUnitView>();
        }

        protected override void OnAbilityAction(object info = null)
        {
            _unitsViews = (List<BoardUnitView>)info;

            float delayBeforeDestroy = 3f;
            float delayAfter = 0;
            Vector3 offset = Vector3.zero;
            Enumerators.CardNameOfAbility cardNameOfAbility = Enumerators.CardNameOfAbility.None;

            //if (Ability.AbilityData.HasVisualEffectType(Enumerators.VisualEffectType.Impact))
            {
                VfxObject = LoadObjectsManager.GetObjectByPath<GameObject>("Prefabs/VFX/Bulldozer");//(Ability.AbilityData.GetVisualEffectByType(Enumerators.VisualEffectType.Impact).Path);

                AbilityEffectInfoView effectInfo = VfxObject.GetComponent<AbilityEffectInfoView>();
                if (effectInfo != null)
                {
                    delayAfter = effectInfo.delayAfterEffect;
                    delayBeforeDestroy = effectInfo.delayBeforeEffect;
                    cardNameOfAbility = effectInfo.cardNameOfAbility;
                    offset = effectInfo.offset;
                }

                Vector3 targetPosition = Vector3.zero;

                switch (cardNameOfAbility)
                {
                    case Enumerators.CardNameOfAbility.None:
                        break;
                    case Enumerators.CardNameOfAbility.Bulldozer:
                        {
                            CreateVfx(targetPosition + offset, true, delayBeforeDestroy, true);
                            opponentLineObject = VfxObject.transform.Find("RubbleUp/RubbleSeq").gameObject;
                            playerLineObject = VfxObject.transform.Find("Rubble/RubbleSeq").gameObject;

                            _cardDissapearingPrefab = VfxObject.transform.Find("CardsDissapearing/Tears").gameObject;

                            Ability.OnUpdateEvent += OnUpdateEventHandler;
                        }
                        break;
                    default:
                        break;
                }
            }

            InternalTools.DoActionDelayed(Ability.InvokeVFXAnimationEnded, delayAfter);
        }

        private void OnUpdateEventHandler()
        {
            BoardUnitView unitView;
            for (int i = 0; i < _unitsViews.Count; i++)
            {
                unitView = _unitsViews[i];
                if (unitView.Model.OwnerPlayer == Ability.PlayerCallerOfAbility)
                {
                    if (playerLineObject.transform.position.x > unitView.Transform.position.x + 1f)
                    {
                        unitView.ChangeModelVisibility(false);
                        CreateSubParticle(unitView.Transform.position);
                        _unitsViews.Remove(unitView);
                    }
                }
                else
                {
                    if (opponentLineObject.transform.position.x + 1f < unitView.Transform.position.x)
                    {
                        unitView.ChangeModelVisibility(false);
                        CreateSubParticle(unitView.Transform.position);
                        _unitsViews.Remove(unitView);
                    }
                }
            }

            if(_unitsViews.Count == 0)
            {
                Ability.OnUpdateEvent -= OnUpdateEventHandler;
            }
        }

        private void CreateSubParticle(Vector3 pos, float duration = 3)
        {
            GameObject subObject = Object.Instantiate(_cardDissapearingPrefab);
            subObject.SetActive(true);
            subObject.transform.position = pos;
            ParticlesController.RegisterParticleSystem(VfxObject, true, duration);
        }

        protected override void CreateVfx(Vector3 pos, bool autoDestroy = false, float duration = 3, bool justPosition = false)
        {
            base.CreateVfx(pos, autoDestroy, duration, justPosition);
        }
    }
}
