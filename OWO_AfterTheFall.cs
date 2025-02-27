﻿using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using OWOSKin;
using UnityEngine;
using Il2CppSystem;
using Vertigo.Snowbreed;
using Vertigo.Snowbreed.Client;
using Vertigo.VR;
using BepInEx;
using Vertigo.ECS;

namespace OWO_AfterTheFall
{
    [BepInPlugin("org.bepinex.plugins.OWO_AfterTheFall", "After The Fall owo integration", "1.0.0")]
    public class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
    {
        internal static new ManualLogSource Log;        
        public static OWOSkin owoSkin;

        public override void Load()
        {
            //Plugin startup logic
            Log = base.Log;            
            Log.LogInfo("OWO_AfterTheFall loaded!");                        
            
            owoSkin = new OWOSkin();
            owoSkin.Feel("HeartBeat",1);
            //// patch all functions
            var harmony = new Harmony("owo.patch.afterthefall");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(Gun), "FireBullet", new System.Type[] { typeof(bool), typeof(bool) })]
        public class FireGun
        {
            public static uint[] shotgunsIds = { 13, 42 };

            public static void Postfix(Gun __instance)
            {
                if (!__instance.IsEquippedLocally)
                {
                    return;
                }
                Log.LogInfo("AMMO TYPE " + __instance.GunData.AmmoType);
                bool isRight = (__instance.MainHandSide == Vertigo.VR.EHandSide.Right);
                bool dualWield = (__instance.grabbedHands.Count == 2);
                if (shotgunsIds.Contains(__instance.GunData.AmmoType))
                {
                    //Log.LogInfo("is dualwield -> " + dualWield +" | is right -> " + isRight);
                    owoSkin.ShootRecoil("Shotgun", isRight, dualWield);
                }
                else
                {
                    //Log.LogInfo("is dualwield -> " + dualWield +" | is right -> " + isRight);
                    owoSkin.ShootRecoil("Pistol", isRight, dualWield);
                }
            }
        }

        [HarmonyPatch(typeof(SnowbreedPlayerHealthModule), "OnHit")]
        public class PlayerOnDamaged
        {
            public static void Postfix(SnowbreedPlayerHealthModule __instance, HitArgs args)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.Entity.Name.Equals(localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    Vector3 flattenedHit = new Vector3(args.PlayerData.HitDirection.x, 0f, args.PlayerData.HitDirection.z);
                    Vector3 patternOrigin = new Vector3(0f, 0f, -1f);
                    float earlyhitAngle = Vector3.Angle(flattenedHit, patternOrigin);
                    Log.LogInfo("HIT " + args.PlayerData.HitDirection);
                    Log.LogInfo("Early angle " + earlyhitAngle);
                    Vector3 earlycrossProduct = Vector3.Cross(flattenedHit, patternOrigin);
                    Log.LogInfo("Early CROSS " + earlycrossProduct);
                    if (earlycrossProduct.y > 0f) { earlyhitAngle *= -1f; }
                    float myRotation = earlyhitAngle;
                    myRotation *= -1f;
                    if (myRotation < 0f) { myRotation = 360f + myRotation; }
                    
                    Log.LogInfo("Rotation " + myRotation);


                    if(myRotation >= 0 && myRotation <= 180)
                    {
                        Log.LogInfo("ESPALDA");
                        if (myRotation >= 0 && myRotation <= 90) Log.LogInfo("IZQUIERDa");
                        else Log.LogInfo("DERECHA");
                    }
                    else
                    { 
                        Log.LogInfo("FRENTE");
                        if (myRotation >= 270 && myRotation <= 359) Log.LogInfo("IZQUIERDA");
                        else Log.LogInfo("DERCHA");
                    }


                    owoSkin.PlayBackHit("Slash", myRotation);

                    //Low Health
                    if (__instance.Health < (__instance.MaxHealth * 25 / 100))
                    {
                        //start heartbeat lowhealth
                        owoSkin.UpdateHeartBeat(1000);
                        owoSkin.StartHeartBeat();
                    }

                    //Downed, frozen
                    if (__instance.IsDowned)
                    {
                        owoSkin.Feel("Frozen", 2);
                        owoSkin.UpdateHeartBeat(4000);
                        owoSkin.StartHeartBeat();
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(ClientSessionGameSystem), "HandleOnSessionStateChangedEvent")]
        public class OnSessionStateChange
        {
            public static void Postfix(ClientSessionGameSystem __instance)
            {
                owoSkin.StopHeartBeat();
                owoSkin.StopAllHapticFeedback();
                if(__instance.SessionEndingType == SessionGameSystem.ESessionEndingType.Failed)
                {
                    owoSkin.Feel("Death", 4);
                }
            }
        }
        
        [HarmonyPatch(typeof(ClientExplosionGameSystem), "SpawnExplosion",
            new System.Type[] { typeof(Vertigo.Snowbreed.Client.ExplosionTO), typeof(Vector3), typeof(Quaternion), typeof(uint)})]
        public class OnExplosionSpawn
        {
            public static int explosionDistance = 25;
            public static void Postfix(ClientExplosionGameSystem __instance, Vector3 position)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                Vertigo.VRShooter.PawnTransformModule module = localPawn.GetModule<Vertigo.VRShooter.PawnTransformModule>();
                float distance = Vector3.Distance(module.GroundPosition, position);

                if (module != null && distance < explosionDistance)
                {
                    int intensity = Mathf.CeilToInt(((explosionDistance - distance) * 1.1f / explosionDistance) * 100);
                    Log.LogInfo("Intensity will be " + intensity);
                    owoSkin.FeelExplosion(intensity);
                }
            }
        }

        [HarmonyPatch(typeof(ClientSnowbreedPlayerHealthModule), "ApplyHeal")]
        public class PlayerOnHeal
        {
            public static void Postfix(ClientSnowbreedPlayerHealthModule __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.Entity.Name.Equals(localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.Feel("Healing", 2);
                    if (__instance.Health >= (__instance.MaxHealth * 25 / 100))
                    {
                        //stop heartbeat lowhealth
                        owoSkin.StopHeartBeat();
                    }
                    else
                    {
                        //start heartbeat lowhealth in case you healed from frozen state and not enough health
                        owoSkin.UpdateHeartBeat(1000);
                        owoSkin.StartHeartBeat();
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(ZombieGrabAttackView), "Start")]
        public class PlayerOnGrabbedByJuggernautStart
        {
            public static void Postfix(ZombieGrabAttackView __instance, IClientAttackableTarget target)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (target.Entity.Name.Equals(localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.StartZombieGrab();
                }
            }
        }

        [HarmonyPatch(typeof(ZombieGrabAttackView), "Stop")]
        public class PlayerOnGrabbedByJuggernautStop
        {
            public static void Postfix(ZombieGrabAttackView __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                try
                {
                    if (__instance.targetEntityModuleData.targetPawnTrackedTransform.Entity.Name.Equals(
                        localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        owoSkin.StopZombieGrab();
                    }
                } catch (System.Exception)
                {
                    owoSkin.StopZombieGrab();
                }
            }
        }

        [HarmonyPatch(typeof(MissileCombatDeviceLocalController), "StopUse")]
        public class OnCombatDeviceItemUse
        {
            public static void Postfix(MissileCombatDeviceLocalController __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.Owner.identityModule.Entity.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase) && __instance.Owner.CanBeActivated)
                {
                    owoSkin.Feel("MissileRecoil_" + (__instance.Owner.isEquippedOnLeftHand ? "L" : "R"), 3);
                }
            }
        }

        [HarmonyPatch(typeof(ShockwavePunchDeviceItem), "SpawnExplosion")]
        public class OnShockwavePunchDeviceItemUse
        {
            public static void Postfix(ShockwavePunchDeviceItem __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.identityModule.Entity.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.Feel("ShockwaveRecoil_" + (__instance.isEquippedOnLeftHand ? "L" : "R"), 3);
                }
            }
        }

        [HarmonyPatch(typeof(SawbladeDeviceItem), "StopUse")]
        public class OnSawbladeDeviceItemUse
        {
            public static void Postfix(SawbladeDeviceItem __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.identityModule.Entity.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase) && __instance.CanBeActivated)
                {
                    owoSkin.Feel("SawbladeRecoil_" + (__instance.isEquippedOnLeftHand ? "L" : "R"), 3);
                }
            }
        }

        [HarmonyPatch(typeof(ZiplineAttachableTransform), "StartZiplining")]
        public class OnZipLineEnter
        {
            public static void Postfix(ZiplineAttachableTransform __instance, Vertigo.ECS.Entity pawn, EHandSide handSide)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (pawn.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.StartZipline(handSide == EHandSide.Right);
                }
            }
        }

        [HarmonyPatch(typeof(Zipline), "StopUse")]
        public class OnZipLineExit
        {
            public static void Postfix(Zipline __instance, Entity pawn)
            {
                Entity localPawn = LightweightDebug.GetLocalPawn();
                if (pawn.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.StopZipline();
                }
            }
        }
        /*
        [HarmonyPatch(typeof(BoosterSpeedBuffCommand), "ApplyBoost")]
        public class OnRageBoosterStart
        {
            public static void Postfix(BoosterSpeedBuffCommand __instance, Entity user)
            {
                Log.LogMessage("RAGE START");
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (user.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    TactsuitVR.heartBeatRate = 500;
                    tactsuitVr.StartHeartBeat();
                }
            }
        }

        [HarmonyPatch(typeof(BoosterSpeedBuffCommand), "StopBoost")]
        public class OnRageBoosterStop
        {
            public static void Postfix(BoosterSpeedBuffCommand __instance, Entity user)
            {
                Log.LogMessage("RAGE STOP");
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (user.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    tactsuitVr.StopHeartBeat();
                }
            }
        }
        */
        [HarmonyPatch(typeof(ClientPadlock), "HandleOnHandEnterDetectionVolumeEvent")]
        public class OnClientPadlock
        {
            public static void Postfix(ClientPadlock __instance, Vertigo.ECS.Entity entity, Vertigo.VR.EHandSide handSide)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (entity.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.Feel("Padlock_" + (handSide == Vertigo.VR.EHandSide.Right ? "R" : "L"), 2);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerAudioModule), "PlayFootstepLocalPlayer")]
        public class owo_FootStep
        {
            private static bool rightFoot = false;

            [HarmonyPostfix]
            public static void Postfix(PlayerAudioModule __instance)
            {
                Vertigo.ECS.Entity localPawn = LightweightDebug.GetLocalPawn();
                if (__instance.Entity.Name.Equals(
                    localPawn.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    owoSkin.Feel("FootStep_" + (owo_FootStep.rightFoot ? "R" : "L"), 0);
                    owo_FootStep.rightFoot = !owo_FootStep.rightFoot;
                }
            }
        }

        [HarmonyPatch(typeof(Gun), "OnMagazineEjected")]
        public class owo_EjectMagazine
        {
            [HarmonyPostfix]
            public static void Postfix(Gun __instance)
            {
                if (!__instance.IsEquippedLocally)
                {
                    return;
                }
                owoSkin.Feel("MagazineEjected_" + (__instance.MainHandSide == Vertigo.VR.EHandSide.Right ? "R" : "L"), 1);
            }
        }

        [HarmonyPatch(typeof(GunAmmoInserter), "HandleAmmoInsertedEvent")]
        public class owo_Reloading
        {
            [HarmonyPostfix]
            public static void Postfix(GunAmmoInserter __instance)
            {
                if (!__instance.gun.IsEquippedLocally)
                {
                    return;
                }

                owoSkin.Feel("MagazineReloading_" + (__instance.gun.MainHandSide == Vertigo.VR.EHandSide.Right ? "R" : "L"), 1);
            }
        }

        [HarmonyPatch(typeof(GunAmmoInserter), "HandleMagInserterHandleFullyInsertedEvent")]
        public class owo_Reloaded
        {
            [HarmonyPostfix]
            public static void Postfix(GunAmmoInserter __instance)
            {
                if (!__instance.gun.IsEquippedLocally)
                {
                    return;
                }

                owoSkin.Feel("MagazineLoaded_" + (__instance.gun.MainHandSide == Vertigo.VR.EHandSide.Right ? "R" : "L"), 1);
            }
        }
    }
}
