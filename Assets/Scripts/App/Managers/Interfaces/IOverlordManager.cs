using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;

namespace Loom.ZombieBattleground
{
    public interface IOverlordManager
    {
        Hero HeroInStartGame { get; }

        void ChangeExperience(Hero hero, int value);
        int GetRequiredExperienceForNewLevel(Hero hero);
        void ReportExperienceAction(Hero hero, Enumerators.ExperienceActionType actionType);
        OverlordManager.LevelReward GetLevelReward(Hero hero);
    }
}
