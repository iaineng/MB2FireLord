using FireLord.Settings;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using FireLord.Utils;

namespace FireLord
{
    /// <summary>
    /// Handles the fire and burning mechanics for agents in the game
    /// </summary>
    public class IgnitionLogic : MissionLogic
    {
        #region Fields and Events

        private static readonly sbyte[] IgnitionBoneIndexes = { 0, 1, 2, 3, 5, 6, 7, 9, 12, 13, 15, 17, 22, 24 };
        public Dictionary<Agent, AgentFireData> AgentFireDataset = new Dictionary<Agent, AgentFireData>();
        public event OnAgentDropItemDelegate OnAgentDropItem;

        #endregion

        #region Delegates and Classes

        public delegate void OnAgentDropItemDelegate(Agent agent, bool dropLock);

        public class AgentFireData
        {
            public bool IsBurning;
            public float FireBar;
            public MissionTimer BurningTimer;
            public GameEntity FireEntity;
            public Light FireLight;
            public Agent Attacker;
            public MissionTimer DamageTimer;
            public ParticleSystem[] Particles;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Increases the fire bar for a victim agent
        /// </summary>
        public void IncreaseAgentFireBar(Agent attacker, Agent victim, float fireBarAdd)
        {
            if (AgentFireDataset.TryGetValue(victim, out var fireData))
            {
                if (fireData.IsBurning) return;

                fireData.FireBar += fireBarAdd;
                fireData.Attacker = attacker;
            }
            else
            {
                var newFireData = new AgentFireData
                {
                    FireBar = fireBarAdd,
                    Attacker = attacker
                };
                AgentFireDataset.Add(victim, newFireData);
            }
        }

        /// <summary>
        /// Checks if the current mission is a battle-type mission
        /// </summary>
        public bool IsInBattle()
        {
            return BattleUtils.IsInBattle(Mission);
        }

        public override void OnMissionTick(float dt)
        {
            if (!BattleUtils.IsInBattle(Mission) || AgentFireDataset.Count <= 0)
                return;

            var deleteAgent = new List<Agent>();
            foreach (var pair in AgentFireDataset)
            {
                var agent = pair.Key;
                var fireData = pair.Value;

                if (fireData.IsBurning)
                {
                    HandleBurningAgent(agent, fireData);
                }
                else
                {
                    HandleNonBurningAgent(agent, fireData, dt, deleteAgent);
                }
            }

            CleanupDeletedAgents(deleteAgent);
        }

        #endregion

        #region Private Methods

        private void HandleBurningAgent(Agent agent, AgentFireData fireData)
        {
            if (FireLordConfig.IgnitionDealDamage &&
                fireData.DamageTimer.Check(true) &&
                agent.IsActive())
            {
                ApplyBurningDamage(agent, fireData);
            }

            if (!fireData.BurningTimer.Check())
                return;

            ExtinguishAgent(agent, fireData);
        }

        private void HandleNonBurningAgent(Agent agent, AgentFireData fireData, float dt, List<Agent> deleteAgent)
        {
            if (fireData.FireBar >= FireLordConfig.IgnitionBarMax)
            {
                IgniteAgent(agent, fireData);
            }
            else
            {
                UpdateFireBar(agent, fireData, dt, deleteAgent);
            }
        }

        private void ApplyBurningDamage(Agent agent, AgentFireData fireData)
        {
            var blow = CreateBlow(fireData.Attacker, agent);
            agent.RegisterBlow(blow, new AttackCollisionData());
            DisplayDamageMessage(agent, fireData.Attacker, blow);
        }

        private void DisplayDamageMessage(Agent victim, Agent attacker, Blow blow)
        {
            if (attacker == Agent.Main)
            {
                var text = GameTexts.FindText("ui_delivered_burning_damage");
                var damageText = Regex.Replace(text.ToString(), @"\d+", blow.InflictedDamage.ToString());
                InformationManager.DisplayMessage(new InformationMessage(damageText));
            }
            else if (victim == Agent.Main)
            {
                var text = GameTexts.FindText("ui_received_burning_damage");
                var damageText = Regex.Replace(text.ToString(), @"\d+", blow.InflictedDamage.ToString());
                InformationManager.DisplayMessage(new InformationMessage(damageText,
                    Color.ConvertStringToColor("#D65252FF")));
            }
        }

        private void ExtinguishAgent(Agent agent, AgentFireData fireData)
        {
            if (fireData.FireEntity != null)
            {
                RemoveFireEffects(agent, fireData);
            }

            fireData.FireBar = 0;
            fireData.IsBurning = false;
        }

        private void RemoveFireEffects(Agent agent, AgentFireData fireData)
        {
            foreach (var particle in fireData.Particles)
            {
                fireData.FireEntity.RemoveComponent(particle);
            }

            if (fireData.FireLight != null)
            {
                fireData.FireLight.Intensity = 0;
                var skeleton = agent.AgentVisuals?.GetSkeleton();
                skeleton?.RemoveBoneComponent((sbyte)HumanBone.Abdomen, fireData.FireLight);
            }

            fireData.FireEntity = null;
            fireData.FireLight = null;
        }

        private void IgniteAgent(Agent agent, AgentFireData fireData)
        {
            InitializeFireData(fireData);
            var index = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            if (index == EquipmentIndex.None)
                return;

            var wieldedWeaponEntity = agent.GetWeaponEntityFromEquipmentSlot(index);
            var skeleton = agent.AgentVisuals?.GetSkeleton();
            if (skeleton == null)
                return;

            CreateFireParticles(fireData, wieldedWeaponEntity, skeleton);
            HandleWeaponDrop(agent, index, wieldedWeaponEntity, fireData);
            // CreateFireLight(skeleton, fireData);
        }

        private void InitializeFireData(AgentFireData fireData)
        {
            fireData.IsBurning = true;
            fireData.BurningTimer = new MissionTimer(FireLordConfig.IgnitionDurationInSecond);
            fireData.DamageTimer = new MissionTimer(1f);
        }

        private void CreateFireParticles(AgentFireData fireData, GameEntity wieldedWeaponEntity, Skeleton skeleton)
        {
            fireData.Particles = new ParticleSystem[IgnitionBoneIndexes.Length];
            for (byte i = 0; i < IgnitionBoneIndexes.Length; i++)
            {
                var localFrame = new MatrixFrame(Mat3.Identity, new Vec3(0));
                var particle = ParticleSystem.CreateParticleSystemAttachedToEntity("psys_campfire",
                    wieldedWeaponEntity, ref localFrame);
                skeleton.AddComponentToBone(IgnitionBoneIndexes[i], particle);
                fireData.Particles[i] = particle;
            }
        }

        private void HandleWeaponDrop(Agent agent, EquipmentIndex index, GameEntity wieldedWeaponEntity,
            AgentFireData fireData)
        {
            OnAgentDropItem?.Invoke(agent, true);
            agent.DropItem(index);
            var spawnedItemEntity = wieldedWeaponEntity.GetFirstScriptOfType<SpawnedItemEntity>();
            if (spawnedItemEntity != null)
                agent.OnItemPickup(spawnedItemEntity, EquipmentIndex.None, out _);
            fireData.FireEntity = wieldedWeaponEntity;
            OnAgentDropItem?.Invoke(agent, false);
        }

        private void CreateFireLight(Skeleton skeleton, AgentFireData fireData)
        {
            var light = Light.CreatePointLight(FireLordConfig.IgnitionLightRadius);
            light.Intensity = FireLordConfig.IgnitionLightIntensity;
            light.LightColor = FireLordConfig.IgnitionLightColor;
            skeleton.AddComponentToBone((sbyte)HumanBone.Abdomen, light);
            fireData.FireLight = light;
        }

        private void UpdateFireBar(Agent agent, AgentFireData fireData, float dt, List<Agent> deleteAgent)
        {
            fireData.FireBar -= dt * FireLordConfig.IgnitionDropPerSecond;
            fireData.FireBar = Math.Max(fireData.FireBar, 0);

            if (!agent.IsActive())
                deleteAgent.Add(agent);
        }

        private void CleanupDeletedAgents(List<Agent> deleteAgent)
        {
            foreach (var agent in deleteAgent)
            {
                if (!AgentFireDataset.TryGetValue(agent, out var fireData)) continue;

                var entity = fireData.FireEntity;
                entity?.RemoveAllParticleSystems();

                var skeleton = agent.AgentVisuals?.GetSkeleton();
                if (skeleton != null && fireData.FireLight != null)
                {
                    skeleton.RemoveBoneComponent((sbyte)HumanBone.Abdomen, fireData.FireLight);
                }

                AgentFireDataset.Remove(agent);
            }
        }

        private Blow CreateBlow(Agent attacker, Agent victim)
        {
            var blow = new Blow(attacker.Index)
            {
                DamageType = DamageTypes.Blunt,
                BlowFlag = BlowFlags.ShrugOff | BlowFlags.NoSound,
                BoneIndex = victim.Monster.HeadLookDirectionBoneIndex,
                GlobalPosition = victim.Position,
                BaseMagnitude = 0,
                InflictedDamage = FireLordConfig.IgnitionDamagePerSecond,
                SwingDirection = victim.LookDirection,
                DamageCalculated = true
            };

            blow.GlobalPosition.z += victim.GetEyeGlobalHeight();
            blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
            blow.SwingDirection.Normalize();
            blow.Direction = blow.SwingDirection;

            return blow;
        }

        #endregion
    }
}