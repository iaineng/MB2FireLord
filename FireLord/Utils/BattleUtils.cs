using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FireLord.Utils
{
    /// <summary>
    /// 战斗相关的工具类
    /// </summary>
    public static class BattleUtils
    {
        /// <summary>
        /// 检查当前任务是否为战斗类型任务
        /// </summary>
        public static bool IsInBattle(Mission mission)
        {
            return mission.IsFieldBattle
                   || mission.IsSiegeBattle
                   || !mission.IsFriendlyMission
                   || mission.Mode == MissionMode.Duel
                   || mission.Mode == MissionMode.Stealth
                   || mission.Mode == MissionMode.Tournament;
        }
    }
}