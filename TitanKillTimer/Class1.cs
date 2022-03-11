using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BossKillTimer
{
    [BepInPlugin("com.Moffein.BossKillTimer", "Boss Kill Timer", "1.0.0")]
    public class BossKillTimer : BaseUnityPlugin
    {
        public static bool teleOnly = false;
        public static float instakillThreshold = 0f;
        public void Awake()
        {
            teleOnly = Config.Bind("Settings", "Teleporter Bosses Only", false, "Only show kill times when bosses with red healthbars die.").Value;
            instakillThreshold = Config.Bind("Settings", "Instakill Threshold", 1f, "Display an instakill message when killing a boss in this many seconds or less.").Value;
            On.RoR2.HealthComponent.TakeDamage += (orig, self, di) =>
            {
                KillTimerComponent kt = null;
                if (NetworkServer.active
                && self.body.isChampion
                && (!teleOnly  || self.body.isBoss))
                {
                    if (!self.gameObject.GetComponent<KillTimerComponent>())
                    {
                        kt = self.gameObject.AddComponent<KillTimerComponent>();
                        kt.bodyString = RoR2.Util.GetBestBodyName(self.gameObject);
                        kt.StartTimer();
                    }
                }

                orig(self, di);
            };

            On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
            {
                orig(self, damageReport);

                if (NetworkServer.active)
                {
                    if (damageReport.victim && damageReport.victimIsChampion && (!teleOnly || damageReport.victimIsBoss))
                    {
                        KillTimerComponent kt = damageReport.victim.GetComponent<KillTimerComponent>();
                        if (kt)
                        {
                            kt.EndTimer();
                        }
                    }
                }
            };
        }
    }

    public class KillTimerComponent : MonoBehaviour
    {
        public void FixedUpdate()
        {
            if (timerStarted)
            {
                stopwatch += Time.fixedDeltaTime;
            }
        }

        public void StartTimer()
        {
            if (!timerStarted)
            {
                timerStarted = true;
                stopwatch = 0f;
            }
        }

        public void EndTimer()
        {
            if (!hasDied)
            {
                hasDied = true;

                bool instaKill = stopwatch <= BossKillTimer.instakillThreshold;
                string deathString = string.Empty;
                if (instaKill)
                {
                    deathString += "<style=cIsHealing>INSTANT KILL!</style> ";
                }
                deathString += "<style=cIsHealth>" + bodyString + "</style>";
                deathString += " was killed in ";
                deathString += "<style=cIsDamage>" + stopwatch + "</style>";
                deathString += " seconds!";

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = deathString
                });
            }
        }

        float stopwatch;
        public bool timerStarted = false;
        public bool hasDied = false;
        public string bodyString = "BOSS";
    }
}