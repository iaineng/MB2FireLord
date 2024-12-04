using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FireLord
{
    [HarmonyPatch(typeof(BattleAgentLogic), "OnAgentRemoved")]
    public class DontDiePatch
    {
        public static bool Prefix(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            if (agentState != AgentState.Killed || affectedAgent?.Origin == null || affectedAgent?.Team == null) return true;
            
            if (!affectedAgent.Team.IsPlayerAlly)
            {
                return true;
            }
            
            affectedAgent.Origin.SetWounded();

            return false;
        }
    }
}
