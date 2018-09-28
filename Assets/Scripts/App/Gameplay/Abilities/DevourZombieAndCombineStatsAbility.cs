using System.Collections.Generic;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using UnityEngine;

namespace Loom.ZombieBattleground
{
    public class DevourZombiesAndCombineStatsAbility : AbilityBase
    {
        public int Value;

        public DevourZombiesAndCombineStatsAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
            Value = ability.Value;
        }

        public override void Activate()
        {
            base.Activate();

            if (AbilityCallType != Enumerators.AbilityCallType.ENTRY)
                return;

            VfxObject = LoadObjectsManager.GetObjectByPath<GameObject>("Prefabs/VFX/GreenHealVFX");

            if (Value == -1)
            {
                DevourAllAllyZombies();
            }
        }

        public override void Update()
        {
        }

        public override void Dispose()
        {
        }

        protected override void InputEndedHandler()
        {
            base.InputEndedHandler();

            if (IsAbilityResolved && Value > 0)
            {
                DevourTargetZombie(TargetUnit);
            }
        }

        private void DevourAllAllyZombies()
        {
            List<BoardUnitView> units = PlayerCallerOfAbility.BoardCards;

            foreach (BoardUnitView unit in units)
            {
                DevourTargetZombie(unit.Model);
            }
        }

        private void DevourTargetZombie(BoardUnitModel unit)
        {
            if (unit == AbilityUnitOwner)
                return;

            int health = unit.InitialHp;
            int damage = unit.InitialDamage;

            BattlegroundController.DestroyBoardUnit(unit);

            AbilityUnitOwner.BuffedHp += health;
            AbilityUnitOwner.CurrentHp += health;

            AbilityUnitOwner.BuffedDamage += damage;
            AbilityUnitOwner.CurrentDamage += damage;

            CreateVfx(BattlegroundController.GetBoardUnitViewByModel(unit).Transform.position, true, 5f);
        }
    }
}
