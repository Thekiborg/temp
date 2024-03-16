global using System;
global using System.Collections.Generic;
global using System.Reflection;
global using RimWorld;
global using Verse;
global using UnityEngine;

namespace Thek_ShootPulseLaser
{
    public class Verb_ShootPulseLaser : Verb_LaunchProjectile
    {
        protected override int ShotsPerBurst => verbProps.burstShotCount;
        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out ShootLine resultingLine);
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }
            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompReloadable>()?.UsedOnce();
            }
            lastShotTick = Find.TickManager.TicksGame;
            Thing manningPawn = caster;
            Thing equipmentSource = base.EquipmentSource;
            CompMannable compMannable = caster.TryGetComp<CompMannable>();
            if (compMannable != null && compMannable.ManningPawn != null)
            {
                manningPawn = compMannable.ManningPawn;
                equipmentSource = caster;
            }
            Vector3 drawPos = caster.DrawPos;
            ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
            Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
            ThingDef targetCoverDef = randomCoverToMissInto?.def;
            if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
            {
                resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget);
                ThrowDebugText("ToWild" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
                ThrowDebugText("Wild\nDest", resultingLine.Dest);
                Shoot(manningPawn, drawPos, resultingLine.Dest, currentTarget, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }
            if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
            {
                ThrowDebugText("ToCover" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
                ThrowDebugText("Cover\nDest", randomCoverToMissInto.Position);
                Shoot(manningPawn, drawPos, resultingLine.Dest, currentTarget, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }
            ThrowDebugText("ToHit" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
            if (currentTarget.Thing != null)
            {
                Shoot(manningPawn, drawPos, resultingLine.Dest, currentTarget, preventFriendlyFire, equipmentSource, targetCoverDef);
                ThrowDebugText("Hit\nDest", currentTarget.Cell);
            }
            else
            {
                Shoot(manningPawn, drawPos, resultingLine.Dest, currentTarget, preventFriendlyFire, equipmentSource, targetCoverDef);
                ThrowDebugText("Hit\nDest", resultingLine.Dest);
            }
            return true;
        }

        private void Shoot(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarge, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            Log.Message("shoots");
            Vector3 shotLandPos = usedTarget.Cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.3f);
            Map map = caster.Map;

            if (usedTarget.HasThing && CanHit(usedTarget.Thing))
            {
                if (usedTarget.Thing is Pawn p && p.GetPosture() != 0 && (origin - shotLandPos).MagnitudeHorizontalSquared() >= 20.25f && !Rand.Chance(0.2f))
                {
                    ThrowDebugText("miss-laying", usedTarget.Cell);
                }
                else
                {
                    DoDamage(usedTarget.Thing,
                        new DamageInfo(verbProps.beamDamageDef, verbProps.beamDamageDef.defaultDamage, verbProps.beamDamageDef.defaultArmorPenetration, (currentTarget.Cell - caster.Position).AngleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing),
                        new BattleLogEntry_RangedImpact(caster, usedTarget.Thing, currentTarget.Thing, base.EquipmentSource.def, null, targetCoverDef)
                        );
                }
                return;
            }

            List<Thing> list = VerbUtility.ThingsToHit(usedTarget.Cell, map, CanHit);
            list.Shuffle();
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                float num;
                if (thing is Pawn pawn)
                {
                    num = 0.5f * Mathf.Clamp(pawn.BodySize, 0.1f, 2f);
                    if (pawn.GetPosture() != 0 && (origin - shotLandPos).MagnitudeHorizontalSquared() >= 20.25f)
                    {
                        num *= 0.2f;
                    }
                    if (launcher != null && pawn.Faction != null && launcher.Faction != null && !pawn.Faction.HostileTo(launcher.Faction))
                    {
                        num *= VerbUtility.InterceptChanceFactorFromDistance(origin, usedTarget.Cell);
                    }
                }
                else
                {
                    num = 1.5f * thing.def.fillPercent;
                }
                if (Rand.Chance(num))
                {
                    ThrowDebugText("hit-" + num.ToStringPercent(), usedTarget.Cell);

                    DoDamage(thing,
                        new DamageInfo(verbProps.beamDamageDef, verbProps.beamDamageDef.defaultDamage, verbProps.beamDamageDef.defaultArmorPenetration, (currentTarget.Cell - caster.Position).AngleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing),
                        new BattleLogEntry_RangedImpact(caster, usedTarget.Thing, currentTarget.Thing, base.EquipmentSource.def, null, targetCoverDef)
                        );
                    return;
                }
                ThrowDebugText("miss-" + num.ToStringPercent(), usedTarget.Cell);
                DoDamage(thing, new DamageInfo(verbProps.beamDamageDef, verbProps.beamDamageDef.defaultDamage, verbProps.beamDamageDef.defaultArmorPenetration, (currentTarget.Cell - caster.Position).AngleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing), new BattleLogEntry_RangedImpact(caster, usedTarget.Thing, currentTarget.Thing, base.EquipmentSource.def, null, targetCoverDef));
            }
        }


        private void DoDamage(Thing target, DamageInfo dinfo, BattleLogEntry_RangedImpact log)
        {
            target.TakeDamage(dinfo).AssociateWithLog(log);
        }


        public override bool Available()
        {
            if (CasterIsPawn)
            {
                Pawn casterPawn = CasterPawn;
                if (casterPawn.Faction != Faction.OfPlayer
                    && !verbProps.ai_ProjectileLaunchingIgnoresMeleeThreats
                    && casterPawn.mindState.MeleeThreatStillThreat
                    && casterPawn.mindState.meleeThreat.Position.AdjacentTo8WayOrInside(casterPawn.Position)
                    && EquipmentSource != null
                    && EquipmentUtility.RolePreventsFromUsing(casterPawn, EquipmentSource, out var _)
                    )
                {
                    return false;
                }
            }
            return true;
        }


        public override void WarmupComplete()
        {
            base.WarmupComplete();
        }


        private bool CanHit(Thing thing)
        {
            if (!thing.Spawned) return false;
            if (thing == null) return false;
            if (thing == caster) return false;
            if (verbProps.beamDamageDef == null) return false;
            if (CoverUtility.ThingCovered(thing, caster.Map)) return false;
            if (thing == CurrentTarget && thing.def.Fillage == FillCategory.Full) return true;
            return false;
        }


        private void ThrowDebugText(string text)
        {
            if (DebugViewSettings.drawShooting)
            {
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, text);
            }
        }

        private void ThrowDebugText(string text, IntVec3 c)
        {
            if (DebugViewSettings.drawShooting)
            {
                MoteMaker.ThrowText(c.ToVector3Shifted(), caster.Map, text);
            }
        }
    }
}
