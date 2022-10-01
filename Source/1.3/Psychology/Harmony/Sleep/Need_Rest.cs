﻿using System;
using System.Reflection.Emit;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Psychology.Harmony;

[HarmonyPatch(typeof(Need_Rest), nameof(Need_Rest.NeedInterval))]
public static class Need_Rest_IntervalDreamPatch
{

    [HarmonyPostfix]
    public static void CauseDream(Need_Rest __instance, Pawn ___pawn)
    {
        if (Traverse.Create(__instance).Property("Resting").GetValue<bool>() != true)
        {
            return;
        }
        //Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        Pawn pawn = ___pawn;
        if (Rand.Value < 0.001f && pawn.RaceProps.Humanlike && !pawn.Awake())
        {
            if (Rand.Value < 0.5f)
            {
                if (Rand.Value < 0.125f)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamNightmare, pawn);
                    return;
                }
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamBad, pawn);
                return;
            }
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamGood, pawn);
        }
    }

}

[HarmonyPatch(typeof(Need_Rest), nameof(Need_Rest.NeedInterval))]
public static class Need_Rest_IntervalInsomniacPatch
{

    [HarmonyPostfix]
    public static void MakeInsomniacLessRestful(Need_Rest __instance, Pawn ___pawn)
    {
        if (Traverse.Create(__instance).Property("Resting").GetValue<bool>() != true)
        {
            return;
        }
        //Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        Pawn pawn = ___pawn;
        if (Traverse.Create(__instance).Property("IsFrozen").GetValue<bool>())
        {
            return;
        }

        if (!Traverse.Create(__instance).Property("IsFrozen").GetValue<bool>() && pawn.RaceProps.Humanlike && pawn.story.traits.HasTrait(TraitDefOfPsychology.Insomniac) && !pawn.health.hediffSet.HasHediff(HediffDefOfPsychology.SleepingPills))
        {
            __instance.CurLevel -= (2f * 150f * Need_Rest.BaseRestGainPerTick) / 3f;
            if (__instance.CurLevel > (Need_Rest.DefaultNaturalWakeThreshold / 4f))
            {
                if (Rand.MTBEventOccurs((Need_Rest.DefaultNaturalWakeThreshold - __instance.CurLevel) / 4f, GenDate.TicksPerDay, 150f) && !pawn.Awake())
                {
                    pawn.jobs.curDriver.asleep = false;
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

    }
}
