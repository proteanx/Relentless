using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using System.Collections.Generic;

namespace Loom.ZombieBattleground
{
    public class UseAllGooToIncreaseStatsAbility : AbilityBase
    {
        public int Value;

        public UseAllGooToIncreaseStatsAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
            Value = ability.Value;
        }

        public override void Activate()
        {
            base.Activate();

            if (AbilityCallType != Enumerators.AbilityCallType.ENTRY)
                return;

            Action();

            AbilitiesController.ThrowUseAbilityEvent(MainWorkingCard, new List<BoardObject>(), AbilityData.AbilityType, Protobuf.AffectObjectType.Character);
        }

        public override void Action(object info = null)
        {
            base.Action(info);

            if (PlayerCallerOfAbility.Goo == 0)
                return;

            int increaseOn;

            increaseOn = PlayerCallerOfAbility.Goo * Value;
            AbilityUnitOwner.BuffedHp += increaseOn;
            AbilityUnitOwner.CurrentHp += increaseOn;

            increaseOn = PlayerCallerOfAbility.Goo * Value;
            AbilityUnitOwner.BuffedDamage += increaseOn;
            AbilityUnitOwner.CurrentDamage += increaseOn;

            PlayerCallerOfAbility.Goo = 0;
        }
    }
}
