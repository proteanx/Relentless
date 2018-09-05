using Loom.ZombieBattleground.Common;

namespace Loom.ZombieBattleground
{
    public interface ITutorialManager
    {
        TutorialDataStep CurrentTutorialDataStep { get; }

        bool IsTutorial { get; }

        bool IsBubbleShow { get; set; }

        void StartTutorial();

        void StopTutorial();

        void ReportAction(Enumerators.TutorialReportAction action);

        void ActivateSelectTarget();

        void DeactivateSelectTarget();

        void NextButtonClickHandler();

        void SkipTutorial(Enumerators.AppState state);
    }
}
