using System.Collections.Generic;
using System.Linq;
using FireLord.Settings;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using FireLord.Utils;

namespace FireLord
{
    public class FireArrowLogic : MissionLogic
    {
        private bool _initialized;
        private bool _fireArrowEnabled;

        private readonly IgnitionLogic _ignitionLogic;

        private readonly List<Mission.Missile> _missiles =
            new List<Mission.Missile>();

        public FireArrowLogic(IgnitionLogic ignitionLogic)
        {
            _ignitionLogic = ignitionLogic;
        }

        public void Initialize()
        {
            var timeOfDay = Mission.Current.Scene.TimeOfDay;

            if (FireLordConfig.FireArrowAllowedTimeStart <= FireLordConfig.FireArrowAllowedTimeEnd)
                _fireArrowEnabled = timeOfDay >= FireLordConfig.FireArrowAllowedTimeStart &&
                                    timeOfDay <= FireLordConfig.FireArrowAllowedTimeEnd;
            else
                _fireArrowEnabled = timeOfDay >= FireLordConfig.FireArrowAllowedTimeStart ||
                                    timeOfDay <= FireLordConfig.FireArrowAllowedTimeEnd;

            _fireArrowEnabled &= !FireLordConfig.UseFireArrowsOnlyInSiege ||
                                 Mission.Current.MissionTeamAIType == Mission.MissionTeamAITypeEnum.Siege;
        }

        public override void OnMissionTick(float dt)
        {
            if (!BattleUtils.IsInBattle(Mission))
                return;

            if (!_initialized && Mission.Current != null && Agent.Main != null)
            {
                _initialized = true;
                Initialize();
            }

            if (!Input.IsKeyPressed(FireLordConfig.FireArrowToggleKey)) return;

            _fireArrowEnabled = !_fireArrowEnabled;
            var text = GameTexts.FindText("ui_fire_arrow_" + (_fireArrowEnabled ? "enabled" : "disabled"));
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position,
            Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (!BattleUtils.IsInBattle(Mission) || !_fireArrowEnabled)
                return;

            var allowed = false;
            var weapon = shooterAgent.Equipment[weaponIndex];
            switch (weapon.CurrentUsageItem.WeaponClass)
            {
                case WeaponClass.Arrow:
                case WeaponClass.Bolt:
                case WeaponClass.Bow:
                case WeaponClass.Crossbow:
                    allowed = true;
                    break;
                case WeaponClass.ThrowingAxe:
                case WeaponClass.ThrowingKnife:
                case WeaponClass.Javelin:
                    allowed = FireLordConfig.AllowFireThrownWeapon;
                    break;
                case WeaponClass.Undefined:
                case WeaponClass.Dagger:
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword:
                case WeaponClass.OneHandedAxe:
                case WeaponClass.TwoHandedAxe:
                case WeaponClass.Mace:
                case WeaponClass.Pick:
                case WeaponClass.TwoHandedMace:
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                case WeaponClass.Cartridge:
                case WeaponClass.Stone:
                case WeaponClass.Boulder:
                case WeaponClass.Pistol:
                case WeaponClass.Musket:
                case WeaponClass.SmallShield:
                case WeaponClass.LargeShield:
                case WeaponClass.Banner:
                case WeaponClass.NumClasses:
                default:
                    break;
            }

            allowed &= FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.All
                       || (FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.Player &&
                           shooterAgent == Agent.Main)
                       || (FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.Heroes &&
                           shooterAgent.IsHero)
                       || (FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.Companions &&
                           shooterAgent.IsHero && shooterAgent.Team.IsPlayerTeam)
                       || (FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.Allies &&
                           shooterAgent.Team.IsPlayerAlly)
                       || (FireLordConfig.FireArrowAllowedUnitType == FireLordConfig.UnitType.Enemies &&
                           !shooterAgent.Team.IsPlayerAlly);

            allowed &= shooterAgent == Agent.Main ||
                       MBRandom.RandomFloatRanged(100) < FireLordConfig.ChancesOfFireArrow;

            if (!allowed)
            {
                switch (FireLordConfig.FireArrowWhitelistType)
                {
                    case FireLordConfig.WhitelistType.Troops:
                        allowed = FireLordConfig.FireArrowTroopsWhitelist.Contains(shooterAgent.Character.StringId);
                        break;
                    case FireLordConfig.WhitelistType.Items:
                        var wieldedWeapon = shooterAgent.WieldedWeapon;
                        if (!wieldedWeapon.IsEmpty)
                        {
                            allowed = FireLordConfig.FireArrowItemsWhitelist.Contains(wieldedWeapon.Item.ToString());
                            var ammoData = wieldedWeapon.GetAmmoWeaponData(false);
                            if (ammoData.IsValid())
                                allowed |= FireLordConfig.FireArrowItemsWhitelist.Contains(ammoData.GetItemObject()
                                    .ToString());
                        }

                        break;
                    case FireLordConfig.WhitelistType.Disabled:
                    default:
                        break;
                }
            }

            if (!allowed) return;

            foreach (var missile in Mission.Current.Missiles)
            {
                if (
                    missile.ShooterAgent != shooterAgent ||
                    _missiles.Contains(missile) ||
                    missile.Entity == null
                ) continue;

                var localFrame = new MatrixFrame(Mat3.Identity, new Vec3(0));
                ParticleSystem.CreateParticleSystemAttachedToEntity(
                    "psys_game_burning_agent", missile.Entity, ref localFrame);

                _missiles.Add(missile);
                break;
            }
        }

        public override void OnMissileCollisionReaction(Mission.MissileCollisionReaction collisionReaction,
            Agent attacker, Agent victim, sbyte attachedBoneIndex)
        {
            if (!BattleUtils.IsInBattle(Mission) || !_fireArrowEnabled)
                return;

            foreach (var missile in _missiles.ToList().Where(missile => missile.ShooterAgent == attacker))
            {
                missile.Entity?.RemoveAllParticleSystems();

                _missiles.Remove(missile);

                if (!FireLordConfig.IgniteTargetWithFireArrow) continue;

                if (victim == null || !victim.IsHuman) continue;

                if (!FireLordConfig.IgnitionFriendlyFire && !attacker.IsEnemyOf(victim)) continue;

                _ignitionLogic.IncreaseAgentFireBar(attacker, victim, FireLordConfig.IgnitionPerFireArrow);
            }
        }
    }
}