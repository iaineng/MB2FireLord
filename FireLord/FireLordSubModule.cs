using System;
using FireLord.Settings;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.MountAndBlade;

namespace FireLord
{
    public class FireLordSubModule : MBSubModuleBase
    {
        public static string ModName => "Fire Lord";
        public static string ModuleName => "FireLord";
        public static string Version => "1.2.0";

        private readonly Harmony _harmony = new Harmony("DontDiePatch");

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        protected override void OnSubModuleLoad()
        {
            FireLordConfig.Init();

            var original = typeof(BattleAgentLogic).GetMethod("OnAgentRemoved");
            var prefix = typeof(DontDiePatch).GetMethod("Prefix");

            try
            {
                _harmony.Patch(original, new HarmonyMethod(prefix));
            }
            catch (Exception e)
            {
                AllocConsole();

                Console.WriteLine(e);
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            var ignitionLogic = new IgnitionLogic();
            mission.AddMissionBehavior(ignitionLogic);
            mission.AddMissionBehavior(new FireArrowLogic(ignitionLogic));
            mission.AddMissionBehavior(new FireSwordLogic(ignitionLogic));
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony.UnpatchAll(_harmony.Id);
        }
    }
}