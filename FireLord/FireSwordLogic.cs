using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using FireLord.Settings;
using System.Collections.Generic;
using FireLord.Utils;

namespace FireLord
{
    /// <summary>
    /// 处理火焰剑的逻辑，包括火焰效果的开启和关闭
    /// </summary>
    public class FireSwordLogic : MissionLogic
    {
        #region Fields

        private static bool _playerFireSwordEnabled;
        private readonly IgnitionLogic _ignitionLogic;

        private readonly Dictionary<Agent, AgentFireSwordData> _agentFireSwordDataset =
            new Dictionary<Agent, AgentFireSwordData>();

        private const string BurningParticleName = "psys_game_burning_agent";
        private const float WeaponSectionLength = 0.1f;
        private const float TimerDelay = 0.1f;

        #endregion

        #region Classes

        /// <summary>
        /// 存储代理人火焰剑的相关数据
        /// </summary>
        public class AgentFireSwordData
        {
            public bool Enabled;
            public Agent Agent;
            public GameEntity Entity;
            public Light Light;
            public bool DropLock;
            public MissionTimer Timer;
            public bool LastWieldedWeaponEmpty;

            public void OnAgentWieldedItemChange()
            {
                if (DropLock || Agent == null)
                    return;

                var wieldedWeapon = Agent.WieldedWeapon;
                var wieldedOffHandWeapon = Agent.WieldedOffhandWeapon;

                if (LastWieldedWeaponEmpty && !wieldedWeapon.IsEmpty)
                {
                    if (!Agent.IsMainAgent || _playerFireSwordEnabled)
                    {
                        Timer = new MissionTimer(TimerDelay);
                    }
                }
                else
                {
                    SetFireSwordEnable(false);
                }

                LastWieldedWeaponEmpty = wieldedWeapon.IsEmpty;
            }

            public void OnAgentHealthChanged(Agent agent, float oldHealth, float newHealth)
            {
                if (newHealth <= 0)
                {
                    SetFireSwordEnable(false);
                }
            }

            /// <summary>
            /// 设置火焰剑效果的开启或关闭
            /// </summary>
            public void SetFireSwordEnable(bool enable)
            {
                if (Agent == null) return;

                if (enable)
                {
                    EnableFireSword();
                }
                else
                {
                    DisableFireSword();
                }
            }

            /// <summary>
            /// 开启火焰剑效果
            /// </summary>
            private void EnableFireSword()
            {
                SetFireSwordEnable(false);

                var index = Agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (index == EquipmentIndex.None) return;

                var wieldedWeaponEntity = Agent.GetWeaponEntityFromEquipmentSlot(index);
                var wieldedWeapon = Agent.WieldedWeapon;
                if (wieldedWeapon.IsEmpty) return;

                if (!IsFireSwordAllowed(wieldedWeapon)) return;

                var agentVisuals = Agent.AgentVisuals;
                if (agentVisuals == null) return;

                var skeleton = agentVisuals.GetSkeleton();
                ApplyWeaponFireEffects(wieldedWeapon, wieldedWeaponEntity, skeleton);

                // 处理玩家角色的特殊效果
                if (Agent.IsMainAgent && FireLordConfig.IgnitePlayerBody)
                {
                    ApplyPlayerBodyFireEffects(skeleton, wieldedWeaponEntity);
                }

                // 重新装备武器以显示粒子效果
                ReequipWeapon(index, wieldedWeaponEntity);

                Entity = wieldedWeaponEntity;
                Enabled = true;
            }

            /// <summary>
            /// 关闭火焰剑效果
            /// </summary>
            private void DisableFireSword()
            {
                Enabled = false;
                if (Entity == null || Agent == null) return;

                var agentVisuals = Agent.AgentVisuals;
                if (agentVisuals != null)
                {
                    var skeleton = agentVisuals.GetSkeleton();
                    if (Light != null && skeleton != null)
                        skeleton.RemoveComponent(Light);
                }

                Entity.RemoveAllParticleSystems();
            }

            /// <summary>
            /// 检查是否允许使用火焰剑
            /// </summary>
            private bool IsFireSwordAllowed(MissionWeapon wieldedWeapon)
            {
                var allowed = FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.All
                              || (FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.Player &&
                                  Agent == Agent.Main)
                              || (FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.Heroes &&
                                  Agent.IsHero)
                              || (FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.Companions &&
                                  Agent.IsHero && Agent.Team.IsPlayerTeam)
                              || (FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.Allies &&
                                  Agent.Team.IsPlayerAlly)
                              || (FireLordConfig.FireSwordAllowedUnitType == FireLordConfig.UnitType.Enemies &&
                                  !Agent.Team.IsPlayerAlly);

                if (allowed) return true;

                switch (FireLordConfig.FireSwordWhitelistType)
                {
                    case FireLordConfig.WhitelistType.Troops:
                        allowed = FireLordConfig.FireSwordTroopsWhitelist.Contains(Agent.Character.StringId);
                        break;
                    case FireLordConfig.WhitelistType.Items:
                        allowed = FireLordConfig.FireSwordItemsWhitelist.Contains(wieldedWeapon.ToString());
                        break;
                    case FireLordConfig.WhitelistType.Disabled:
                    default:
                        break;
                }

                return allowed;
            }

            /// <summary>
            /// 为武器添加火焰效果
            /// </summary>
            private void ApplyWeaponFireEffects(MissionWeapon wieldedWeapon, GameEntity wieldedWeaponEntity,
                Skeleton skeleton)
            {
                var length = wieldedWeapon.GetWeaponStatsData()[0].WeaponLength;
                var sections = (int)Math.Round(length / 10f);

                switch (wieldedWeapon.CurrentUsageItem.WeaponClass)
                {
                    case WeaponClass.OneHandedSword:
                    case WeaponClass.TwoHandedSword:
                    case WeaponClass.Mace:
                    case WeaponClass.TwoHandedMace:
                        ApplySwordFireEffects(sections, wieldedWeaponEntity, skeleton);
                        break;
                    case WeaponClass.OneHandedAxe:
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.OneHandedPolearm:
                    case WeaponClass.TwoHandedPolearm:
                    case WeaponClass.LowGripPolearm:
                        ApplyPolearmFireEffects(sections, wieldedWeaponEntity, skeleton);
                        break;
                    case WeaponClass.Undefined:
                    case WeaponClass.Dagger:
                    case WeaponClass.Pick:
                    case WeaponClass.Arrow:
                    case WeaponClass.Bolt:
                    case WeaponClass.Cartridge:
                    case WeaponClass.Bow:
                    case WeaponClass.Crossbow:
                    case WeaponClass.Stone:
                    case WeaponClass.Boulder:
                    case WeaponClass.ThrowingAxe:
                    case WeaponClass.ThrowingKnife:
                    case WeaponClass.Javelin:
                    case WeaponClass.Pistol:
                    case WeaponClass.Musket:
                    case WeaponClass.SmallShield:
                    case WeaponClass.LargeShield:
                    case WeaponClass.Banner:
                    case WeaponClass.NumClasses:
                    default:
                        break;
                }
            }

            /// <summary>
            /// 为剑类武器添加火焰效果
            /// </summary>
            private void ApplySwordFireEffects(int sections, GameEntity wieldedWeaponEntity, Skeleton skeleton)
            {
                for (var i = 1; i < sections; i++)
                {
                    AddFireParticle(i * WeaponSectionLength, wieldedWeaponEntity, skeleton);
                }
            }

            /// <summary>
            /// 为长柄武器添加火焰效果
            /// </summary>
            private void ApplyPolearmFireEffects(int sections, GameEntity wieldedWeaponEntity, Skeleton skeleton)
            {
                var fireLength = sections > 19 ? 9 : sections > 15 ? 6 : sections > 12 ? 5 : sections > 10 ? 4 : 3;
                for (var i = sections - 1; i > 0 && i > sections - fireLength; i--)
                {
                    AddFireParticle(i * WeaponSectionLength, wieldedWeaponEntity, skeleton);
                }
            }

            /// <summary>
            /// 添加单个火焰粒子效果
            /// </summary>
            private void AddFireParticle(float elevation, GameEntity weaponEntity, Skeleton skeleton)
            {
                var localFrame = new MatrixFrame(Mat3.Identity, new Vec3(0)).Elevate(elevation);
                var particle = ParticleSystem.CreateParticleSystemAttachedToEntity(
                    BurningParticleName,
                    weaponEntity,
                    ref localFrame);
                skeleton.AddComponentToBone(Game.Current.DefaultMonster.MainHandItemBoneIndex, particle);
            }

            /// <summary>
            /// 为玩家角色添加全身火焰效果
            /// </summary>
            private void ApplyPlayerBodyFireEffects(Skeleton skeleton, GameEntity weaponEntity)
            {
                int boneCount = skeleton.GetBoneCount();
                for (sbyte i = 0; i < boneCount; i++)
                {
                    var localFrame = new MatrixFrame(Mat3.Identity, new Vec3(0, 0, 0));
                    var particle = ParticleSystem.CreateParticleSystemAttachedToEntity(
                        BurningParticleName,
                        weaponEntity,
                        ref localFrame);
                    skeleton.AddComponentToBone(i, particle);
                }
            }

            /// <summary>
            /// 重新装备武器以显示粒子效果
            /// </summary>
            private void ReequipWeapon(EquipmentIndex index, GameEntity weaponEntity)
            {
                DropLock = true;
                Agent.DropItem(index);
                var spawnedItemEntity = weaponEntity.GetFirstScriptOfType<SpawnedItemEntity>();
                if (spawnedItemEntity != null)
                    Agent.OnItemPickup(spawnedItemEntity, EquipmentIndex.None, out var removeItem);
                DropLock = false;
            }
        }

        #endregion

        public FireSwordLogic(IgnitionLogic ignitionLogic)
        {
            _ignitionLogic = ignitionLogic;
            _ignitionLogic.OnAgentDropItem += SetDropLockForAgent;
            _playerFireSwordEnabled = FireLordConfig.PlayerFireSwordDefaultOn;
        }

        public void SetDropLockForAgent(Agent agent, bool dropLock)
        {
            _agentFireSwordDataset.TryGetValue(agent, out var fireSwordData);
            if (fireSwordData != null)
            {
                fireSwordData.DropLock = dropLock;
            }
        }

        public override void OnAgentCreated(Agent agent)
        {
            if (!BattleUtils.IsInBattle(Mission))
                return;

            if (!agent.IsHuman || _agentFireSwordDataset.ContainsKey(agent)) return;

            var agentFireSwordData = new AgentFireSwordData
            {
                Agent = agent
            };
            agent.OnAgentWieldedItemChange += agentFireSwordData.OnAgentWieldedItemChange;
            agent.OnAgentHealthChanged += agentFireSwordData.OnAgentHealthChanged;
            agentFireSwordData.LastWieldedWeaponEmpty = agent.WieldedWeapon.IsEmpty;
            if (!agent.IsMainAgent || _playerFireSwordEnabled)
            {
                agentFireSwordData.Timer = new MissionTimer(1f);
            }

            _agentFireSwordDataset.Add(agent, agentFireSwordData);
        }

        public override void OnAgentDeleted(Agent agent)
        {
            if (!BattleUtils.IsInBattle(Mission))
                return;
            _agentFireSwordDataset.Remove(agent);
        }

        public override void OnMissionTick(float dt)
        {
            if (!BattleUtils.IsInBattle(Mission))
                return;
            foreach (var item in _agentFireSwordDataset)
            {
                var fireSwordData = item.Value;
                if (item.Key.IsMainAgent && !_playerFireSwordEnabled)
                {
                    fireSwordData.Timer = null;
                }
                else if (fireSwordData.Timer != null && fireSwordData.Timer.Check())
                {
                    fireSwordData.SetFireSwordEnable(true);
                    fireSwordData.Timer = null;
                }
            }

            if (!Input.IsKeyPressed(FireLordConfig.FireSwordToggleKey) || Agent.Main == null) return;

            {
                _playerFireSwordEnabled = !_playerFireSwordEnabled;
                if (!_agentFireSwordDataset.TryGetValue(Agent.Main, out var fireSwordData)) return;

                fireSwordData.SetFireSwordEnable(_playerFireSwordEnabled);
                fireSwordData.Timer = null;
            }
        }

        public override void OnScoreHit(Agent victim, Agent attacker, WeaponComponentData attackerWeapon,
            bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData, float damageHp,
            float hitDistance, float shotDifficulty)
        {
            if (!BattleUtils.IsInBattle(Mission))
                return;

            if (Agent.Main == null || attacker == null || victim == null || attacker.Team == null) return;

            if (!attacker.Team.IsPlayerAlly || attacker != Agent.Main)
            {
                return;
            }

            var fireBarAdd = isBlocked
                ? FireLordConfig.IgnitionPerFireSwordHit / 2f
                : FireLordConfig.IgnitionPerFireSwordHit;
            _ignitionLogic.IncreaseAgentFireBar(attacker, victim, fireBarAdd);
        }
    }
}