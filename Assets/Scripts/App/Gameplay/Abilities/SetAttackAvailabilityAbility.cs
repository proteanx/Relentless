using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;

namespace Loom.ZombieBattleground
{
    public class SetAttackAvailabilityAbility : AbilityBase
    {
        public SetAttackAvailabilityAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
        }

        public override void Activate()
        {
            base.Activate();

            if (AbilityTrigger != Enumerators.AbilityTrigger.ENTRY)
                return;

            InvokeUseAbilityEvent();

            SetAttackAvailability(AbilityUnitOwner);
        }

        private void SetAttackAvailability(CardModel card)
        {
            if (card == null)
                return;

            if (AbilityTargets.Count > 0)
            {
                card.AttackTargetsAvailability.Clear();

                foreach(Enumerators.Target targetType in AbilityTargets)
                {
                    switch(targetType)
                    {
                        case Enumerators.Target.OPPONENT:
                            card.AttackTargetsAvailability.Add(Enumerators.SkillTarget.OPPONENT);
                            break;
                        case Enumerators.Target.OPPONENT_CARD:
                        case Enumerators.Target.OPPONENT_ALL_CARDS:
                            card.AttackTargetsAvailability.Add(Enumerators.SkillTarget.OPPONENT_CARD);
                            break;
                    }
                }
            }
            else
            {
                card.CanAttackByDefault = false;
            }
        }
    }
}
