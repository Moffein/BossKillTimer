using BepInEx;
using RoR2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}
namespace BossKillTimer
{
    [BepInPlugin("com.Moffein.BossKillTimer", "Boss Kill Timer", "2.0.0")]
    public class BossKillTimer : BaseUnityPlugin
    {
        public static bool teleOnly = false;
        public static float instakillThreshold = 0f;

        private static Dictionary<CharacterBody, float> dict = new Dictionary<CharacterBody, float>();

        public void Awake()
        {
            teleOnly = Config.Bind("Settings", "Teleporter Bosses Only", false, "Only show kill times when bosses with red healthbars die.").Value;
            instakillThreshold = Config.Bind("Settings", "Instakill Threshold", 1f, "Display an instakill message when the kill time is <= to this value.").Value;
            RoR2.Stage.onStageStartGlobal += Stage_onStageStartGlobal;
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            On.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        private void GlobalEventManager_OnCharacterDeath(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, GlobalEventManager self, DamageReport damageReport)
        {
            orig(self, damageReport);
            if (!NetworkServer.active || !(damageReport.victimBody && dict.ContainsKey(damageReport.victimBody))) return;

            if (dict.TryGetValue(damageReport.victimBody, out float startTime))
            {
                float stopwatch = Time.time - startTime;
                bool instaKill = stopwatch <= BossKillTimer.instakillThreshold;
                string deathString = string.Empty;
                if (instaKill)
                {
                    deathString += "<style=cIsHealing>INSTANT KILL!</style> ";
                }
                deathString += "<style=cIsHealth>" + Util.GetBestBodyName(damageReport.victimBody.gameObject) + "</style>";
                deathString += " was killed in ";
                deathString += "<style=cIsDamage>" + stopwatch + "</style>";
                deathString += " seconds!";

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = deathString
                });

                dict.Remove(damageReport.victimBody);
            }
        }

        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            //Add before so that instakills register.
            bool matchesCriteria = self.body && self.body.isChampion && self.body.teamComponent && self.body.teamComponent.teamIndex != TeamIndex.Player;
            bool added = false;
            if (NetworkServer.active && matchesCriteria && !damageInfo.rejected)
            {
                if (!dict.ContainsKey(self.body))
                {
                    added = true;
                    dict.Add(self.body, Time.time);
                }
            }

            orig(self, damageInfo);

            if (added && damageInfo.rejected)
            {
                dict.Remove(self.body);
            }
        }

        private void Stage_onStageStartGlobal(Stage obj)
        {
            dict.Clear();
        }
    }
}